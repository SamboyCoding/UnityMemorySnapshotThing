using UnityMemorySnapshotLib;

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
        
        Console.WriteLine($"Snapshot file version: {file.SnapshotFormatVersion}");
        Console.WriteLine($"Target platform: {file.ProfileTargetInfo}");
        Console.WriteLine($"Memory stats: {file.ProfileTargetMemoryStats}");
        Console.WriteLine($"VM info: {file.VirtualMachineInformation}");
    }
}