namespace NL.Core;

/// <summary>
/// A mocked in-game event fed into the <see cref="RuleEngine"/>. Real game integration
/// (see ROADMAP.md Phase 3) would replace <see cref="MockGameEventSource"/> as the producer
/// of these, not this type itself.
/// </summary>
public sealed record GameEvent(string Name, IReadOnlyDictionary<string, double> Properties)
{
    public static GameEvent Simple(string name) => new(name, new Dictionary<string, double>());

    public static GameEvent Create(string name, params (string Key, double Value)[] properties)
    {
        var dict = new Dictionary<string, double>();
        foreach (var (key, value) in properties)
        {
            dict[key] = value;
        }

        return new GameEvent(name, dict);
    }
}
