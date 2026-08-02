namespace NL.Fleet.Core;

public interface IFleetMetricsStore
{
    void RecordAdmit(bool allowed, string? streamerId = null);

    void RecordForkCreate(string streamerId, string regionId);

    void RecordDecision(int count = 1);

    void RecordSessionSample(FleetSessionMetricSample sample);

    FleetObservabilitySnapshot BuildSnapshot(int activeForks, int activeNls, int recentSessionLimit = 20);

    int GetForkCreatesInLastMinute();

    int GetForkCreatesForStreamerInLastHour(string streamerId);
}

public interface IFleetIncidentStore
{
    IReadOnlyList<FleetIncident> ListRecent(int count = 50);

    void Add(FleetIncident incident);
}

public interface IFleetStreamerRequirementsStore
{
    FleetStreamerRequirements GetOrDefault(string streamerId);

    void Save(FleetStreamerRequirements requirements);
}
