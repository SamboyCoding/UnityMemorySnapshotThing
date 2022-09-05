using System.Runtime.InteropServices;
using UnityMemorySnapshotThing.Structures;
using UnityMemorySnapshotThing.Structures.LowLevel;
using UnityMemorySnapshotThing.Utils;

namespace UnityMemorySnapshotThing;

public static class Program
{
    private static Dictionary<EntryType, Entry> _entries;

    public static unsafe void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No file specified");
            return;
        }
        
        var filePath = args[0];

        // var start = DateTime.Now;
        // Console.WriteLine("Reading file and magics...");
        using var file = new MemoryMappedFileSpanHelper<byte>(filePath);

        //Check first and last word magics
        var magic = file.As<uint>(..4);
        var endMagic = file.As<uint>(^4..);
        
        // Console.WriteLine($"Read in {DateTime.Now - start:c}");
        
        // Console.WriteLine($"Magic: {magic:X8}");
        // Console.WriteLine($"End Magic: {endMagic:X8}");

        if(magic != MagicNumbers.HeaderMagic || endMagic != MagicNumbers.FooterMagic)
        {
            Console.WriteLine("Invalid file");
            return;
        }

        var firstChapterOffset = (int) file.As<ulong>(^12..); //8 bytes before end magic
        
        // Console.WriteLine($"First chapter offset: {firstChapterOffset:X8}");
        
        var directoryMetadata = file.As<DirectoryMetadata>(firstChapterOffset..);
        
        // Console.WriteLine($"Directory metadata: {directoryMetadata}");
        
        if(directoryMetadata.Magic != MagicNumbers.DirectoryMagic)
        {
            Console.WriteLine("Invalid directory magic.");
            return;
        }

        if (directoryMetadata.Version != MagicNumbers.SupportedDirectoryVersion)
        {
            Console.WriteLine("Unsupported directory version.");
            return;
        }

        var entriesOffset = firstChapterOffset + sizeof(DirectoryMetadata);

        var blockSection = file.As<BlockSection>(directoryMetadata.BlocksOffset);

        if (blockSection.Version != MagicNumbers.SupportedBlockSectionVersion)
        {
            Console.WriteLine("Unsupported block section version.");
            return;
        }

        if (blockSection.Count < 1)
        {
            Console.WriteLine("No blocks found.");
            return;
        }
        
        //Unity then returns the entriesOffset and the blocksOffset (+ 4 bytes for the version)
        var entryOffsetCount = file.As<int>(entriesOffset);

        if (entryOffsetCount > (int) EntryType.Count)
        {
            Console.WriteLine($"Decreasing entry offset count from {entryOffsetCount} to {EntryType.Count} to match entry type count.");
        }

        // var entryTypeToChapterOffset = new long[entryOffsetCount];
        // var dataBlockOffsets = new long[blockSection.Count];

        var startOfEntryOffsets = entriesOffset + sizeof(int);
        var endOfEntryOffsets = startOfEntryOffsets + entryOffsetCount * sizeof(long);
        var entryTypeToChapterOffset = file.AsSpan<long>(startOfEntryOffsets..endOfEntryOffsets);
        
        var startOfDataBlockOffsets = (int) directoryMetadata.BlocksOffset + sizeof(BlockSection);
        var endOfDataBlockOffsets = startOfDataBlockOffsets + blockSection.Count * sizeof(long);
        var dataBlockOffsets = file.AsSpan<long>(startOfDataBlockOffsets..endOfDataBlockOffsets);
        
        // Console.WriteLine($"Entry offsets: {entryTypeToChapterOffset.ToCommaSeparatedHexString()}");
        // Console.WriteLine($"Data block offsets: {dataBlockOffsets.ToCommaSeparatedHexString()}");

        //First actual allocation :))
        //Can't have an array of ref structs, so Block copies the offsets to an array
        var blocks = new Block[dataBlockOffsets.Length];
        for (var i = 0; i < dataBlockOffsets.Length; i++)
        {
            var header = file.As<BlockHeader>(dataBlockOffsets[i]);
            blocks[i] = new(header, file, (int)(dataBlockOffsets[i] + sizeof(BlockHeader)));
        }
        
        // Console.WriteLine($"Read {blocks.Length} blocks with a total of {blocks.Sum(b => b.Offsets.Length)} offsets, representing a total of {blocks.Sum(b => (int) b.Header.TotalBytes)} bytes.");
        
        _entries = new();
        for (var i = 0; i < entryTypeToChapterOffset.Length; i++)
        {
            if(entryTypeToChapterOffset[i] == 0)
                continue;
            
            var header = file.As<EntryHeader>(entryTypeToChapterOffset[i]);
            var entry = _entries[(EntryType) i] = new(header);
            entry.Block = blocks[header.BlockIndex];

            if (header.Format == EntryFormat.DynamicSizeElementArray)
            {
                var dataStart = (int) entryTypeToChapterOffset[i] + sizeof(EntryHeader);
                var dataEnd = dataStart + (int) header.Count * sizeof(long);

                entry.AdditionalEntryStorage = file.AsSpan<long>(dataStart..dataEnd).ToArray();
                
                // Console.WriteLine($"Read dynamic size element array of {entry.AdditionalEntryStorage!.Length} pointers for entry {(EntryType) i}, has blockIndex {header.BlockIndex}");
            }
        }
        
        Console.WriteLine($"Snapshot contains data for {_entries.Count} of {(int) EntryType.Count} entry types.");
        
        //Get snapshot file version
        var version = ReadEntryAsStruct<FormatVersion>(EntryType.Metadata_Version);
        
        Console.WriteLine($"Snapshot file version: {version}");

        var targetInfo = ReadEntryAsStruct<ProfileTargetInfo>(EntryType.ProfileTarget_Info);
        
        Console.WriteLine($"Target platform: {targetInfo}");
    }
    
    private static T ReadEntryAsStruct<T>(EntryType entryType) where T : struct 
        => MemoryMarshal.Read<T>(ReadEntry(entryType));

    private static byte[] ReadEntry(EntryType entryType) => ReadEntry(entryType, 0, 1);

    private static byte[] ReadEntry(EntryType entryType, int startOffset, int count)
    {
        var entryData = _entries[entryType];
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
                return null;
            }
            default:
                throw new("Unknown entry format.");
        }
    }
}