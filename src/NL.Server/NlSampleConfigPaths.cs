namespace NL.Server;

/// <summary>Resolve bundled <c>samples/configs/*.nle</c> paths for demo and web editor.</summary>
public static class NlSampleConfigPaths
{
    public static string Resolve(string fileName)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "configs", fileName)),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "configs", fileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "samples", "configs", fileName)),
        };

        return candidates.FirstOrDefault(File.Exists) ?? fileName;
    }
}
