using System.Runtime.InteropServices;

namespace UnityMemorySnapshotThing.Structures.LowLevel;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct EntryHeader
{
    public EntryFormat Format;
    public uint BlockIndex;
    
    /// <summary>
    /// For constant size array, size of element
    /// For dynamic size array, length of array
    /// For single element, size of element 
    /// </summary>
    public uint EntriesMeta;
    /// <summary>
    /// For constant size array, length of array
    /// For dynamic size array, size of data
    /// For single element, offset of data in block
    /// </summary>
    public ulong HeaderMeta;

    public uint Count => Format switch
    {
        EntryFormat.SingleElement => 1,
        EntryFormat.ConstantSizeElementArray => (uint)HeaderMeta,
        EntryFormat.DynamicSizeElementArray => EntriesMeta,
        _ => 0
    };
}