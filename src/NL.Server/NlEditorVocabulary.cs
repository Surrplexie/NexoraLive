namespace NL.Server;

/// <summary>Known event/property vocabulary for the Phase I web rule editor.</summary>
public static class NlEditorVocabulary
{
    public static object ToPublicInfo() => new
    {
        events = new[]
        {
            "sessionStart",
            "playerJoin",
            "playerLeave",
            "shoot",
            "respawn",
            "leaveBoundary",
            "useItem",
            "playerChat",
        },
        properties = new[]
        {
            new { name = "player.health", type = "number", example = "100" },
            new { name = "player.hasItem", type = "number", example = "1" },
            new { name = "chat.length", type = "number", example = "24" },
            new { name = "chat.capsRatio", type = "number", example = "0.9" },
        },
        comparators = new[] { ">", "<", ">=", "<=", "==", "!=" },
        joins = new[] { "and", "or" },
        statementTypes = new[]
        {
            new { id = "allow", label = "Allow" },
            new { id = "block", label = "Block" },
            new { id = "deny", label = "Deny" },
            new { id = "warn", label = "Warn" },
            new { id = "if", label = "If / else" },
        },
    };
}
