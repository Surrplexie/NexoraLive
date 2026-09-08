using NL.Server.Core;

namespace NL.Server.Core.Integration;

/// <summary>Maps blocked session events to standard NL integration action verbs.</summary>
public static class NlStandardActions
{
    public static string ChooseAction(SessionEvent sessionEvent)
    {
        var name = sessionEvent.Event.Name;
        if (string.Equals(name, "playerJoin", StringComparison.OrdinalIgnoreCase))
        {
            return "kick";
        }

        if (string.Equals(name, "playerChat", StringComparison.OrdinalIgnoreCase))
        {
            return "tell";
        }

        if (name.StartsWith("anomaly", StringComparison.OrdinalIgnoreCase)
            || name is "crash" or "rollover" or "leaveBoundary" or "airtime")
        {
            return "recover";
        }

        return "warn";
    }
}
