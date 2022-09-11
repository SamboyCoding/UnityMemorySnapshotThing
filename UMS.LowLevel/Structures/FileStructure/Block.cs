using UMS.LowLevel.Utils;

namespace UMS.LowLevel.Structures.FileStructure;

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
        var offsetIntoFirstChunk = (uint) (index % Header.ChunkSize);
        var bytesLeftInChunk = (uint) Header.ChunkSize - offsetIntoFirstChunk;

        if (length > bytesLeftInChunk)
        {
            // We need to read from multiple chunks
            return ReadMultipleChunks(startChunk, offsetIntoFirstChunk, length);
        }
        
        var chunk = Offsets[startChunk];
        var offset = (int) (chunk + offsetIntoFirstChunk);
        
        return _file.Span[offset..(offset + length)].ToArray();
    }

    private byte[] ReadMultipleChunks(uint startChunk, uint offsetIntoFirstChunk, int length)
    {
        var ret = new byte[length];
        var bytesLeft = length;
        
        var offset = 0;
        var currChunk = startChunk;
        var chunkOffset = offsetIntoFirstChunk;

        while (bytesLeft > 0)
        {
            var chunkStart = Offsets[currChunk];
            var bytesToRead = Math.Min((int) Header.ChunkSize - (int) chunkOffset, bytesLeft);
            var chunk = _file.Span[(int) (chunkStart + chunkOffset)..(int) (chunkStart + chunkOffset + bytesToRead)];
            chunk.CopyTo(ret.AsSpan(offset));
            offset += bytesToRead;
            bytesLeft -= bytesToRead;
            chunkOffset = 0;
            currChunk++;
        }

        return ret;
    }
}