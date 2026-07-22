using NL.Core;
using NL.NleEditor.Model;

namespace NL.NleEditor;

public sealed record NleEvaluateRequest(
    string EventName,
    IReadOnlyDictionary<string, double>? Properties = null,
    ConfigModel? Model = null,
    string? NleText = null);

public sealed record NleEvaluateResult(
    string Decision,
    string? Message,
    bool ParseOk,
    string? Error = null);

/// <summary>Live rule preview — same flow as WinForms Config Editor evaluate panel.</summary>
public static class NleEditorEvaluate
{
    public static NleEvaluateResult Evaluate(NleEvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            return new NleEvaluateResult("Error", null, false, "eventName required.");
        }

        string nleText;
        try
        {
            nleText = ResolveNleText(request);
        }
        catch (Exception ex)
        {
            return new NleEvaluateResult("Error", null, false, $"Writer error: {ex.Message}");
        }

        RuleEngine engine;
        try
        {
            var ast = Parser.Parse(nleText);
            engine = new RuleEngine(ast);
        }
        catch (Exception ex)
        {
            return new NleEvaluateResult("Error", null, false, $"Parse error: {ex.Message}");
        }

        var props = request.Properties ?? new Dictionary<string, double>();
        var gameEvent = new GameEvent(request.EventName.Trim(), props);
        var result = engine.Evaluate(gameEvent);

        return new NleEvaluateResult(
            result.Decision.ToString(),
            result.Message,
            ParseOk: true);
    }

    private static string ResolveNleText(NleEvaluateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.NleText))
        {
            return request.NleText;
        }

        if (request.Model is not null)
        {
            return NleWriter.Write(request.Model);
        }

        throw new InvalidOperationException("Provide model or nleText.");
    }
}
