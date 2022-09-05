namespace UnityMemorySnapshotLib.Structures.LowLevel;

public struct DirectoryMetadata
{
    public uint Magic;
    public uint Version;
    public ulong BlocksOffset;

    public override string ToString()
    {
        return $"Magic: {Magic:X8}, Version: {Version:X8}, BlocksOffset: {BlocksOffset:X8}";
    }
}