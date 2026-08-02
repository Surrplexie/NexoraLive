namespace NL.Fork.Catalog.Core;

public interface IForkCatalogRepository
{
    ForkCatalogManifest Load();

    void Save(ForkCatalogManifest manifest);
}
