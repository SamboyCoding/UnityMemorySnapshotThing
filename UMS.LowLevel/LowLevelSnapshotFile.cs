using System.Runtime.InteropServices;
using System.Text;
using UMS.LowLevel.Structures;
using UMS.LowLevel.Structures.FileStructure;
using UMS.LowLevel.Utils;

namespace UMS.LowLevel;

public class LowLevelSnapshotFile : IDisposable
{
    private readonly MemoryMappedFileSpanHelper<byte> _file;
    private readonly Block[] _blocks;
    private readonly Dictionary<EntryType, Chapter> _chaptersByEntryType;

    public ProfileTargetInfo ProfileTargetInfo => ReadChapterAsStruct<ProfileTargetInfo>(EntryType.ProfileTarget_Info);
    public ProfileTargetMemoryStats ProfileTargetMemoryStats => ReadChapterAsStruct<ProfileTargetMemoryStats>(EntryType.ProfileTarget_MemoryStats);
    public VirtualMachineInformation VirtualMachineInformation => ReadChapterAsStruct<VirtualMachineInformation>(EntryType.Metadata_VirtualMachineInformation);
    public FormatVersion SnapshotFormatVersion => ReadChapterAsStruct<FormatVersion>(EntryType.Metadata_Version);
    public CaptureFlags CaptureFlags => ReadChapterAsStruct<CaptureFlags>(EntryType.Metadata_CaptureFlags);
    public byte[] UserMetadata => ReadChapterBody(EntryType.Metadata_UserMetadata);
    public DateTime CaptureDateTime => new(ReadChapterAsStruct<long>(EntryType.Metadata_RecordDate));
    public string[] NativeTypeNames => ReadStringArrayChapter(EntryType.NativeTypes_Name);
    public string[] NativeObjectNames => ReadStringArrayChapter(EntryType.NativeObjects_Name);
    public Span<long> ManagedHeapSectionStartAddresses => ReadValueTypeChapter<long>(EntryType.ManagedHeapSections_StartAddress, 0, -1);
    public byte[][] ManagedHeapSectionBytes => ReadValueTypeArrayChapter<byte>(EntryType.ManagedHeapSections_Bytes, 0, -1);
    public string[] TypeDescriptionNames => ReadStringArrayChapter(EntryType.TypeDescriptions_Name);
    public string[] TypeDescriptionAssemblies => ReadStringArrayChapter(EntryType.TypeDescriptions_Assembly);
    public int[][] TypeDescriptionFieldIndices => ReadValueTypeArrayChapter<int>(EntryType.TypeDescriptions_FieldIndices, 0, -1);
    public byte[][] TypeDescriptionStaticFieldBytes => ReadValueTypeArrayChapter<byte>(EntryType.TypeDescriptions_StaticFieldBytes, 0, -1);
    public string[] FieldDescriptionNames => ReadStringArrayChapter(EntryType.FieldDescriptions_Name);
    public Span<int> FieldDescriptionTypeIndices => ReadValueTypeChapter<int>(EntryType.FieldDescriptions_TypeIndex, 0, -1);
    public Span<int> FieldDescriptionOffsets => ReadValueTypeChapter<int>(EntryType.FieldDescriptions_Offset, 0, -1);
    public Span<ulong> GcHandles => ReadValueTypeChapter<ulong>(EntryType.GCHandles_Target, 0, -1);

    public unsafe LowLevelSnapshotFile(string path)
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
        ReadAllBlocks(dataBlockOffsets);

        _chaptersByEntryType = new();
        ReadMetadataForAllChapters(entryTypeToChapterOffset);
    }

    private unsafe void ReadAllBlocks(Span<long> dataBlockOffsets)
    {
        for (var i = 0; i < dataBlockOffsets.Length; i++)
        {
            var header = _file.As<BlockHeader>(dataBlockOffsets[i]);
            _blocks[i] = new(header, _file, (int)(dataBlockOffsets[i] + sizeof(BlockHeader)));
        }
    }

    private void ReadMetadataForAllChapters(Span<long> entryTypeToChapterOffset)
    {
        for (var i = 0; i < entryTypeToChapterOffset.Length; i++)
        {
            if (entryTypeToChapterOffset[i] == 0)
                continue;

            _chaptersByEntryType[(EntryType)i] = ReadChapterMetadata((int)entryTypeToChapterOffset[i]);

            if (_chaptersByEntryType[(EntryType)i].AdditionalEntryStorage != null)
            {
                Console.WriteLine($"Read additional entry storage for chapter {(EntryType) i}");
            }
        }
    }

    private unsafe Chapter ReadChapterMetadata(int chapterOffset)
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
        => MemoryMarshal.Read<T>(ReadChapterBody(entryType));

    public byte[] ReadChapterBody(EntryType entryType) => ReadChapterBody(entryType, 0, 1);

    public byte[] ReadChapterBody(EntryType entryType, int startOffset, int count)
    {
        var chapter = _chaptersByEntryType[entryType];
        var block = chapter.Block;
        var size = (int) chapter.ComputeByteSizeForEntryRange(startOffset, count, false);

        switch (chapter.Header.Format)
        {
            case EntryFormat.SingleElement:
            {
                //header meta = offset into block
                var offsetIntoBlock = (uint)chapter.Header.HeaderMeta;
                return block.Read(offsetIntoBlock, size);
            }
            case EntryFormat.ConstantSizeElementArray:
            {
                //entries meta = size of element
                var offsetIntoBlock = (uint) (chapter.Header.EntriesMeta * startOffset);
                var endOffset = offsetIntoBlock + size;
                return block.Read(offsetIntoBlock, (int)(endOffset - offsetIntoBlock));
            }
            case EntryFormat.DynamicSizeElementArray:
            {
                //We just return the raw data here, and interpret it later
                
                //AdditionalEntryStorage = offset of end of each element (exclusive)
                var offsetIntoBlock = startOffset == 0 ? 0u : (uint)chapter.AdditionalEntryStorage![startOffset - 1];
                return block.Read(offsetIntoBlock, size);
            }
            default:
                throw new("Unknown entry format.");
        }
    }

    public string[] ReadStringArrayChapter(EntryType entryType) => ReadStringArrayChapter(entryType, 0, -1);
    public string[] ReadStringArrayChapter(EntryType entryType, int startOffset, int count)
    {
        var chapter = _chaptersByEntryType[entryType];

        if (count == -1)
            count = (int)chapter.Count;
        
        var rawData = ReadChapterBody(entryType, startOffset, count);

        //AdditionalEntryStorage = offset of end of each element (exclusive)
        var offsets = chapter.AdditionalEntryStorage!;

        var ret = new string[count];
        if(startOffset == 0)
            ret[0] = Encoding.UTF8.GetString(rawData, 0, (int)offsets[0]);
        else
            ret[0] = Encoding.UTF8.GetString(rawData, (int)offsets[startOffset - 1], (int)(offsets[startOffset] - offsets[startOffset - 1]));
        
        for(var i = 1; i < count; i++)
            ret[i] = Encoding.UTF8.GetString(rawData, (int)offsets[startOffset + i - 1], (int)(offsets[startOffset + i] - offsets[startOffset + i - 1]));

        return ret;
    }

    public Span<T> ReadSingleValueTypeArrayChapterElement<T>(EntryType entryType, int offset) where T : unmanaged
    {
        var chapter = _chaptersByEntryType[entryType];
        
        var rawData = ReadChapterBody(entryType, offset, 1); //Future - Consider trying to read this as a span not a byte array
        
        return MemoryMarshal.Cast<byte, T>(rawData);
    }

    public T[][] ReadValueTypeArrayChapter<T>(EntryType entryType, int startOffset, int count) where T : unmanaged
    {
        var chapter = _chaptersByEntryType[entryType];

        if (count == -1)
            count = (int)chapter.Count;
        
        var rawData = ReadChapterBody(entryType, startOffset, count);
        
        var ret = new T[count][];
        
        //AdditionalEntryStorage = offset of end of each element (exclusive)
        var offsets = chapter.AdditionalEntryStorage!;
        
        for(var i = 0; i < count; i++)
        {
            var offset = startOffset + i;
            var firstElemPos = offset == 0 ? 0 : (int)offsets[offset - 1];
            var numElements = (int)(offsets[offset] - firstElemPos);
            ret[i] = MemoryMarshal.Cast<byte, T>(rawData.AsSpan(firstElemPos, numElements)).ToArray();
        }

        return ret;
    }
    
    public Span<T> ReadValueTypeChapter<T>(EntryType entryType, int startOffset, int count) where T : unmanaged
    {
        var chapter = _chaptersByEntryType[entryType];

        if (count == -1)
            count = (int)chapter.Count;
        
        var rawData = ReadChapterBody(entryType, startOffset, count);

        return MemoryMarshal.Cast<byte, T>(rawData);
    }

    public void Dispose()
    {
        _file.Dispose();
        GC.SuppressFinalize(this);
    }
}