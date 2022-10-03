using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures;

public struct RawManagedObjectInfo
{
    public ulong SelfAddress;
    public ulong TypeInfoAddress;
    
    public int NativeObjectIndex;
    public int ManagedObjectIndex;
    public int TypeDescriptionIndex;
    public int Size;
    public int RefCount;
    public TypeFlags Flags;

    public byte[] Data;

    public RawManagedObjectInfo()
    {
        SelfAddress = 0;
        TypeInfoAddress = 0;
        
        NativeObjectIndex = -1;
        ManagedObjectIndex = -1;
        TypeDescriptionIndex = -1;
        
        Size = -1;
        RefCount = -1;
        Data = Array.Empty<byte>();
        Flags = default;
    }


    public bool IsKnownType => TypeDescriptionIndex >= 0;
    
}