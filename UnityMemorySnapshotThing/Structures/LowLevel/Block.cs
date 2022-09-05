using UnityMemorySnapshotThing.Utils;

namespace UnityMemorySnapshotThing.Structures.LowLevel;

public struct Block
{
    private MemoryMappedFileSpanHelper<byte> _file;
    
    public BlockHeader Header { get; }
    public long[] Offsets { get; }

    public Block(BlockHeader header, MemoryMappedFileSpanHelper<byte> file, int start)
    {
        _file = file;
        Header = header;
        Offsets = header.GetOffsets(file, start).ToArray();
    }

    public byte[] Read(uint index, int length)
    {
        var startChunk = (uint) (index / Header.ChunkSize);
        var startOffset = (uint) (index % Header.ChunkSize);
        var bytesLeftInChunk = (uint) Header.ChunkSize - startOffset;
        
        if(length > bytesLeftInChunk)
            throw new("Cross-chunk reading not implemented");
        
        var chunk = Offsets[startChunk];
        var offset = (int) (chunk + startOffset);
        
        return _file.Span[offset..(offset + length)].ToArray();
    }
}