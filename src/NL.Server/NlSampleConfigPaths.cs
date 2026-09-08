namespace NL.Server;

/// <summary>Resolve bundled <c>samples/configs/*.nle</c> paths for demo and web editor.</summary>
public static class NlSampleConfigPaths
{
    public static string Resolve(string fileName)
    {
        foreach (var root in CandidateRoots())
        {
            var path = Path.Combine(root, "samples", "configs", fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return fileName;
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = seed;
            for (var depth = 0; depth < 10 && !string.IsNullOrEmpty(dir); depth++)
            {
                if (seen.Add(dir))
                {
                    yield return dir;
                }

                dir = Directory.GetParent(dir)?.FullName ?? "";
            }
        }
    }
}
