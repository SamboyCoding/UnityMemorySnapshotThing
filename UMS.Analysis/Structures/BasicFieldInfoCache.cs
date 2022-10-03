using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures;

public struct BasicFieldInfoCache
{
    public int FieldIndex;
    public int TypeDescriptionIndex;
    public TypeFlags Flags;
    public int FieldOffset;
    public int FieldTypeSize;
}