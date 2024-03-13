using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UMS.Analysis.Structures;
using UMS.Analysis.Structures.Objects;
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

    public readonly Dictionary<int, int> StaticFieldsToOwningTypes = new();

    public readonly WellKnownTypeHelper WellKnownTypes;
    
    private readonly Dictionary<int, BasicFieldInfoCache[]> _nonStaticFieldIndicesByTypeIndex = new();
    private readonly Dictionary<int, BasicFieldInfoCache[]> _staticFieldIndicesByTypeIndex = new();
    
    private readonly Dictionary<int, BasicTypeInfoCache> _typeInfoCacheByTypeIndex = new();

    private readonly Dictionary<ulong, RawManagedObjectInfo> _managedObjectInfoCache = new(1024*1024 * 4);

    private readonly Dictionary<ulong, ManagedClassInstance> _managedClassInstanceCache = new(1024 * 1024 * 4);
    
    private readonly Dictionary<int, string> _typeNamesByTypeIndex = new();
    
    private readonly Dictionary<int, string> _fieldNamesByFieldIndex = new();

    public IEnumerable<ManagedClassInstance> AllManagedClassInstances => _managedClassInstanceCache.Values;

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

    public void LoadManagedObjectsFromGcRoots()
    {
        var gcRoots = GcHandles;
        var start = DateTime.Now;
        
        Console.WriteLine($"Processing {gcRoots.Length} GC roots...");
        foreach (var gcHandle in gcRoots) 
            GetOrCreateManagedClassInstance(gcHandle);
        
        Console.WriteLine($"Found {_managedClassInstanceCache.Count} managed objects in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    public void LoadManagedObjectsFromStaticFields()
    {
        var allStaticFields = ReadValueTypeArrayChapter<byte>(EntryType.TypeDescriptions_StaticFieldBytes, 0, -1);

        var start = DateTime.Now;
        var initialCount = _managedClassInstanceCache.Count;
        
        Console.WriteLine($"Processing static field info for {allStaticFields.Length} types...");
        for (var typeIndex = 0; typeIndex < allStaticFields.Length; typeIndex++)
        {
            var typeFieldBytes = allStaticFields[typeIndex].AsSpan();
            if (typeFieldBytes.Length == 0)
                continue;

            var typeInfo = GetTypeInfo(typeIndex);
            var staticFields = GetStaticFieldInfoForTypeIndex(typeIndex);
            
            foreach (var field in staticFields)
            {
                StaticFieldsToOwningTypes[field.FieldIndex] = typeIndex;
                if(field.IsValueType)
                    continue; //TODO
                
                if(field.IsArray)
                    continue;

                var fieldOffset = field.FieldOffset;
                
                if(fieldOffset < 0)
                    continue; //Generics, mainly
                
                var fieldPointer = MemoryMarshal.Read<ulong>(typeFieldBytes[fieldOffset..]);
                if (fieldPointer == 0)
                    continue;
                
                GetOrCreateManagedClassInstance(fieldPointer, reason: LoadedReason.StaticField, fieldOrArrayIdx: field.FieldIndex);
            }
        }
        
        Console.WriteLine($"Found {_managedClassInstanceCache.Count - initialCount} additional managed objects from static fields in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    public ManagedClassInstance? GetOrCreateManagedClassInstance(ulong address, ManagedClassInstance? parent = null, int depth = 0, LoadedReason reason = LoadedReason.GcRoot, int fieldOrArrayIdx = int.MinValue)
    {
        if (_managedClassInstanceCache.TryGetValue(address, out var ret))
            return ret;
        
        var info = ParseManagedObjectInfo(address);
        if (!info.IsKnownType)
            return null;

        var instance = new ManagedClassInstance(this, info, parent, depth, reason, fieldOrArrayIdx);
        _managedClassInstanceCache[address] = instance;
        return instance;
    }

    public RawManagedObjectInfo ParseManagedObjectInfo(ulong address)
    {
        if (_managedObjectInfoCache.TryGetValue(address, out var ret))
            return ret;
        
        var info = new RawManagedObjectInfo();
        if (!TryGetSpanForHeapAddress(address, out var heap))
            return info;

        info.TypeInfoAddress = ReadPointer(heap);

        if (!TypeIndicesByPointer.TryGetValue(info.TypeInfoAddress, out info.TypeDescriptionIndex))
        {
            //Try and read at that pointer instead
            if (!TryGetSpanForHeapAddress(info.TypeInfoAddress, out var typeInfoSpan))
                return new();

            info.TypeInfoAddress = ReadPointer(typeInfoSpan);
            if (!TypeIndicesByPointer.TryGetValue(info.TypeInfoAddress, out info.TypeDescriptionIndex) || !info.IsKnownType)
            {
                Console.WriteLine($"WARNING: Failed to resolve type for object at {address:X}");
                
                //Cache the failure - let's not waste time.
                ret = _managedObjectInfoCache[address] = new();
                return ret;
            }
        }

        var typeIndex = info.TypeDescriptionIndex;
        info.Flags = GetTypeFlagsByIndex(typeIndex);
        info.Size = SizeOfObjectInBytes(info, heap);
        if (info.Size == 0)
            throw new("Size 0?");
        info.Data = heap[..info.Size].ToArray();
        info.SelfAddress = address;
        
        _managedObjectInfoCache.Add(address, info);

        return info;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeFlags GetTypeFlagsByIndex(int typeIndex) 
        => ReadSingleValueType<TypeFlags>(EntryType.TypeDescriptions_Flags, typeIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetTypeDescriptionSizeBytes(int index) 
        => ReadSingleValueType<int>(EntryType.TypeDescriptions_Size, index);

    public int SizeOfObjectInBytes(RawManagedObjectInfo info, Span<byte> heap)
    {
        if (info.Flags.HasFlag(TypeFlags.Array))
            return GetObjectSizeFromArrayInBytes(info, heap);

        if (info.TypeDescriptionIndex == WellKnownTypes.String)
            return GetObjectSizeFromStringInBytes(info, heap);

        return GetTypeDescriptionSizeBytes(info.TypeDescriptionIndex);
    }

    private int GetObjectSizeFromStringInBytes(RawManagedObjectInfo info, Span<byte> heap)
    {
        heap = heap[VirtualMachineInformation.ObjectHeaderSize..]; //Skip object header
        var length = MemoryMarshal.Read<int>(heap);

        if (length < 0 || length * 2u > heap.Length)
        {
            Console.WriteLine($"Warning - String length is invalid: {length}");
            return 0;
        }

        return VirtualMachineInformation.ObjectHeaderSize + 4 + length * 2 + 2; //Add four bytes for the length, and two bytes for the null terminator
    }

    private int GetObjectSizeFromArrayInBytes(RawManagedObjectInfo info, Span<byte> heap)
    {
        var arrayLength = ReadArrayLength(info.Flags, heap);

        if (arrayLength > heap.Length)
        {
            Console.WriteLine($"Warning: Reducing array length {arrayLength} to {heap.Length} because the heap doesn't contain all the data.");
            return heap.Length; //Sanity bailout
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

    public int ReadArrayLength(TypeFlags flags, Span<byte> heap)
    {
        var heapTemp = heap[VirtualMachineInformation.ArrayBoundsOffsetInHeader..]; //Seek to the bounds offset
        var bounds = ReadPointer(heapTemp);

        if (bounds == 0)
            return MemoryMarshal.Read<int>(heap[VirtualMachineInformation.ArraySizeOffsetInHeader..]); //If there are no bounds, just read the size

        //Otherwise, we need to read the bounds
        if (!TryGetSpanForHeapAddress(bounds, out var boundsHeap))
            return 0;

        var length = 1;
        var rank = (int)(flags & TypeFlags.ArrayRankMask) >> 16;
        for (var i = 0; i < rank; i++)
        {
            length *= MemoryMarshal.Read<int>(boundsHeap);
            boundsHeap = boundsHeap[8..]; //Move to the next bound
        }

        return length;
    }

    public ulong ReadPointer(Span<byte> from)
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

        var sectionIdx = BinarySearchForRelevantHeapSection(ptr);
        if (sectionIdx == -1)
            return false;

        var section = sections[sectionIdx];

        //Move start of span to the offset relative to the start of the heap section
        var offset = ptr - section.VirtualAddress;

        if (offset >= int.MaxValue)
        {
            //more than 2GiB above the current heap section -> probably not managed heap
            return false;
        }

        var intOffset = (int)offset;
        var sectionSpan = section.Heap.AsSpan();

        if (sectionSpan.Length < intOffset)
            return false;

        heapSection = sectionSpan[intOffset..];

        return true;
    }

    private int BinarySearchForRelevantHeapSection(ulong address)
    {
        var first = 0;
        var last = ManagedHeapSectionStartAddresses.Length - 1;
        do
        {
            var mid = first + (last - first) / 2;

            var range = ManagedHeapSectionStartAddresses[mid];
            if (address < range.VirtualAddressEnd && address >= range.VirtualAddress)
                return mid;

            if (address >= range.VirtualAddressEnd)
                first = mid + 1;
            else
                last = mid - 1;
        } while (first <= last);

        return -1;
    }

    public BasicTypeInfoCache GetTypeInfo(int typeIndex)
    {
        if (_typeInfoCacheByTypeIndex.TryGetValue(typeIndex, out var ret))
            return ret;
        
        var info = new BasicTypeInfoCache
        {
            TypeIndex = typeIndex,
            BaseTypeIndex = ReadSingleValueType<int>(EntryType.TypeDescriptions_BaseOrElementTypeIndex, typeIndex)
        };

        _typeInfoCacheByTypeIndex[typeIndex] = info;
        return info;
    }

    public BasicFieldInfoCache[] GetInstanceFieldInfoForTypeIndex(int typeIndex)
    {
        if(_nonStaticFieldIndicesByTypeIndex.TryGetValue(typeIndex, out var indices))
            return indices;
        
        _nonStaticFieldIndicesByTypeIndex[typeIndex] = indices = WalkFieldInfoForTypeIndex(typeIndex, false).ToArray();

        return indices;
    }
    
    public BasicFieldInfoCache[] GetStaticFieldInfoForTypeIndex(int typeIndex)
    {
        if(_staticFieldIndicesByTypeIndex.TryGetValue(typeIndex, out var indices))
            return indices;
        
        _staticFieldIndicesByTypeIndex[typeIndex] = indices = WalkFieldInfoForTypeIndex(typeIndex, true).ToArray();

        return indices;
    }

    public IEnumerable<BasicFieldInfoCache> WalkFieldInfoForTypeIndex(int typeIndex, bool wantStatic)
    {
        if (!wantStatic)
        {
            var baseTypeIndex = ReadSingleValueType<int>(EntryType.TypeDescriptions_BaseOrElementTypeIndex, typeIndex);
            if (baseTypeIndex != -1)
            {
                foreach (var i in GetInstanceFieldInfoForTypeIndex(baseTypeIndex))
                    yield return i;
            }
        }

        var fieldIndices = ReadSingleValueTypeArrayChapterElement<int>(EntryType.TypeDescriptions_FieldIndices, typeIndex).ToArray();
        foreach (var fieldIndex in fieldIndices)
        {
            var isStatic = ReadSingleValueType<bool>(EntryType.FieldDescriptions_IsStatic, fieldIndex);
            if (isStatic != wantStatic)
                continue;
            
            var fieldOffset = ReadSingleValueType<int>(EntryType.FieldDescriptions_Offset, fieldIndex);
            var type = ReadSingleValueType<int>(EntryType.FieldDescriptions_TypeIndex, fieldIndex);
            var typeFlags = ReadSingleValueType<TypeFlags>(EntryType.TypeDescriptions_Flags, type);
            var typeSize = ReadSingleValueType<int>(EntryType.TypeDescriptions_Size, type);
            
            yield return new()
            {
                TypeDescriptionIndex = type,
                Flags = typeFlags,
                FieldTypeSize = typeSize,
                FieldIndex = fieldIndex,
                FieldOffset = fieldOffset,
            };
        }
    }

    public string GetTypeName(int typeIndex)
    {
        if (_typeNamesByTypeIndex.TryGetValue(typeIndex, out var ret))
            return ret;

        _typeNamesByTypeIndex[typeIndex] = ret = ReadSingleStringFromChapter(EntryType.TypeDescriptions_Name, typeIndex);
        return ret;
    }
    
    public string GetFieldName(int fieldIndex)
    {
        if (_fieldNamesByFieldIndex.TryGetValue(fieldIndex, out var ret))
            return ret;

        _fieldNamesByFieldIndex[fieldIndex] = ret = ReadSingleStringFromChapter(EntryType.FieldDescriptions_Name, fieldIndex);
        return ret;
    }
}