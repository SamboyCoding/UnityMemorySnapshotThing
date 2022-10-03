using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures;

public struct BasicFieldInfoCache
{
    public int FieldIndex;
    public int TypeDescriptionIndex;
    public TypeFlags Flags;
    public int FieldOffset;
    public int FieldTypeSize;
    
    public bool IsValueType => (Flags & TypeFlags.ValueType) == TypeFlags.ValueType;
    
    public bool IsArray => (Flags & TypeFlags.Array) == TypeFlags.Array;
}