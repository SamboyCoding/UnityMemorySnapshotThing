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

        var file = new SnapshotFile(filePath);

        //Get snapshot file version
        var version = file.ReadEntryAsStruct<FormatVersion>(EntryType.Metadata_Version);
        
        Console.WriteLine($"Snapshot file version: {version}");

        var targetInfo = file.ReadEntryAsStruct<ProfileTargetInfo>(EntryType.ProfileTarget_Info);
        
        Console.WriteLine($"Target platform: {targetInfo}");
    }
}