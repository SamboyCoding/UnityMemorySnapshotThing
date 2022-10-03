using System.Runtime;
using UMS.Analysis;
using UMS.Analysis.Structures;
using UMS.Analysis.Structures.Objects;
using UMS.LowLevel;
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
        
        Console.WriteLine($"Snapshot file version: {file.SnapshotFormatVersion}\n");
        Console.WriteLine($"Snapshot taken on {file.CaptureDateTime}\n");
        Console.WriteLine($"Target platform: {file.ProfileTargetInfo}\n");
        Console.WriteLine($"Memory stats: {file.ProfileTargetMemoryStats}\n");
        Console.WriteLine($"VM info: {file.VirtualMachineInformation}\n");
        
        // Console.WriteLine("Querying large dynamic arrays...");
        // start = DateTime.Now;
        // Console.WriteLine($"Snapshot contains {file.NativeObjectNames.Length} native objects and {file.TypeDescriptionNames.Length} managed objects");
        //
        // var heapSections = file.ManagedHeapSectionBytes;
        // var heapSectionStartAddresses = file.ManagedHeapSectionStartAddresses;
        // Console.WriteLine($"Snapshot contains {heapSections.Length} managed heap sections (starting at {heapSectionStartAddresses.Length} start addresses) totalling {heapSections.Sum(b => b.Length)} bytes");
        //
        // var fieldIndices = file.TypeDescriptionFieldIndices;
        // var fieldBytes = file.TypeDescriptionStaticFieldBytes;
        // Console.WriteLine($"Snapshot contains {fieldIndices.Length} type description-field index mappings, totalling {fieldIndices.Sum(i => i.Length)} field indices, and {fieldBytes.Length} type description-static field bytes");
        //
        // var fieldNames = file.FieldDescriptionNames;
        // Console.WriteLine($"Snapshot contains {fieldNames.Length} field names");
        //
        // var fieldOffsets = file.FieldDescriptionOffsets;
        // Console.WriteLine($"Snapshot contains {fieldOffsets.Length} field offsets");
        //
        // var fieldTypes = file.FieldDescriptionTypeIndices;
        // Console.WriteLine($"Snapshot contains {fieldTypes.Length} field-type mappings");
        //
        // //Field indices map type description names to field names
        // //e.g. field indices element 2 has some values, so those values are the indices into the field name array for type description name 2
        //
        // Console.WriteLine($"Querying large dynamic arrays took {(DateTime.Now - start).TotalMilliseconds} ms\n");

        CrawlManagedObjects(file);
        
        FindLeakedUnityObjects(file);
    }

    private static void CrawlManagedObjects(SnapshotFile file)
    {
        //Start with GC Handles
        var gcHandles = file.GcHandles;
        
        //Each of those is a pointer into a managed heap section which can be mapped to the data in that file
        //At that address there will be an object header which allows us to find the type index or type description
        //It also then contains the instance fields (which we can parse with type data), and we can potentially crawl to other objects using connection data
        //We want to find all the objects so we can then iterate on them easily

        var validCount = 0;
        var start = DateTime.Now;
        var rootObjects = new List<ManagedClassInstance>(gcHandles.Length);
        
        Console.WriteLine($"Processing {gcHandles.Length} GC roots...");
        // GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        foreach (var gcHandle in gcHandles)
        {
            rootObjects.Add(file.GetManagedClassInstance(gcHandle)!.Value);
            
            validCount++;
            
            // if(validCount % 1000 == 0)
                // Console.WriteLine($"Processed {validCount} GC roots in {(DateTime.Now - start).TotalMilliseconds} ms");
        }

        GCSettings.LatencyMode = GCLatencyMode.Interactive;
        
        Console.WriteLine($"Found {validCount} valid GC roots out of {gcHandles.Length} total in {(DateTime.Now - start).TotalMilliseconds} ms");
    }
    
    private static void FindLeakedUnityObjects(SnapshotFile file)
    {
        var start = DateTime.Now;
        Console.WriteLine("Finding leaked Unity objects...");
        
        //Find all the managed objects, filter to those which have a m_CachedObjectPtr field
        //Then filter to those for which that field is 0 (i.e. not pointing to a native object)
        //That gives the leaked managed shells.
        Console.WriteLine($"Snapshot contains {file.AllManagedClassInstances.Count()} managed objects");

        var filterStart = DateTime.Now;

        var unityEngineObjects = file.AllManagedClassInstances.Where(i => i.InheritsFromUnityEngineObject(file)).ToArray();
        
        Console.WriteLine($"Of those, {unityEngineObjects.Length} inherit from UnityEngine.Object (filtered in {(DateTime.Now - filterStart).TotalMilliseconds} ms)");
        
        var detectStart = DateTime.Now;

        int numLeaked = 0;
        foreach (var managedClassInstance in unityEngineObjects)
        {
            var fields = file.GetFieldInfoForTypeIndex(managedClassInstance.TypeInfo.TypeIndex);
            for (var fieldNumber = 0; fieldNumber < fields.Length; fieldNumber++)
            {
                var basicFieldInfoCache = fields[fieldNumber];
                var name = file.GetFieldName(basicFieldInfoCache.FieldIndex);

                if (name == "m_CachedPtr")
                {
                    var value = managedClassInstance.Fields[fieldNumber];
                    
                    if(value is not IntegerFieldValue integerFieldValue)
                        throw new Exception("Expected integer field value");
                    
                    if (integerFieldValue.Value == 0)
                    {
                        var typeName = file.GetTypeName(managedClassInstance.TypeInfo.TypeIndex);
                        Console.WriteLine($"Found leaked managed object of type: {typeName} at memory address 0x{managedClassInstance.ObjectAddress:X}");
                        Console.WriteLine($"    Retention Path: {managedClassInstance.GetFirstObservedRetentionPath(file)}");
                        numLeaked++;
                    }
                }
            }
        }
        
        Console.WriteLine($"Finished detection in {(DateTime.Now - detectStart).TotalMilliseconds} ms. {numLeaked} of those are leaked managed shells");
    }
}