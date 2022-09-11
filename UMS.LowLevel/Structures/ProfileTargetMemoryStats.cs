namespace UMS.LowLevel.Structures;

#pragma warning disable CS0169, CS0649 // Field never used, field never assigned
// ReSharper disable UnassignedField.Global
public unsafe struct ProfileTargetMemoryStats
{
    public ulong TotalVirtualMemory;
    public ulong TotalUsedMemory;
    public ulong TotalReservedMemory;
    public ulong TempAllocatorUsedMemory;
    public ulong GraphicsUsedMemory;
    public ulong AudioUsedMemory;
    public ulong GcHeapUsedMemory;
    public ulong GcHeapReservedMemory;
    public ulong ProfilerUsedMemory;
    public ulong ProfilerReservedMemory;
    public ulong MemoryProfilerUsedMemory;
    public ulong MemoryProfilerReservedMemory;
    private uint _freeBlockBucketCount;
    private fixed uint _freeBlockBuckets[32];
    private fixed byte _padding[32];
    
    public uint[] FreeBlockBuckets
    {
        get
        {
            var buckets = new uint[_freeBlockBucketCount];
            for (var i = 0; i < buckets.Length; i++) 
                buckets[i] = _freeBlockBuckets[i];

            return buckets;
        }
    }

    public override string ToString()
    {
        return $"TotalVirtualMemory: {TotalVirtualMemory}, TotalUsedMemory: {TotalUsedMemory}, TotalReservedMemory: {TotalReservedMemory}, TempAllocatorUsedMemory: {TempAllocatorUsedMemory}, GraphicsUsedMemory: {GraphicsUsedMemory}, " +
               $"AudioUsedMemory: {AudioUsedMemory}, GcHeapUsedMemory: {GcHeapUsedMemory}, GcHeapReservedMemory: {GcHeapReservedMemory}, ProfilerUsedMemory: {ProfilerUsedMemory}, ProfilerReservedMemory: {ProfilerReservedMemory}, " +
               $"MemoryProfilerUsedMemory: {MemoryProfilerUsedMemory}, MemoryProfilerReservedMemory: {MemoryProfilerReservedMemory}, FreeBlockBuckets: {string.Join(", ", FreeBlockBuckets)}";
    }
}