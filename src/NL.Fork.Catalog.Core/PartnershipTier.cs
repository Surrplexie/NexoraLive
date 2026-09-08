namespace NL.Fork.Catalog.Core;

/// <summary>How a fork snapshot is partnered — surfaced in UI and legal copy (Phase N).</summary>
public enum PartnershipTier
{
    /// <summary>Publisher SDK / menu integration — fully endorsed.</summary>
    Official,

    /// <summary>Platform-wide opt-in (e.g. Steam app flag).</summary>
    Platform,

    /// <summary>Fork without publisher blessing — user acknowledgment required.</summary>
    AtOwnRisk,
}

public enum ForkCatalogEntryStatus
{
    Active,
    Deprecated,
}

public static class PartnershipTierLabels
{
    public static string DisplayName(PartnershipTier tier) => tier switch
    {
        PartnershipTier.Official => "Official",
        PartnershipTier.Platform => "Platform",
        PartnershipTier.AtOwnRisk => "At own risk",
        _ => tier.ToString(),
    };

    public static string LegalNotice(PartnershipTier tier) => tier switch
    {
        PartnershipTier.Official => "Publisher-approved NL session. Progress on this fork does not sync to publisher servers.",
        PartnershipTier.Platform => "Platform-opted title. Session data is ephemeral on NL infrastructure.",
        PartnershipTier.AtOwnRisk => "Not endorsed by the publisher. No progress transfer. Play at your own risk.",
        _ => "",
    };
}
