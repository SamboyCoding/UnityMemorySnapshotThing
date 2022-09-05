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

        var start = DateTime.Now;
        Console.WriteLine();
        Console.WriteLine("Reading snapshot file...");
        using var file = new SnapshotFile(filePath);
        Console.WriteLine($"Read snapshot file in {(DateTime.Now - start).TotalMilliseconds} ms\n");
        
        Console.WriteLine($"Snapshot file version: {file.SnapshotFormatVersion}\n");
        Console.WriteLine($"Snapshot taken on {file.CaptureDateTime}\n");
        Console.WriteLine($"Target platform: {file.ProfileTargetInfo}\n");
        Console.WriteLine($"Memory stats: {file.ProfileTargetMemoryStats}\n");
        Console.WriteLine($"VM info: {file.VirtualMachineInformation}\n");

        Console.WriteLine($"Snapshot contains {file.NativeObjectNames.Length} native objects and {file.TypeDescriptionNames.Length} managed objects");
    }
}