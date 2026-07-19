namespace NL.Core.Sp;

/// <summary>
/// Identity/account verification signals a streamer's join requirements can demand (nl.txt
/// section 2: "the NL account must be verified (by or all; email, phone number, 2FA, ID
/// verification, and many more)"). Flags so a profile can hold any combination.
/// </summary>
[Flags]
public enum SpVerification
{
    None = 0,
    Email = 1 << 0,
    Phone = 1 << 1,
    TwoFactor = 1 << 2,
    Id = 1 << 3,
}
