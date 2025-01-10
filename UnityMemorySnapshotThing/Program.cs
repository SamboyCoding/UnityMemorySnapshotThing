using System.Globalization;
using System.Text;
using UMS.Analysis;
using UMS.Analysis.Structures.Objects;
using UMS.LowLevel.Structures;

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
        
        Console.WriteLine($"Snapshot file version: {file.SnapshotFormatVersion} ({(int) file.SnapshotFormatVersion})");
        Console.WriteLine($"Snapshot taken on {file.CaptureDateTime}");
        Console.WriteLine($"Target platform: {file.ProfileTargetInfo}");
        Console.WriteLine($"Memory info at time of snapshot: {file.ProfileTargetMemoryStats}");
        
        Console.WriteLine();
        Console.WriteLine("Finding objects in snapshot...");

        file.LoadManagedObjectsFromGcRoots();
        file.LoadManagedObjectsFromStaticFields();

        Console.WriteLine($"Found {file.AllManagedClassInstances.Count()} managed objects.");

        while (true)
        {
            Console.Write("\n\nWhat would you like to do now?\n1: Find leaked managed shells.\n2: Dump information on a specific object (by address).\n0: Exit\nChoice: ");

            var choice = Console.ReadLine();

            if (choice == "1")
                FindLeakedUnityObjects(file);
            else if (choice == "2")
                DumpObjectInfo(file);
            else if(choice == "0")
                break;
            else
                Console.WriteLine("Invalid choice.");
        }
    }
    
    private static void FindLeakedUnityObjects(SnapshotFile file)
    {
        var start = DateTime.Now;
        Console.WriteLine("Finding leaked Unity objects...");
        
        //Find all the managed objects, filter to those which have a m_CachedObjectPtr field
        //Then filter to those for which that field is 0 (i.e. not pointing to a native object)
        //That gives the leaked managed shells.
        var ret = new StringBuilder();
        var str = $"Snapshot contains {file.AllManagedClassInstances.Count()} managed objects";
        Console.WriteLine(str);
        ret.AppendLine(str);

        var filterStart = DateTime.Now;

        var unityEngineObjects = file.AllManagedClassInstances.Where(i => i.InheritsFromUnityEngineObject(file)).ToArray();

        str = $"Of those, {unityEngineObjects.Length} inherit from UnityEngine.Object (filtered in {(DateTime.Now - filterStart).TotalMilliseconds} ms)";
        Console.WriteLine(str);
        ret.AppendLine(str);
        
        var detectStart = DateTime.Now;

        int numLeaked = 0;
        var leakedTypes = new Dictionary<string, int>();
        foreach (var managedClassInstance in unityEngineObjects)
        {
            if (managedClassInstance.IsLeakedManagedShell(file))
            {
                var typeName = file.GetTypeName(managedClassInstance.TypeInfo.TypeIndex);

                str = $"Found leaked managed object of type: {typeName} at memory address 0x{managedClassInstance.ObjectAddress:X}";
                Console.WriteLine(str);
                ret.AppendLine(str);

                str = $"    Retention Path: {managedClassInstance.GetFirstObservedRetentionPath(file)}";
                Console.WriteLine(str);
                ret.AppendLine(str);
                        
                leakedTypes[typeName] = leakedTypes.GetValueOrDefault(typeName) + 1;
                        
                numLeaked++;
            }
        }

        str = $"Finished detection in {(DateTime.Now - detectStart).TotalMilliseconds} ms. {numLeaked} of those are leaked managed shells";
        Console.WriteLine(str);
        ret.AppendLine(str);
        
        var leakedTypesSorted = leakedTypes.OrderByDescending(kvp => kvp.Value).ToArray();
        
        str = $"Leaked types by count: \n{string.Join("\n", leakedTypesSorted.Select(kvp => $"{kvp.Value} x {kvp.Key}"))}";
        ret.AppendLine(str);
        
        File.WriteAllText("leaked_objects.txt", ret.ToString());
    }
    
    private static void DumpObjectInfo(SnapshotFile file)
    {
        Console.WriteLine("Enter the memory address of the object you want to dump:");
        var addressString = Console.ReadLine();
        
        if (!ulong.TryParse(addressString, NumberStyles.HexNumber, null, out var address))
        {
            Console.WriteLine("Unable to parse address.");
            return;
        }

        var nullableObj = file.TryFindManagedClassInstanceByAddress(address);
        
        if (nullableObj == null)
        {
            Console.WriteLine($"No object at address 0x{address:X8} was found in the snapshot");
            return;
        }

        var obj = nullableObj.Value;

        if ((obj.TypeDescriptionFlags & TypeFlags.Array) != 0)
        {
            Console.WriteLine("Dumping arrays is not supported, yet.");
            return;
        }
        
        Console.WriteLine($"Found object at address 0x{address:X8}");
        Console.WriteLine($"Type: {file.GetTypeName(obj.TypeInfo.TypeIndex)}");
        Console.WriteLine($"Flags: {obj.TypeDescriptionFlags}");
        Console.WriteLine($"Retention path: {obj.GetFirstObservedRetentionPath(file)}");
        Console.WriteLine("Fields:");

        for (var i = 0; i < obj.Fields.Length; i++)
        {
            WriteField(file, obj, i);
        }
    }

    private static void WriteField(SnapshotFile file, ManagedClassInstance parent, int index)
    {
        var fields = file.GetInstanceFieldInfoForTypeIndex(parent.TypeInfo.TypeIndex);
        var fieldInfo = fields[index];
        var fieldValue = parent.Fields[index];

        var fieldName = file.GetFieldName(fieldInfo.FieldIndex);
        var fieldType = file.GetTypeName(fieldInfo.TypeDescriptionIndex);
        
        Console.WriteLine($"    {fieldType} {fieldName} = {fieldValue}");
    }

   
}