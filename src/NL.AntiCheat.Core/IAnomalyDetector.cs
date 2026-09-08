using NL.Server.Core;

namespace NL.AntiCheat.Core;

/// <summary>Observes a stream of <see cref="SessionEvent"/>s and optionally emits
/// <see cref="AnomalySignal"/>s. Detectors are stateful (per-player history) but side-effect
/// free beyond their own memory — mirroring Phase 0's deterministic-evaluator spirit.</summary>
public interface IAnomalyDetector
{
    IReadOnlyList<AnomalySignal> Observe(SessionEvent sessionEvent, DateTimeOffset nowUtc);
}
