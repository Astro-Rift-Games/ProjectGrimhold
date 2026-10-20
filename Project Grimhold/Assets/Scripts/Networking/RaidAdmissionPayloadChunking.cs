using System;

/// <summary>
/// Splits the raid admission payload (normally the backend-signed authoritative ticket) into
/// chunks that fit under the Fusion RPC payload limit (512 bytes). The ticket grows with the
/// reserved loadout, so a single RPC cannot carry it reliably.
/// </summary>
public static class RaidAdmissionPayloadChunking
{
    /// <summary>Chunk body size; leaves headroom under Fusion's 512-byte RPC limit for headers.</summary>
    public const int MaxChunkBytes = 384;

    /// <summary>Upper bound for a reassembled admission payload accepted by State Authority.</summary>
    public const int MaxPayloadBytes = 8192;

    public const int MaxChunkCount = (MaxPayloadBytes + MaxChunkBytes - 1) / MaxChunkBytes;

    public static bool TrySplit(byte[] payload, out byte[][] chunks)
    {
        chunks = null;
        if (payload == null || payload.Length == 0 || payload.Length > MaxPayloadBytes)
        {
            return false;
        }

        int count = (payload.Length + MaxChunkBytes - 1) / MaxChunkBytes;
        chunks = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            int offset = index * MaxChunkBytes;
            int length = Math.Min(MaxChunkBytes, payload.Length - offset);
            chunks[index] = new byte[length];
            Buffer.BlockCopy(payload, offset, chunks[index], 0, length);
        }

        return true;
    }
}

public enum RaidAdmissionPayloadAssemblyResult
{
    Incomplete,
    Completed,
    Rejected
}

/// <summary>
/// Reassembles one sender's admission payload on State Authority. Chunks may arrive in any
/// order; any malformed, inconsistent or duplicated chunk rejects the whole submission.
/// </summary>
public sealed class RaidAdmissionPayloadAssembler
{
    private byte[][] _chunks;
    private int _received;

    public RaidAdmissionPayloadAssemblyResult Accept(
        int chunkIndex,
        int chunkCount,
        byte[] chunk,
        out byte[] payload)
    {
        payload = null;
        if (chunkCount <= 0 || chunkCount > RaidAdmissionPayloadChunking.MaxChunkCount ||
            chunkIndex < 0 || chunkIndex >= chunkCount ||
            chunk == null || chunk.Length == 0 || chunk.Length > RaidAdmissionPayloadChunking.MaxChunkBytes)
        {
            return RaidAdmissionPayloadAssemblyResult.Rejected;
        }

        if (_chunks == null)
        {
            _chunks = new byte[chunkCount][];
        }
        else if (_chunks.Length != chunkCount || _chunks[chunkIndex] != null)
        {
            return RaidAdmissionPayloadAssemblyResult.Rejected;
        }

        _chunks[chunkIndex] = chunk;
        _received++;
        if (_received < chunkCount)
        {
            return RaidAdmissionPayloadAssemblyResult.Incomplete;
        }

        int totalLength = 0;
        for (int index = 0; index < _chunks.Length; index++)
        {
            totalLength += _chunks[index].Length;
        }

        if (totalLength > RaidAdmissionPayloadChunking.MaxPayloadBytes)
        {
            return RaidAdmissionPayloadAssemblyResult.Rejected;
        }

        payload = new byte[totalLength];
        int offset = 0;
        for (int index = 0; index < _chunks.Length; index++)
        {
            Buffer.BlockCopy(_chunks[index], 0, payload, offset, _chunks[index].Length);
            offset += _chunks[index].Length;
        }

        return RaidAdmissionPayloadAssemblyResult.Completed;
    }
}
