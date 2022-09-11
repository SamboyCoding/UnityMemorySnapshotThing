namespace UMS.LowLevel.Structures;

public struct VirtualMachineInformation
{
    public int PointerSize;
    public int ObjectHeaderSize;
    public int ArrayHeaderSize;
    public int ArrayBoundsOffsetInHeader;
    public int ArraySizeOffsetInHeader;
    public int AllocationGranularity;

    public override string ToString()
    {
        return $"PointerSize: {PointerSize}, ObjectHeaderSize: {ObjectHeaderSize}, ArrayHeaderSize: {ArrayHeaderSize}, ArrayBoundsOffsetInHeader: {ArrayBoundsOffsetInHeader}, ArraySizeOffsetInHeader: {ArraySizeOffsetInHeader}, AllocationGranularity: {AllocationGranularity}";
    }
}