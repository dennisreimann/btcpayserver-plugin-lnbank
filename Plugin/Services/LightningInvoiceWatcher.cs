using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LightningInvoiceWatcher : BackgroundService
{
    private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    // grace period before starting to check a pending transaction, which is inflight
    // and might get handled in the request context that initiated the payment 
    private static readonly TimeSpan _inflightDelay = WalletService.SendTimeout + _checkInterval;
    private readonly BTCPayService _btcpayService;
    private readonly ILogger<LightningInvoiceWatcher> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WalletRepository _walletRepository;

    public LightningInvoiceWatcher(
        BTCPayService btcpayService,
        WalletRepository walletRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LightningInvoiceWatcher> logger)
    {
        _logger = logger;
        _btcpayService = btcpayService;
        _walletRepository = walletRepository;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting");

        cancellationToken.Register(() => _logger.LogInformation("Stop request via cancellation"));

        while (!cancellationToken.IsCancellationRequested)
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            WalletService walletService = scope.ServiceProvider.GetRequiredService<WalletService>();

            IEnumerable<Transaction> transactions = await _walletRepository.GetPendingTransactions();
            List<Transaction> list = transactions.ToList();
            int count = list.Count;

            if (count > 0)
            {
                _logger.LogDebug("Processing {Count} transactions", count);

                try
                {
                    await Task.WhenAll(list.Select(transaction =>
                    {
                        using CancellationTokenSource cts =
                            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(_checkInterval);
                        return CheckPendingTransaction(walletService, transaction, cts.Token);
                    }));
                }
                catch (Exception exception) when (exception is TaskCanceledException or OperationCanceledException)
                {
                    _logger.LogInformation("Checking pending transactions canceled after time out");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Checking pending transactions failed: {Message}", exception.Message);
                }
            }

            await Task.Delay(_checkInterval, cancellationToken);
        }

        _logger.LogInformation("Ending, cancellation requested");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping");

        await Task.CompletedTask;
    }

    private async Task CheckPendingTransaction(WalletService walletService, Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        await (string.IsNullOrEmpty(transaction.InvoiceId)
            ? CheckPayment(walletService, transaction, cancellationToken)
            : CheckInvoice(walletService, transaction, cancellationToken));
    }

    private async Task CheckInvoice(WalletService walletService, Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        LightningInvoiceData invoice = null;
        string errorDetails = null;
        bool invalidate = false;

        try
        {
            invoice = await _btcpayService.GetLightningInvoice(transaction.InvoiceId, cancellationToken);
        }
        catch (GreenfieldAPIException apiException) when (apiException.APIError.Code == "invoice-not-found")
        {
            errorDetails = apiException.Message;
            invalidate = true;
        }
        catch (Exception exception)
        {
            errorDetails = exception.Message;
        }

        if (invoice == null)
        {
            _logger.LogWarning(
                "Unable to resolve invoice (Invoice Id = {InvoiceId}) for transaction {TransactionId}{Details}",
                transaction.InvoiceId, transaction.TransactionId,
                string.IsNullOrEmpty(errorDetails) ? "" : $": {errorDetails}");
            if (invalidate)
                await walletService.Invalidate(transaction);
            return;
        }

        switch (invoice.Status)
        {
            case LightningInvoiceStatus.Paid:
                {
                    DateTimeOffset paidAt = invoice.PaidAt ?? DateTimeOffset.Now;
                    LightMoney
                        amount = invoice.Amount ?? invoice.AmountReceived; // Zero amount invoices have amount as null value
                    LightMoney feeAmount = amount - invoice.AmountReceived;
                    await walletService.Settle(transaction, amount, invoice.AmountReceived, feeAmount, paidAt);
                    break;
                }
            case LightningInvoiceStatus.Expired:
                await walletService.Expire(transaction);
                break;
        }
    }

    private async Task CheckPayment(WalletService walletService, Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        LightningPaymentData payment = null;
        string errorDetails = null;
        bool invalidate = false;

        try
        {
            payment = await _btcpayService.GetLightningPayment(transaction.PaymentHash, cancellationToken);
        }
        catch (GreenfieldAPIException apiException) when (apiException.APIError.Code == "payment-not-found")
        {
            errorDetails = apiException.Message;
            invalidate = true;
        }
        catch (Exception exception)
        {
            errorDetails = exception.Message;
        }

        if (payment == null)
        {
            bool isInflight = transaction.IsPending && transaction.CreatedAt > DateTimeOffset.Now - _inflightDelay;
            if (!isInflight)
            {
                invalidate = true;
                _logger.LogWarning(
                    "Unable to resolve payment (Payment Hash = {PaymentHash}) for transaction {TransactionId}{Details}",
                    transaction.PaymentHash, transaction.TransactionId,
                    string.IsNullOrEmpty(errorDetails) ? "" : $": {errorDetails}");
            }

            if (invalidate)
                await walletService.Invalidate(transaction);
            return;
        }

        switch (payment.Status)
        {
            case LightningPaymentStatus.Complete:
                {
                    DateTimeOffset paidAt = payment.CreatedAt ?? DateTimeOffset.Now;
                    LightMoney originalAmount = payment.TotalAmount - payment.FeeAmount;
                    await walletService.Settle(transaction, originalAmount, payment.TotalAmount * -1, payment.FeeAmount,
                        paidAt);
                    break;
                }
            case LightningPaymentStatus.Failed:
                _logger.LogWarning(
                    "Failed payment (Payment Hash = {PaymentHash}) for transaction {TransactionId} - invalidating transaction",
                    transaction.PaymentHash, transaction.TransactionId);
                await walletService.Invalidate(transaction);
                break;
            case LightningPaymentStatus.Unknown:
            case LightningPaymentStatus.Pending:
                _logger.LogDebug("Transaction {TransactionId} status: {Status}",
                    transaction.TransactionId, payment.Status.ToString());
                break;
        }
    }
}
