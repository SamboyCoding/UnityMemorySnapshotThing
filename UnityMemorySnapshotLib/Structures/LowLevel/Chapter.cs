﻿namespace UnityMemorySnapshotLib.Structures.LowLevel;

public class Chapter
{
    public ChapterHeader Header;
    public long[]? AdditionalEntryStorage;
    public Block Block;
    
    public Chapter(ChapterHeader header)
    {
        Header = header;
        AdditionalEntryStorage = null;
    }

    public uint Count => Header.Count;
    
    public long ComputeByteSizeForEntryRange(long offset, long count, bool includeOffsetsMemory)
    {
        switch (Header.Format)
        {
            case EntryFormat.SingleElement:
                return Header.EntriesMeta;
            case EntryFormat.ConstantSizeElementArray:
                return Header.EntriesMeta * (count - offset);
            case EntryFormat.DynamicSizeElementArray:
                long size = 0;
                //Additional entry storage gives us the offsets to the start of each element
                
                // if (count + offset == Count)
                // {
                //     //
                //     var entryOffset = AdditionalEntryStorage![offset];
                //     size = (long)(Header.HeaderMeta - (ulong)entryOffset); //adding the size of the last element
                // }
                if(offset == 0)
                    //just get the (n - 1)th item as it gives the end of the nth element
                    size = AdditionalEntryStorage![count - 1];
                else
                    size = (AdditionalEntryStorage![offset + count - 1] - AdditionalEntryStorage[offset - 1]);

                return size + (includeOffsetsMemory ? sizeof(long) * (count + 1) : 0);
            default:
                return 0;
        }
    }
}