using UMS.LowLevel.Utils;

namespace UMS.LowLevel.Structures.FileStructure;

public struct BlockHeader
{
    //Header
    public ulong ChunkSize;
    public ulong TotalBytes;

    public uint OffsetCount => (uint)(TotalBytes / ChunkSize) + (TotalBytes % ChunkSize == 0 ? 0u : 1u);
    
    public Span<long> GetOffsets(MemoryMappedFileSpanHelper<byte> file, long start) 
        => file.AsSpan<long>(start, (int) OffsetCount * sizeof(long));
}