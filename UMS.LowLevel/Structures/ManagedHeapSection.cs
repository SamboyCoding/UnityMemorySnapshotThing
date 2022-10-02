namespace UMS.LowLevel.Structures;

public class ManagedHeapSection : IComparable
{
    public ulong VirtualAddress;
    public int HeapIndex;
    public int CompareTo(object? obj)
    {
        if (obj is ManagedHeapSection other)
        {
            return VirtualAddress.CompareTo(other.VirtualAddress);
        }

        return 0;
    }
}