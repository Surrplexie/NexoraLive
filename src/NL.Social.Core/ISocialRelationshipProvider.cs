namespace NL.Social.Core;

public interface ISocialRelationshipProvider
{
    Task<SocialRelationshipStatus> GetStatusAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default);
}
