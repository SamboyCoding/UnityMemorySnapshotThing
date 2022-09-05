using UnityMemorySnapshotLib.Utils;

namespace UnityMemorySnapshotLib.Structures.LowLevel;

public struct BlockHeader
{
    //Header
    public ulong ChunkSize;
    public ulong TotalBytes;

    public uint OffsetCount => (uint)(TotalBytes / ChunkSize) + (TotalBytes % ChunkSize == 0 ? 0u : 1u);
    
    public Span<long> GetOffsets(MemoryMappedFileSpanHelper<byte> file, int start) 
        => file.AsSpan<long>(start..(start + (int) OffsetCount * sizeof(long)));
}