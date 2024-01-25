namespace UMS.LowLevel.Structures;

public class ManagedHeapSection : IComparable
{
    private const ulong referenceBit = 1UL << 63; 
    
    public ulong VirtualAddress;
    public ulong VirtualAddressEnd;
    public byte[] Heap;

    public bool IsVmSection;

    public ManagedHeapSection(ulong virtualAddress, bool areAddressesEncoded, byte[] heap)
    {
        if (areAddressesEncoded)
        {
            VirtualAddress = virtualAddress & ~referenceBit;
            IsVmSection = (virtualAddress & referenceBit) != 0;
        }
        else
        {
            VirtualAddress = virtualAddress;
            IsVmSection = false;
        }

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