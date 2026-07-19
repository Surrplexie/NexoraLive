using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Core.Sp;

namespace NL.Moderation;

/// <summary>
/// Loads/saves a streamer's <see cref="JoinRequirements"/> as JSON under
/// <c>%LOCALAPPDATA%\NL\join-requirements.json</c> (or an explicit path) so NLServer's join
/// gate and Session Host share the same rules the ModerationConsole/admin set up.
/// </summary>
public static class JoinRequirementsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static JoinRequirements LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return JoinRequirements.None;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return JoinRequirements.None;
        }

        return JsonSerializer.Deserialize<JoinRequirementsDto>(json, JsonOptions)?.ToRequirements()
               ?? JoinRequirements.None;
    }

    public static void Save(string path, JoinRequirements requirements)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(JoinRequirementsDto.FromRequirements(requirements), JsonOptions);
        File.WriteAllText(path, json);
    }

    private sealed class JoinRequirementsDto
    {
        public bool RequireFollow { get; set; }
        public bool RequireSubscription { get; set; }
        public int MinAccountAgeDays { get; set; }
        public SpVerification RequiredVerification { get; set; }
        public int MaxActiveOffenses { get; set; } = int.MaxValue;
        public bool AllowGraylistWithHold { get; set; } = true;

        public static JoinRequirementsDto FromRequirements(JoinRequirements r) => new()
        {
            RequireFollow = r.RequireFollow,
            RequireSubscription = r.RequireSubscription,
            MinAccountAgeDays = r.MinAccountAgeDays,
            RequiredVerification = r.RequiredVerification,
            MaxActiveOffenses = r.MaxActiveOffenses,
            AllowGraylistWithHold = r.AllowGraylistWithHold,
        };

        public JoinRequirements ToRequirements() => new(
            RequireFollow,
            RequireSubscription,
            MinAccountAgeDays,
            RequiredVerification,
            MaxActiveOffenses,
            AllowGraylistWithHold);
    }
}
