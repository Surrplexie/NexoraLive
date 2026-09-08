namespace NL.Fork.Catalog.Core;

/// <summary>Raised when a streamer selects a non-latest major without beta/paid entitlement.</summary>
public sealed class ForkCatalogVersionAccessException : Exception
{
    public ForkCatalogVersionAccessException(string message) : base(message)
    {
    }
}
