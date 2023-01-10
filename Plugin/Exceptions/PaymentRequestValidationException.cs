using System;

namespace BTCPayServer.Plugins.LNbank.Exceptions;

public class PaymentRequestValidationException : Exception
{
    public PaymentRequestValidationException(string message) : base(message)
    {
    }
}
