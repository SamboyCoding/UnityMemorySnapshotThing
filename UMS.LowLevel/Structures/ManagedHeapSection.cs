namespace UMS.LowLevel.Structures;

public class ManagedHeapSection : IComparable
{
    public ulong VirtualAddress;
    public ulong VirtualAddressEnd;
    public byte[] Heap;

    public ManagedHeapSection(ulong virtualAddress, byte[] heap)
    {
        VirtualAddress = virtualAddress;
        Heap = heap;
        VirtualAddressEnd = VirtualAddress + (ulong) Heap.Length;
    }

    public int CompareTo(object? obj)
    {
        if (obj is ManagedHeapSection other)
        {
            return VirtualAddress.CompareTo(other.VirtualAddress);
        }

        return 0;
    }
}