using System.Runtime.InteropServices;

namespace UnityMemorySnapshotLib.Structures.LowLevel;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DirectoryMetadata
{
    public uint Magic;
    public uint Version;
    public ulong BlocksOffset;
    public int EntriesCount;

    public override string ToString()
    {
        return $"Magic: {Magic:X8}, Version: {Version:X8}, BlocksOffset: {BlocksOffset:X8} EntriesCount: {EntriesCount:X8}";
    }
}