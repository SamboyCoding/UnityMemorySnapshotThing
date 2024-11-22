using UMS.LowLevel.Utils;

namespace UMS.LowLevel.Structures.FileStructure;

public struct Block
{
    private readonly MemoryMappedFileSpanHelper<byte> _file;
    
    public BlockHeader Header { get; }
    public long[] Offsets { get; }
    
    public MergedRange[] MergedRanges { get; }
    
    public long StartOffset => Offsets[0];

    public Block(BlockHeader header, MemoryMappedFileSpanHelper<byte> file, long start)
    {
        _file = file;
        Header = header;
        Offsets = header.GetOffsets(file, start).ToArray();

        MergedRanges = Array.Empty<MergedRange>(); //Preemptively set this so we can call RangeFromOffset
        if (Offsets.Length == 0)
            return;

        if (Offsets.Length == 1)
        {
            MergedRanges = new[] { RangeFromOffset(0, Offsets[0]) };
            return;
        }

        //Merge offsets into contiguous ranges, allows zero-alloc reading where possible
        MergedRanges = MergeRanges().ToArray();
    }

    private List<MergedRange> MergeRanges()
    {
        var ranges = new List<MergedRange>();
        var currOffset = RangeFromOffset(0, Offsets[0]);
        var index = 1;
        var blockOffset = (long) Header.ChunkSize;

        while (index < Offsets.Length)
        {
            var nextOffset = RangeFromOffset(blockOffset, Offsets[index]);
            if (currOffset.FileEnd == nextOffset.FileStart)
            {
                //Merge
                currOffset.Length += nextOffset.Length;
                blockOffset += nextOffset.Length;
            }
            else
            {
                ranges.Add(currOffset);
                currOffset = nextOffset;
                blockOffset = nextOffset.BlockEnd;
            }

            index++;
        }

        ranges.Add(currOffset); //Add the last range
        return ranges;
    }

    private MergedRange RangeFromOffset(long blockOffset, long fileOffset)
    {
        var start = fileOffset;
        
        return new(blockOffset, start, (int)Header.ChunkSize);
    }

    public bool TryReadAsSpan(long index, int length, out Span<byte> result)
    {
        result = default;

        var relevantBlock = BinarySearchForRelevantChunk(MergedRanges, index);
        if (relevantBlock == -1)
            //No block found containing index
            return false;

        var range = MergedRanges[relevantBlock];

        var offset = index - range.BlockStart;
        var remaining = range.Length - offset;
        if (remaining < length)
            return false;

        result = _file.AsSpan<byte>(range.FileStart + offset, length);
        return true;
    }

    public byte[] Read(long index, long length)
    {
        var startChunk = index / (long) Header.ChunkSize;
        var offsetIntoFirstChunk = index % (long) Header.ChunkSize;
        var bytesLeftInChunk = (long) Header.ChunkSize - offsetIntoFirstChunk;

        if (length > bytesLeftInChunk)
        {
            // We need to read from multiple chunks
            return ReadMultipleChunks(startChunk, offsetIntoFirstChunk, length);
        }
        
        var chunk = Offsets[startChunk];
        var offset = chunk + offsetIntoFirstChunk;
        
        return _file.AsSpan<byte>(offset, (int)length).ToArray();
    }

    private byte[] ReadMultipleChunks(long startChunk, long offsetIntoFirstChunk, long length)
    {
        var ret = new byte[length];
        var bytesLeft = length;
        
        var offset = 0;
        var currChunk = startChunk;
        var chunkOffset = offsetIntoFirstChunk;

        while (bytesLeft > 0)
        {
            var chunkStart = Offsets[currChunk];
            var bytesToRead = Math.Min((long) Header.ChunkSize - chunkOffset, bytesLeft);
            var chunk = _file.AsSpan<byte>(chunkStart + chunkOffset, (int)bytesToRead);
            chunk.CopyTo(ret.AsSpan(offset));
            offset += (int) bytesToRead;
            bytesLeft -= bytesToRead;
            chunkOffset = 0;
            currChunk++;
        }

        return ret;
    }
    
    private static int BinarySearchForRelevantChunk(MergedRange[] ranges, long offset)
    {
        var first = 0;
        var last = ranges.Length - 1;
        do
        {
            var mid = first + (last - first) / 2;

            var range = ranges[mid];
            if(offset < range.BlockEnd && offset >= range.BlockStart)
                return mid;
            
            if (offset >= range.BlockEnd)
                first = mid + 1;
            else
                last = mid - 1;
        } while (first <= last);       
        return -1;
    }

    public struct MergedRange
    {
        public long BlockStart;
        public long FileStart;
        public int Length;
        public long BlockEnd;
        public long FileEnd;
        
        public MergedRange(long blockStart, long fileStart, int length)
        {
            BlockStart = blockStart;
            FileStart = fileStart;
            Length = length;
            BlockEnd = blockStart + length;
            FileEnd = fileStart + length;
        }
    }
}