using NL.Core;
using NL.NleEditor;
using NL.NleEditor.Model;

namespace NL.Server;

/// <summary>Phase I — browser rule editor sandbox under <see cref="NlPaths.WebEditorSandbox"/>.</summary>
public sealed class NlWebEditorStore
{
    private readonly object _lock = new();

    public string SandboxPath => NlPaths.WebEditorSandbox;

    public WebEditorSnapshot Load(string? fallbackConfigPath)
    {
        lock (_lock)
        {
            NlPaths.EnsureRoot();
            var sourcePath = ResolveLoadPath(fallbackConfigPath);
            if (!File.Exists(sourcePath))
            {
                var starter = new ConfigModel
                {
                    Events =
                    [
                        new EventEntry
                        {
                            Name = "shoot",
                            Statements = [new StatementEntry { Type = StatementType.Block }],
                        },
                    ],
                };
                var starterText = NleWriter.Write(starter);
                return new WebEditorSnapshot
                {
                    Model = starter,
                    NleText = starterText,
                    SourcePath = SandboxPath,
                    IsSandbox = false,
                    ParseOk = true,
                };
            }

            var source = File.ReadAllText(sourcePath);
            var model = NleLoader.Load(source);
            var nleText = NleWriter.Write(model);
            return new WebEditorSnapshot
            {
                Model = model,
                NleText = nleText,
                SourcePath = sourcePath,
                IsSandbox = PathsEqual(sourcePath, SandboxPath),
                ParseOk = true,
            };
        }
    }

    public WebEditorSaveResult Save(ConfigModel model)
    {
        lock (_lock)
        {
            NlPaths.EnsureRoot();
            var nleText = NleWriter.Write(model);
            _ = Parser.Parse(nleText);
            File.WriteAllText(SandboxPath, nleText);
            return new WebEditorSaveResult
            {
                Ok = true,
                NleText = nleText,
                SourcePath = SandboxPath,
            };
        }
    }

    public void ResetFromTemplate(string templateFileName)
    {
        lock (_lock)
        {
            NlPaths.EnsureRoot();
            var templatePath = NlSampleConfigPaths.Resolve(templateFileName);
            if (!File.Exists(templatePath))
            {
                if (File.Exists(SandboxPath))
                {
                    File.Delete(SandboxPath);
                }

                return;
            }

            File.Copy(templatePath, SandboxPath, overwrite: true);
        }
    }

    public bool SandboxExists()
    {
        lock (_lock)
        {
            return File.Exists(SandboxPath);
        }
    }

    public bool IsSandboxPath(string? configPath) =>
        !string.IsNullOrWhiteSpace(configPath) && PathsEqual(configPath, SandboxPath);

    private string ResolveLoadPath(string? fallbackConfigPath)
    {
        if (File.Exists(SandboxPath))
        {
            return SandboxPath;
        }

        if (!string.IsNullOrWhiteSpace(fallbackConfigPath) && File.Exists(fallbackConfigPath))
        {
            return fallbackConfigPath;
        }

        var demo = NlSampleConfigPaths.Resolve("demo.nle");
        if (File.Exists(demo))
        {
            return demo;
        }

        var generic = NlSampleConfigPaths.Resolve("generic.nle");
        if (File.Exists(generic))
        {
            return generic;
        }

        return SandboxPath;
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

public sealed class WebEditorSnapshot
{
    public required ConfigModel Model { get; init; }
    public required string NleText { get; init; }
    public required string SourcePath { get; init; }
    public bool IsSandbox { get; init; }
    public bool ParseOk { get; init; }
    public string? Error { get; init; }
}

public sealed class WebEditorSaveResult
{
    public bool Ok { get; init; }
    public string? NleText { get; init; }
    public string? SourcePath { get; init; }
    public string? Error { get; init; }
}
