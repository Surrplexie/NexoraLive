using NL.Core.Sp;
using NL.SpSimulator;

Console.WriteLine("NL SP Simulator - StreamPlayer join-eligibility prototype (Phase 2)");
Console.WriteLine();

var now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

foreach (var attempt in MockSpProfileSource.DefaultScript())
{
    var result = JoinEligibilityEngine.Evaluate(attempt.Profile, attempt.StreamerId, attempt.Requirements, now);

    Console.WriteLine($"- {attempt.Description}");
    Console.WriteLine($"    decision: {result}");
    Console.WriteLine();
}

return 0;
