using UnityMemorySnapshotLib;
using UnityMemorySnapshotLib.Structures;

namespace UnityMemorySnapshotThing;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No file specified");
            return;
        }
        
        var filePath = args[0];

        using var file = new SnapshotFile(filePath);

        //Get snapshot file version
        var version = file.ReadChapterAsStruct<FormatVersion>(EntryType.Metadata_Version);
        
        Console.WriteLine($"Snapshot file version: {version}");

        var targetInfo = file.ReadChapterAsStruct<ProfileTargetInfo>(EntryType.ProfileTarget_Info);
        
        Console.WriteLine($"Target platform: {targetInfo}");
    }
}