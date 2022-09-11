namespace UMS.LowLevel.Structures;

[Flags]
public enum CaptureFlags : uint
{
    None,
    ManagedObjects = 1 << 0,
    NativeObjects = 1 << 1,
    NativeAllocations = 1 << 2,
    NativeAllocationSites = 1 << 3,
    NativeStackTraces = 1 << 4,
}