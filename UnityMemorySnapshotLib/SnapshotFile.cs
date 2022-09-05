﻿using System.Runtime.InteropServices;
using System.Text;
using UnityMemorySnapshotLib.Structures;
using UnityMemorySnapshotLib.Structures.LowLevel;
using UnityMemorySnapshotLib.Utils;

namespace UnityMemorySnapshotLib;

public class SnapshotFile : IDisposable
{
    private readonly MemoryMappedFileSpanHelper<byte> _file;
    private readonly Block[] _blocks;
    private readonly Dictionary<EntryType, Chapter> _chaptersByEntryType;

    public ProfileTargetInfo ProfileTargetInfo => ReadChapterAsStruct<ProfileTargetInfo>(EntryType.ProfileTarget_Info);
    public ProfileTargetMemoryStats ProfileTargetMemoryStats => ReadChapterAsStruct<ProfileTargetMemoryStats>(EntryType.ProfileTarget_MemoryStats);
    public VirtualMachineInformation VirtualMachineInformation => ReadChapterAsStruct<VirtualMachineInformation>(EntryType.Metadata_VirtualMachineInformation);
    public FormatVersion SnapshotFormatVersion => ReadChapterAsStruct<FormatVersion>(EntryType.Metadata_Version);
    public CaptureFlags CaptureFlags => ReadChapterAsStruct<CaptureFlags>(EntryType.Metadata_CaptureFlags);
    public byte[] UserMetadata => ReadChapter(EntryType.Metadata_UserMetadata);
    public DateTime CaptureDateTime => new(ReadChapterAsStruct<long>(EntryType.Metadata_RecordDate));
    public string[] NativeTypeNames => ReadStringArrayChapter(EntryType.NativeTypes_Name);
    public string[] NativeObjectNames => ReadStringArrayChapter(EntryType.NativeObjects_Name);
    //Another dynamic array: ManagedHeapSections_Bytes
    public string[] TypeDescriptionNames => ReadStringArrayChapter(EntryType.TypeDescriptions_Name);
    public string[] TypeDescriptionAssemblies => ReadStringArrayChapter(EntryType.TypeDescriptions_Assembly);
    //TypeDescriptions_FieldIndices and TypeDescriptions_StaticFieldBytes
    public string[] FieldDescriptionNames => ReadStringArrayChapter(EntryType.FieldDescriptions_Name);

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

            if (_chaptersByEntryType[(EntryType)i].AdditionalEntryStorage != null)
            {
                Console.WriteLine($"Read additional entry storage for chapter {(EntryType) i}");
            }
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
                return block.Read(0, size);
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
        
        var rawData = ReadChapter(entryType, startOffset, count);

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

    public void Dispose()
    {
        _file.Dispose();
        GC.SuppressFinalize(this);
    }
}