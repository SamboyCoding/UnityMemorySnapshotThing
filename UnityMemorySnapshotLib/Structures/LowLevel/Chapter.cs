namespace UnityMemorySnapshotLib.Structures.LowLevel;

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
                if (count + offset == Count)
                {
                    var entryOffset = AdditionalEntryStorage![offset];
                    size = (long)(Header.HeaderMeta - (ulong)entryOffset); //adding the size of the last element
                }
                else
                    size =(AdditionalEntryStorage![offset + count] - AdditionalEntryStorage[offset]);

                return size + (includeOffsetsMemory ? sizeof(long) * (count + 1) : 0);
            default:
                return 0;
        }
    }
}