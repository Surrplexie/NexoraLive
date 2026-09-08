using NL.Core.Sp;

namespace NL.Moderation.Core;

/// <summary>Looks up and persists <see cref="SpProfile"/>s so mod actions (warn/ban/graylist)
/// survive across calls. Implementations: an in-memory repository for tests, or a real JSON
/// file repository (see <c>NL.Moderation.JsonFileSpProfileRepository</c>).</summary>
public interface ISpProfileRepository
{
    SpProfile? Find(string playerId);

    SpProfile GetOrCreate(string playerId, string displayName);

    void Save(SpProfile profile);

    IReadOnlyList<SpProfile> All();
}
