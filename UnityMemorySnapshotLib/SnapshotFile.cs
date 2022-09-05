using System.Runtime.InteropServices;
using UnityMemorySnapshotLib.Structures;
using UnityMemorySnapshotLib.Structures.LowLevel;
using UnityMemorySnapshotLib.Utils;

namespace UnityMemorySnapshotLib;

public class SnapshotFile : IDisposable
{
    private readonly MemoryMappedFileSpanHelper<byte> _file;
    private readonly Block[] _blocks;
    private readonly Dictionary<EntryType, Chapter> _chaptersByEntryType;

    public unsafe SnapshotFile(string path)
    {
        _file = new(path);

        //Check first and last word magics
        var magic = _file.As<uint>(..4);
        var endMagic = _file.As<uint>(^4..);

        if(magic != MagicNumbers.HeaderMagic || endMagic != MagicNumbers.FooterMagic)
            throw new($"Magic number mismatch. Expected {MagicNumbers.HeaderMagic} and {MagicNumbers.FooterMagic} but got {magic} and {endMagic}");

        var directoryMetadataOffset = (int) _file.As<ulong>(^12..); //8 bytes before end magic
        var directoryMetadata = _file.As<DirectoryMetadata>(directoryMetadataOffset..);

        if(directoryMetadata.Magic != MagicNumbers.DirectoryMagic)
            throw new($"Directory magic number mismatch. Expected {MagicNumbers.DirectoryMagic} but got {directoryMetadata.Magic}");

        if (directoryMetadata.Version != MagicNumbers.SupportedDirectoryVersion)
            throw new($"Directory version mismatch. Expected {MagicNumbers.SupportedDirectoryVersion} but got {directoryMetadata.Version}");

        var blockSection = _file.As<BlockSection>(directoryMetadata.BlocksOffset);

        if (blockSection.Version != MagicNumbers.SupportedBlockSectionVersion)
            throw new($"Block section version mismatch. Expected {MagicNumbers.SupportedBlockSectionVersion} but got {blockSection.Version}");

        if (blockSection.Count < 1)
        {
            _blocks = Array.Empty<Block>();
            _chaptersByEntryType = new();
            return;
        }
        
        var entryOffsetCount = directoryMetadata.EntriesCount;

        if (entryOffsetCount > (int) EntryType.Count)
        {
            Console.WriteLine($"Warning: Decreasing entry offset count from {entryOffsetCount} to {EntryType.Count} to match entry type count.");
            entryOffsetCount = (int)EntryType.Count;
        }

        var startOfEntryOffsets = directoryMetadataOffset + sizeof(DirectoryMetadata); //Start of entry offsets is right after directory metadata
        var endOfEntryOffsets = startOfEntryOffsets + entryOffsetCount * sizeof(long);
        var entryTypeToChapterOffset = _file.AsSpan<long>(startOfEntryOffsets..endOfEntryOffsets);
        
        var startOfDataBlockOffsets = (int) directoryMetadata.BlocksOffset + sizeof(BlockSection);
        var endOfDataBlockOffsets = startOfDataBlockOffsets + blockSection.Count * sizeof(long);
        var dataBlockOffsets = _file.AsSpan<long>(startOfDataBlockOffsets..endOfDataBlockOffsets);

        _blocks = new Block[dataBlockOffsets.Length];
        ReadBlocks(dataBlockOffsets);

        _chaptersByEntryType = new();
        ReadChapters(entryTypeToChapterOffset);
    }

    private unsafe void ReadBlocks(Span<long> dataBlockOffsets)
    {
        for (var i = 0; i < dataBlockOffsets.Length; i++)
        {
            var header = _file.As<BlockHeader>(dataBlockOffsets[i]);
            _blocks[i] = new(header, _file, (int)(dataBlockOffsets[i] + sizeof(BlockHeader)));
        }
    }

    private void ReadChapters(Span<long> entryTypeToChapterOffset)
    {
        for (var i = 0; i < entryTypeToChapterOffset.Length; i++)
        {
            if (entryTypeToChapterOffset[i] == 0)
                continue;

            _chaptersByEntryType[(EntryType)i] = ReadChapter((int)entryTypeToChapterOffset[i]);
        }
    }

    private unsafe Chapter ReadChapter(int chapterOffset)
    {
        var header = _file.As<ChapterHeader>(chapterOffset);
        var chapter = new Chapter(header)
        {
            Block = _blocks[header.BlockIndex]
        };

        if (header.Format == EntryFormat.DynamicSizeElementArray)
        {
            var dataStart = chapterOffset + sizeof(ChapterHeader);
            var dataEnd = dataStart + (int)header.Count * sizeof(long);

            chapter.AdditionalEntryStorage = _file.AsSpan<long>(dataStart..dataEnd).ToArray();
        }

        return chapter;
    }

    public T ReadChapterAsStruct<T>(EntryType entryType) where T : struct 
        => MemoryMarshal.Read<T>(ReadChapter(entryType));

    public byte[] ReadChapter(EntryType entryType) => ReadChapter(entryType, 0, 1);

    public byte[] ReadChapter(EntryType entryType, int startOffset, int count)
    {
        var entryData = _chaptersByEntryType[entryType];
        var block = entryData.Block;
        var size = (int) entryData.ComputeByteSizeForEntryRange(startOffset, count, false);

        switch (entryData.Header.Format)
        {
            case EntryFormat.SingleElement:
            {
                //header meta = offset into block
                var offsetIntoBlock = (uint)entryData.Header.HeaderMeta;
                return block.Read(offsetIntoBlock, size);
            }
            case EntryFormat.ConstantSizeElementArray:
            {
                //entries meta = size of element
                var offsetIntoBlock = (uint) (entryData.Header.EntriesMeta * startOffset);
                var endOffset = offsetIntoBlock + size;
                return block.Read(offsetIntoBlock, (int)(endOffset - offsetIntoBlock));
            }
            case EntryFormat.DynamicSizeElementArray:
            {
                throw new("Not implemented");
            }
            default:
                throw new("Unknown entry format.");
        }
    }

    public void Dispose()
    {
        _file.Dispose();
        GC.SuppressFinalize(this);
    }
}