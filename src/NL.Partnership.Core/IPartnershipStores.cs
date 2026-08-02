namespace NL.Partnership.Core;

public interface IAtOwnRiskAcknowledgmentStore
{
    AtOwnRiskAcknowledgment? Get(string playerId, string gameId);

    void Save(AtOwnRiskAcknowledgment acknowledgment);

    IReadOnlyList<AtOwnRiskAcknowledgment> ListForPlayer(string playerId);
}

public interface IPublisherRegistry
{
    IReadOnlyList<PublisherRegistration> List();

    PublisherRegistration? Get(string publisherId);

    PublisherRegistration Save(PublisherRegistration publisher);

    PublisherRegistration SetTitleStatus(string publisherId, string gameId, PublisherTitleStatus status);
}

public interface IPlatformOptInStore
{
    IReadOnlyList<PlatformOptInEntry> List(bool enabledOnly = true);

    PlatformOptInEntry? Find(string platform, string appId);

    void Save(PlatformOptInEntry entry);
}

public interface IPublisherBanStore
{
    IReadOnlyList<PublisherBanEntry> ListForGame(string gameId);

    bool IsBanned(string gameId, string platformUserId, DateTimeOffset nowUtc);

    void Ban(PublisherBanEntry entry);

    void Unban(string gameId, string platformUserId);
}

public interface IPublisherSessionMetricsStore
{
    void RecordJoin(string gameId, string? publisherId = null);

    int GetJoinCount(string publisherId);
}
