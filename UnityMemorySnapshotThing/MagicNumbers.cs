namespace UnityMemorySnapshotThing;

public static class MagicNumbers
{
    public const uint HeaderMagic = 0xAEABCDCD;
    public const uint DirectoryMagic = 0xCDCDAEAB;
    public const uint FooterMagic = 0xABCDCDAE;
    
    public const uint SupportedDirectoryVersion = 0x20170724;
    public const uint SupportedBlockSectionVersion = 0x20170724;
}