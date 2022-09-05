namespace UnityMemorySnapshotThing.Structures.LowLevel;

public enum EntryType : ushort
{
    Metadata_Version = 0,
    Metadata_RecordDate,
    Metadata_UserMetadata,
    Metadata_CaptureFlags,
    Metadata_VirtualMachineInformation,
    NativeTypes_Name, //5
    NativeTypes_NativeBaseTypeArrayIndex,
    NativeObjects_NativeTypeArrayIndex,
    NativeObjects_HideFlags,
    NativeObjects_Flags,
    NativeObjects_InstanceId, //10
    NativeObjects_Name,
    NativeObjects_NativeObjectAddress,
    NativeObjects_Size,
    NativeObjects_RootReferenceId,
    GCHandles_Target, //15
    Connections_From,
    Connections_To,
    ManagedHeapSections_StartAddress,
    ManagedHeapSections_Bytes,
    ManagedStacks_StartAddress, //20
    ManagedStacks_Bytes,
    TypeDescriptions_Flags,
    TypeDescriptions_Name,
    TypeDescriptions_Assembly,
    TypeDescriptions_FieldIndices, //25
    TypeDescriptions_StaticFieldBytes,
    TypeDescriptions_BaseOrElementTypeIndex,
    TypeDescriptions_Size,
    TypeDescriptions_TypeInfoAddress,
    TypeDescriptions_TypeIndex, //30
    FieldDescriptions_Offset,
    FieldDescriptions_TypeIndex,
    FieldDescriptions_Name,
    FieldDescriptions_IsStatic,
    NativeRootReferences_Id, //35
    NativeRootReferences_AreaName, 
    NativeRootReferences_ObjectName,
    NativeRootReferences_AccumulatedSize,
    NativeAllocations_MemoryRegionIndex,
    NativeAllocations_RootReferenceId, //40
    NativeAllocations_AllocationSiteId,
    NativeAllocations_Address,
    NativeAllocations_Size,
    NativeAllocations_OverheadSize,
    NativeAllocations_PaddingSize, //45
    NativeMemoryRegions_Name,
    NativeMemoryRegions_ParentIndex,
    NativeMemoryRegions_AddressBase,
    NativeMemoryRegions_AddressSize,
    NativeMemoryRegions_FirstAllocationIndex, //50
    NativeMemoryRegions_NumAllocations,
    NativeMemoryLabels_Name,
    NativeAllocationSites_Id,
    NativeAllocationSites_MemoryLabelIndex,
    NativeAllocationSites_CallstackSymbols, //55
    NativeCallstackSymbol_Symbol,
    NativeCallstackSymbol_ReadableStackTrace,
    NativeObjects_GCHandleIndex,
    ProfileTarget_Info,
    ProfileTarget_MemoryStats, //60
    NativeMemoryLabels_Size,
    SceneObjects_Name,
    SceneObjects_Path,
    SceneObjects_AssetPath,
    SceneObjects_BuildIndex, //65
    SceneObjects_RootIdCounts,
    SceneObjects_RootIdOffsets,
    SceneObjects_RootIds,

    // GfxResourceReferencesAndAllocatorsVersion = 14
    // Added gfx resource to root mapping and allocators information (including extra allocator information to memory labels)
    NativeMemoryLabels_AllocatorIdentifier,
    NativeGfxResourceReferences_Id, //70
    NativeGfxResourceReferences_Size,
    NativeGfxResourceReferences_RootId,
    NativeAllocatorInfo_AllocatorName,
    NativeAllocatorInfo_Identifier,
    NativeAllocatorInfo_UsedSize, //75
    NativeAllocatorInfo_ReservedSize,
    NativeAllocatorInfo_OverheadSize,
    NativeAllocatorInfo_PeakUsedSize,
    NativeAllocatorInfo_AllocationCount,
    NativeAllocatorInfo_Flags, //80
    Count, //used to keep track of entry count, only add c++ matching entries above this one
}