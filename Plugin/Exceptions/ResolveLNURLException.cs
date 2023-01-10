using System;

namespace BTCPayServer.Plugins.LNbank.Exceptions;

public class ResolveLNURLException : Exception
{
    public string Destination { get; set; }

    public ResolveLNURLException(string destination, string message) : base(message)
    {
        Destination = destination;
    }
}
