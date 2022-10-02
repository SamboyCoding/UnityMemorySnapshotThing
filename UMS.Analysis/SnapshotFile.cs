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

    public readonly Dictionary<int, int> TypeIndexIndices = new(); // TypeIndex -> Index in the TypeIndex array. What.

    public readonly WellKnownTypeHelper WellKnownTypes;

    public SnapshotFile(string path) : base(path)
    {
        WellKnownTypes = new(this);
        
        var pointers = TypeDescriptionInfoPointers;
        for (var i = 0; i < pointers.Length; i++) 
            TypeIndicesByPointer.Add(pointers[i], i);

        var indices = TypeDescriptionIndices;
        for (var i = 0; i < indices.Length; i++) 
            TypeIndexIndices.Add(indices[i], i);
    }
    
    public ManagedObjectInfo GetManagedObjectInfo(ulong address)
    {
        var info = new ManagedObjectInfo();
        if (!TryGetSpanForHeapAddress(address, out var heap))
            return info;

        info.TypeInfoAddress = ReadPointer(ref heap);
        // info.TypeDescriptionIndex = TypeIndicesByPointer[info.TypeInfoAddress];

        if (!TypeIndicesByPointer.TryGetValue(info.TypeInfoAddress, out info.TypeDescriptionIndex))
        {
            //Try and read at that pointer instead
            if (!TryGetSpanForHeapAddress(info.TypeInfoAddress, out var typeInfoSpan))
                return new();
            
            info.TypeInfoAddress = ReadPointer(ref typeInfoSpan);
            TypeIndicesByPointer.TryGetValue(info.TypeInfoAddress, out info.TypeDescriptionIndex);
            
            if(!info.IsKnownType)
                throw new($"Failed to resolve type for object at {address:X}");
        }

        info.Flags = ReadSingleValueType<TypeFlags>(EntryType.TypeDescriptions_Flags, info.TypeDescriptionIndex);
        info.Size = SizeOfObjectInBytes(info, heap);
        info.Data = heap[..info.Size];
        info.SelfAddress = address;

        return info;
    }

    public int SizeOfObjectInBytes(ManagedObjectInfo info, Span<byte> heap)
    {
        if (info.Flags.HasFlag(TypeFlags.Array))
            return GetObjectSizeFromArrayInBytes(info, heap);
        
        if(info.TypeDescriptionIndex == WellKnownTypes.String)
            return GetObjectSizeFromStringInBytes(info, heap);
        
        return ReadSingleValueType<int>(EntryType.TypeDescriptions_Size, info.TypeDescriptionIndex);
    }

    private int GetObjectSizeFromStringInBytes(ManagedObjectInfo info, Span<byte> heap)
    {
        heap = heap[VirtualMachineInformation.PointerSize..]; //Add one pointer to skip the type pointer
        var length = MemoryMarshal.Read<int>(heap);

        if (length < 0 || length * 2u > heap.Length)
        {
            Console.WriteLine($"Warning - String length is invalid: {length}");
            return 0;
        }
        
        return VirtualMachineInformation.ObjectHeaderSize + 1 + length * 2 + 2; //Add one byte for the length, and two bytes for the null terminator
    }

    private int GetObjectSizeFromArrayInBytes(ManagedObjectInfo info, Span<byte> heap)
    {
        var arrayLength = ReadArrayLength(info, heap);

        if (arrayLength > heap.Length)
        {
            Console.WriteLine($"Warning: Skipping unreasonable array length {arrayLength}");
            return VirtualMachineInformation.ArrayHeaderSize; //Sanity bailout
        }

        //Need to check if the array element type is a value type
        var elementTypeIndex = ReadSingleValueType<int>(EntryType.TypeDescriptions_BaseOrElementTypeIndex, info.TypeDescriptionIndex);
        if (elementTypeIndex == -1)
            elementTypeIndex = info.TypeDescriptionIndex;

        var ai = TypeIndexIndices[elementTypeIndex];
        var flags = ReadSingleValueType<TypeFlags>(EntryType.TypeDescriptions_Flags, ai);
        var isValueType = flags.HasFlag(TypeFlags.ValueType);
        
        //Ok now we can get the size of the elements in the array - either VT size or pointer size for ref types
        var elementSize = isValueType ? ReadSingleValueType<int>(EntryType.TypeDescriptions_Size, ai) : VirtualMachineInformation.PointerSize;

        return VirtualMachineInformation.ArrayHeaderSize + elementSize * arrayLength;
    }

    private int ReadArrayLength(ManagedObjectInfo info, Span<byte> heap)
    {
        var heapTemp = heap[VirtualMachineInformation.ArrayBoundsOffsetInHeader..]; //Seek to the bounds offset
        var bounds = ReadPointer(ref heapTemp);

        if (bounds == 0)
            return MemoryMarshal.Read<int>(heap[VirtualMachineInformation.ArraySizeOffsetInHeader..]); //If there are no bounds, just read the size
        
        //Otherwise, we need to read the bounds
        if (!TryGetSpanForHeapAddress(bounds, out var boundsHeap))
            return 0;

        var length = 1;
        var rank = (int) (info.Flags & TypeFlags.ArrayRankMask) >> 16;
        for (var i = 0; i < rank; i++)
        {
            length *= MemoryMarshal.Read<int>(boundsHeap);
            boundsHeap = boundsHeap[8..]; //Move to the next bound
        }

        return length;
    }

    public ulong ReadPointer(ref Span<byte> from)
    {
        var ret = VirtualMachineInformation.PointerSize switch
        {
            4 => MemoryMarshal.Read<uint>(from),
            8 => MemoryMarshal.Read<ulong>(from),
            _ => throw new($"Invalid pointer size {VirtualMachineInformation.PointerSize}")
        };
        //Though it would make sense, it appears that unity's implementation does NOT increment the read address after reading a pointer...
        // from = from[VirtualMachineInformation.PointerSize..];
        return ret;
    }

    public bool TryGetSpanForHeapAddress(ulong ptr, out Span<byte> heapSection)
    {
        //Find managed heap section
        var sections = ManagedHeapSectionStartAddresses;
        heapSection = default;

        for (var i = sections.Length - 1; i >= 0; i--)
        {
            if (sections[i].VirtualAddress <= ptr)
            {
                //Move start of span to the offset relative to the start of the heap section
                var offset = ptr - sections[i].VirtualAddress;

                if (offset >= int.MaxValue)
                {
                    //more than 2GiB above the current heap section -> probably not managed heap
                    break;
                }

                var intOffset = (int)offset;
                var section = ReadSingleValueTypeArrayChapterElement<byte>(EntryType.ManagedHeapSections_Bytes, sections[i].HeapIndex);
                
                if (section.Length < intOffset)
                    break;
                
                heapSection = section[intOffset..];

                break;
            }
        }

        return !heapSection.IsEmpty;
    }
}