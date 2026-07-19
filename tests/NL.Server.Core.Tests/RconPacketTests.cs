using NL.Server.Core;
using Xunit;

namespace NL.Server.Core.Tests;

public class RconPacketTests
{
    [Fact]
    public void Encode_ThenTryDecode_RoundTripsExactly()
    {
        var original = new RconPacket(42, RconPacketType.ExecCommand, "say hello world");

        var encoded = original.Encode();
        var decoded = RconPacket.TryDecode(encoded, out var packet, out var consumed);

        Assert.True(decoded);
        Assert.Equal(original.RequestId, packet!.RequestId);
        Assert.Equal(original.Type, packet.Type);
        Assert.Equal(original.Body, packet.Body);
        Assert.Equal(encoded.Length, consumed);
    }

    [Fact]
    public void Encode_EmptyBody_RoundTrips()
    {
        var original = new RconPacket(1, RconPacketType.ResponseValue, "");

        var encoded = original.Encode();
        RconPacket.TryDecode(encoded, out var packet, out _);

        Assert.Equal("", packet!.Body);
    }

    [Fact]
    public void Encode_SizeFieldMatchesProtocolFormula()
    {
        // size = 4 (RequestId) + 4 (Type) + body bytes + 2 null terminators
        var packet = new RconPacket(1, RconPacketType.Auth, "secret");
        var encoded = packet.Encode();

        var declaredSize = BitConverter.ToInt32(encoded, 0);
        Assert.Equal(4 + 4 + "secret".Length + 1 + 1, declaredSize);
        Assert.Equal(4 + declaredSize, encoded.Length);
    }

    [Fact]
    public void TryDecode_AuthFailureRequestId_IsPreserved()
    {
        // Failed SERVERDATA_AUTH_RESPONSE comes back with RequestId == -1 per the real protocol.
        var failure = new RconPacket(-1, RconPacketType.AuthResponse, "");
        var encoded = failure.Encode();

        RconPacket.TryDecode(encoded, out var packet, out _);

        Assert.Equal(-1, packet!.RequestId);
    }

    [Fact]
    public void TryDecode_IncompleteBuffer_ReturnsFalse()
    {
        var full = new RconPacket(1, RconPacketType.ExecCommand, "kick Steve you broke a rule").Encode();
        var truncated = full[..(full.Length - 5)];

        var decoded = RconPacket.TryDecode(truncated, out var packet, out var consumed);

        Assert.False(decoded);
        Assert.Null(packet);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void TryDecode_FewerThanFourBytes_ReturnsFalse()
    {
        var decoded = RconPacket.TryDecode(new byte[] { 1, 2 }, out var packet, out _);

        Assert.False(decoded);
        Assert.Null(packet);
    }

    [Fact]
    public void TryDecode_ConsumesOnlyItsOwnPacket_LeavingTrailingBytesForNextRead()
    {
        var first = new RconPacket(1, RconPacketType.ExecCommand, "cmd1").Encode();
        var second = new RconPacket(2, RconPacketType.ExecCommand, "cmd2").Encode();
        var combined = first.Concat(second).ToArray();

        var decodedFirst = RconPacket.TryDecode(combined, out var firstPacket, out var firstConsumed);
        Assert.True(decodedFirst);
        Assert.Equal("cmd1", firstPacket!.Body);
        Assert.Equal(first.Length, firstConsumed);

        var decodedSecond = RconPacket.TryDecode(combined.AsSpan(firstConsumed), out var secondPacket, out _);
        Assert.True(decodedSecond);
        Assert.Equal("cmd2", secondPacket!.Body);
    }

    [Fact]
    public void Encode_UnicodeBody_RoundTripsAsUtf8()
    {
        var original = new RconPacket(1, RconPacketType.ExecCommand, "tell Steve düm gëïst");

        var encoded = original.Encode();
        RconPacket.TryDecode(encoded, out var packet, out _);

        Assert.Equal(original.Body, packet!.Body);
    }
}
