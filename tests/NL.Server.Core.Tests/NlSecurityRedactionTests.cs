using NL.Server.Core.Integration;
using NL.Server.Core.Security;
using Xunit;

namespace NL.Server.Core.Tests;

public class NlSecurityRedactionTests
{
    [Fact]
    public void RedactManifest_HidesBridgeToken()
    {
        var manifest = new NlSessionManifest
        {
            SessionId = "sess",
            StreamerId = "default-streamer",
            HttpBaseUrl = "http://localhost:27020",
            BridgeConnectUrl = "ws://localhost:27021/nl/v1?token=secret-token",
            AdmitUrl = "http://localhost:27020/api/v1/session/admit",
            ManifestUrl = "http://localhost:27020/api/v1/session/manifest",
            ModerationUrl = "http://localhost:27020/moderation.html",
        };

        var redacted = NlSecurityRedaction.RedactManifest(manifest, includeSecrets: false);
        Assert.DoesNotContain("secret-token", redacted.BridgeConnectUrl);
        Assert.Contains(NlSecurityRedaction.RedactedTokenPlaceholder, redacted.BridgeConnectUrl);
    }
}
