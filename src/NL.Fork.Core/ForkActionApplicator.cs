using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Fork.Core;

/// <summary>Maps <see cref="NlStandardActions"/> verbs to in-fork server commands.</summary>
public sealed class ForkActionApplicator
{
    private readonly List<ForkAppliedAction> _applied = [];

    public IReadOnlyList<ForkAppliedAction> Applied => _applied;

    public void Apply(NlActionMessage action, ForkWorldState world)
    {
        _applied.Add(new ForkAppliedAction(
            action.Action,
            action.Player,
            action.Event,
            action.Message,
            DateTimeOffset.UtcNow));

        if (!world.TryGetPlayer(action.Player, out var player) || player is null)
        {
            return;
        }

        switch (action.Action.ToLowerInvariant())
        {
            case "kick":
                world.RemovePlayer(action.Player);
                break;
            case "despawn":
                player.Connected = false;
                world.RemovePlayer(action.Player);
                break;
            case "recover":
                player.Health = Math.Min(100, player.Health + 25);
                player.X = 0;
                player.Y = 0;
                player.Z = 0;
                break;
            case "mute":
                // hello-fork: no chat queue; recorded in applied log only
                break;
            case "tell":
                // message delivered via action log / overlay in full client
                break;
            case "warn":
                break;
            case "custom":
            case "stripweapon":
                player.HasWeapon = false;
                break;
            default:
                if (string.Equals(action.Event, "shoot", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(action.Decision, "Block", StringComparison.OrdinalIgnoreCase))
                {
                    player.HasWeapon = false;
                }

                break;
        }
    }

    public void ApplyBlockSideEffects(SessionEvent sessionEvent, ActionResult result, ForkWorldState world)
    {
        if (result.Decision != Decision.Block)
        {
            return;
        }

        var verb = NlStandardActions.ChooseAction(sessionEvent);
        var message = new NlActionMessage(
            verb,
            sessionEvent.PlayerName ?? "",
            sessionEvent.Event.Name,
            result.Decision.ToString(),
            result.Message ?? "",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Apply(message, world);
    }
}
