namespace UMS.LowLevel.Structures;

[Flags]
public enum TypeFlags : uint
{
    None = 0,
    ValueType = 1 << 0,
    Array = 1 << 1,
    ArrayRankMask = 0xFFFF0000
}