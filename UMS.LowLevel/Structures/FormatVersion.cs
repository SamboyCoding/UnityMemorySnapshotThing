namespace UMS.LowLevel.Structures;

public enum FormatVersion : uint
{
    SnapshotMinSupportedFormatVersion = 8, //Added metadata to file, min supported version for capture
    NativeConnectionsAsInstanceIdsVersion = 10, //native object collection reworked, added new gchandleIndex array to native objects for fast managed object access (2019.3 or newer?)
    ProfileTargetInfoAndMemStatsVersion = 11, //added profile target info and memory summary struct (shortly before 2021.2.0a12 on 2021.2, backported together with v.12)
    MemLabelSizeAndHeapIdVersion = 12, //added gc heap / vm heap identification encoded within each heap address and memory label size reporting (2021.2.0a12, 2021.1.9, 2020.3.12f1, 2019.4.29f1 or newer)
    // below are present from 2022.2+
    SceneRootsAndAssetBundlesVersion = 13, //added scene roots and asset bundle relations (not yet landed)
    GfxResourceReferencesAndAllocatorsVersion = 14, //added gfx resource to root mapping and allocators information (including extra allocator information to memory labels)
    NativeObjectMetaDataVersion = 15, // adds the ability to get meta data for native objects
    SystemMemoryRegionsVersion = 16, // adds system memory regions information
    // below are present from 2023.1+
    SystemMemoryResidentPagesVersion = 17, // adds system memory resident pages information
}