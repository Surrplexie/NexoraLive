using System.Text;

namespace NL.Server.Core;

/// <summary>
/// A single Source/Valve RCON protocol packet, used to send real commands to (and read
/// responses from) a Minecraft server's RCON port — the "action" half of Phase 3's NLServer
/// integration (kick/ban/tell in response to a `RuleEngine` decision). This class only
/// encodes/decodes the wire format; <c>NL.Server</c>'s socket client does the actual I/O, kept
/// separate so the framing logic here is fully unit-testable without any network access.
///
/// Wire format (little-endian): int32 Size | int32 RequestId | int32 Type | body (UTF-8,
/// null-terminated) | 1 extra null byte. <c>Size</c> covers everything after itself.
/// </summary>
public sealed record RconPacket(int RequestId, RconPacketType Type, string Body)
{
    /// <summary>Encodes this packet to its full wire representation, including the leading
    /// size field.</summary>
    public byte[] Encode()
    {
        var bodyBytes = Encoding.UTF8.GetBytes(Body);
        var size = 4 + 4 + bodyBytes.Length + 1 + 1; // RequestId + Type + body + 2 null terminators
        var buffer = new byte[4 + size];

        BitConverter.TryWriteBytes(buffer.AsSpan(0, 4), size);
        BitConverter.TryWriteBytes(buffer.AsSpan(4, 4), RequestId);
        BitConverter.TryWriteBytes(buffer.AsSpan(8, 4), (int)Type);
        bodyBytes.CopyTo(buffer, 12);
        // buffer[12 + bodyBytes.Length] and the final byte are already 0 (array default).

        return buffer;
    }

    /// <summary>
    /// Attempts to decode exactly one packet starting at the beginning of <paramref
    /// name="buffer"/>. Returns false if <paramref name="buffer"/> doesn't yet contain a full
    /// packet (the caller should buffer more bytes from the socket and retry) — this makes it
    /// safe to use against a streaming <c>NetworkStream</c> where reads can be split
    /// arbitrarily.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> buffer, out RconPacket? packet, out int consumed)
    {
        packet = null;
        consumed = 0;

        if (buffer.Length < 4)
        {
            return false;
        }

        var size = BitConverter.ToInt32(buffer);
        if (size < 10 || buffer.Length < 4 + size)
        {
            // size < 10 would mean no room for RequestId+Type+2 null terminators — malformed.
            return false;
        }

        var requestId = BitConverter.ToInt32(buffer.Slice(4, 4));
        var type = (RconPacketType)BitConverter.ToInt32(buffer.Slice(8, 4));
        var bodyLength = size - 4 - 4 - 2;
        var body = bodyLength > 0
            ? Encoding.UTF8.GetString(buffer.Slice(12, bodyLength))
            : "";

        packet = new RconPacket(requestId, type, body);
        consumed = 4 + size;
        return true;
    }
}
