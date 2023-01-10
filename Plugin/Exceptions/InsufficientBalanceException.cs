using System;

namespace BTCPayServer.Plugins.LNbank.Exceptions;

public class InsufficientBalanceException : Exception
{
    public InsufficientBalanceException(string message) : base(message)
    {
    }
}
