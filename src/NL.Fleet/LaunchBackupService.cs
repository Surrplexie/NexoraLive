using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 9 — fleet data backup verification.</summary>
public sealed class LaunchBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LaunchBackupCheckResult Evaluate(NlLaunchOpsSettings settings, bool hostBackupVerified)
    {
        if (hostBackupVerified)
        {
            return new LaunchBackupCheckResult(true, settings.BackupRoot, DateTimeOffset.UtcNow, "host verified");
        }

        var root = settings.BackupRoot;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return new LaunchBackupCheckResult(false, root, null, "backup root missing");
        }

        var manifestPath = Path.Combine(root, "latest-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new LaunchBackupCheckResult(false, root, null, "no latest-manifest.json");
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("createdAtUtc", out var createdProp))
            {
                return new LaunchBackupCheckResult(false, root, null, "manifest missing createdAtUtc");
            }

            var created = createdProp.GetDateTimeOffset();
            var age = DateTimeOffset.UtcNow - created;
            var maxAge = TimeSpan.FromHours(settings.BackupMaxAgeHours);
            var passed = age <= maxAge;
            return new LaunchBackupCheckResult(
                passed,
                root,
                created,
                passed ? $"age={age.TotalHours:F1}h" : $"stale age={age.TotalHours:F1}h max={settings.BackupMaxAgeHours}h");
        }
        catch (Exception ex)
        {
            return new LaunchBackupCheckResult(false, root, null, ex.Message);
        }
    }

    public string WriteManifest(string backupRoot, string sourceRoot, IReadOnlyList<string> includedPaths)
    {
        Directory.CreateDirectory(backupRoot);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var snapshotDir = Path.Combine(backupRoot, stamp);
        Directory.CreateDirectory(snapshotDir);

        foreach (var relative in includedPaths)
        {
            var src = Path.IsPathRooted(relative) ? relative : Path.Combine(sourceRoot, relative);
            if (!File.Exists(src) && !Directory.Exists(src))
            {
                continue;
            }

            var dest = Path.Combine(snapshotDir, relative.Replace('/', Path.DirectorySeparatorChar));
            var destParent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destParent))
            {
                Directory.CreateDirectory(destParent);
            }

            if (Directory.Exists(src))
            {
                CopyDirectory(src, dest);
            }
            else
            {
                File.Copy(src, dest, overwrite: true);
            }
        }

        var manifest = new
        {
            createdAtUtc = DateTimeOffset.UtcNow,
            snapshotDir = stamp,
            sourceRoot,
            includedPaths,
        };
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(Path.Combine(snapshotDir, "manifest.json"), manifestJson);
        File.WriteAllText(Path.Combine(backupRoot, "latest-manifest.json"), manifestJson);
        return snapshotDir;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
