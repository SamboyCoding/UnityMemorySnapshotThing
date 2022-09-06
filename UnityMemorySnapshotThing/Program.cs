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
        
        Console.WriteLine("Querying large dynamic arrays...");
        start = DateTime.Now;
        Console.WriteLine($"Snapshot contains {file.NativeObjectNames.Length} native objects and {file.TypeDescriptionNames.Length} managed objects");

        var heapSections = file.ManagedHeapSectionBytes;
        Console.WriteLine($"Snapshot contains {heapSections.Length} managed heap sections totalling {heapSections.Sum(b => b.Length)} bytes");

        var fieldIndices = file.TypeDescriptionFieldIndices;
        Console.WriteLine($"Snapshot contains {fieldIndices.Length} type description-field index mappings, totalling {fieldIndices.Sum(i => i.Length)} field indices");
        
        var fieldNames = file.FieldDescriptionNames;
        Console.WriteLine($"Snapshot contains {fieldNames.Length} field names");
        
        //Field indices map type description names to field names
        //e.g. field indices element 2 has some values, so those values are the indices into the field name array for type description name 2
        
        Console.WriteLine($"Querying large dynamic arrays took {(DateTime.Now - start).TotalMilliseconds} ms\n");
    }
}