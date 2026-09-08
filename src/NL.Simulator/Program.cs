using NL.Core;
using NL.Simulator;

string configText;
string configLabel;

if (args.Length > 0)
{
    var path = args[0];
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Config file not found: {path}");
        return 1;
    }

    configText = File.ReadAllText(path);
    configLabel = path;
}
else
{
    configText = DefaultConfig.Source;
    configLabel = "(built-in default config — pass a .nle file path as an argument to use your own)";
}

Console.WriteLine("NL Simulator - NLEvents prototype");
Console.WriteLine($"Config: {configLabel}");
Console.WriteLine();

RuleEngine engine;
try
{
    engine = RuleEngine.FromSource(configText);
}
catch (NlSyntaxException ex)
{
    Console.Error.WriteLine($"Failed to load config: {ex.Message}");
    return 1;
}

foreach (var warning in engine.LoadWarnings)
{
    Console.WriteLine($"[load warning] {warning}");
}

if (engine.LoadWarnings.Count > 0)
{
    Console.WriteLine();
}

foreach (var scripted in MockGameEventSource.DefaultScript())
{
    var result = engine.Evaluate(scripted.Event);
    var propsText = scripted.Event.Properties.Count == 0
        ? ""
        : " {" + string.Join(", ", scripted.Event.Properties.Select(p => $"{p.Key}={p.Value}")) + "}";

    Console.WriteLine($"- {scripted.Description}");
    Console.WriteLine($"    event:    {scripted.Event.Name}{propsText}");
    Console.WriteLine($"    decision: {result}");
    Console.WriteLine();
}

return 0;
