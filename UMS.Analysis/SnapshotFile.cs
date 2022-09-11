using System.Runtime.InteropServices;
using UMS.Analysis.Structures;
using UMS.LowLevel;
using UMS.LowLevel.Structures;

namespace UMS.Analysis;

public class SnapshotFile : LowLevelSnapshotFile
{
    /// <summary>
    /// Basically the inverse of the TypeDescriptionInfoPointers array in the file
    /// </summary>
    public readonly Dictionary<ulong, int> TypeIndicesByPointer = new();

    public readonly WellKnownTypeHelper WellKnownTypes;

    public SnapshotFile(string path) : base(path)
    {
        WellKnownTypes = new(this);
        
        var pointers = TypeDescriptionInfoPointers;
        for (var i = 0; i < pointers.Length; i++) 
            TypeIndicesByPointer.Add(pointers[i], i);
    }
    
    public ManagedObjectInfo GetManagedObjectInfo(ulong address)
    {
        var info = new ManagedObjectInfo();
        var heap = GetSpanForHeapAddress(address);

        info.TypeInfoAddress = ReadPointer(ref heap);
        info.TypeDescriptionIndex = TypeIndicesByPointer[info.TypeInfoAddress];

        if (!TypeIndicesByPointer.TryGetValue(info.TypeInfoAddress, out info.TypeDescriptionIndex))
        {
            //Try and read at that pointer instead
            var typeInfoSpan = GetSpanForHeapAddress(info.TypeInfoAddress);
            info.TypeInfoAddress = ReadPointer(ref typeInfoSpan);
            TypeIndicesByPointer.TryGetValue(info.TypeInfoAddress, out info.TypeDescriptionIndex);
            
            if(!info.IsKnownType)
                throw new($"Failed to resolve type for object at {address:X}");
        }

        info.Size = SizeOfObjectInBytes(info);
        info.Data = heap[..info.Size];
        info.SelfAddress = address;
        
        return info;
    }

    public int SizeOfObjectInBytes(ManagedObjectInfo info)
    {
        var flags = ReadSingleValueType<TypeFlags>(EntryType.TypeDescriptions_Flags, info.TypeDescriptionIndex);

        if (flags.HasFlag(TypeFlags.Array))
            return ReadArrayObjectSizeBytes(info);
        
        if(info.TypeDescriptionIndex == WellKnownTypes.String)
            return ReadStringObjectSizeBytes(info);
        
        return ReadSingleValueType<int>(EntryType.TypeDescriptions_Size, info.TypeDescriptionIndex);
    }

    private int ReadStringObjectSizeBytes(ManagedObjectInfo info)
    {
        throw new NotImplementedException();
    }

    private int ReadArrayObjectSizeBytes(ManagedObjectInfo info)
    {
        throw new NotImplementedException();
    }

    public ulong ReadPointer(ref Span<byte> from)
    {
        var ret = VirtualMachineInformation.PointerSize switch
        {
            4 => MemoryMarshal.Read<uint>(from),
            8 => MemoryMarshal.Read<ulong>(from),
            _ => throw new($"Invalid pointer size {VirtualMachineInformation.PointerSize}")
        };
        from = from[VirtualMachineInformation.PointerSize..];
        return ret;
    }

    public Span<byte> GetSpanForHeapAddress(ulong ptr)
    {
        //Find managed heap section
        var sections = ManagedHeapSectionStartAddresses;
        Span<byte> heapSection = default;

        for (var i = sections.Length - 1; i >= 0; i--)
        {
            if (sections[i] < ptr)
            {
                heapSection = ReadSingleValueTypeArrayChapterElement<byte>(EntryType.ManagedHeapSections_Bytes, i);
                
                //Move start of span to the offset relative to the start of the heap section
                var offset = (int)(ptr - sections[i]);
                heapSection = heapSection[offset..];
                break;
            }
        }

        if (heapSection.IsEmpty)
            throw new($"Unable to find 0x{ptr:X} in the managed heap sections");

        return heapSection;
    }
}