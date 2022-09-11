namespace UMS.Analysis.Structures;

public ref struct ManagedObjectInfo
{
    public ulong SelfAddress;
    public ulong TypeInfoAddress;
    
    public int NativeObjectIndex;
    public int ManagedObjectIndex;
    public int TypeDescriptionIndex;
    public int Size;
    public int RefCount;

    public Span<byte> Data;

    public ManagedObjectInfo()
    {
        SelfAddress = 0;
        TypeInfoAddress = 0;
        
        NativeObjectIndex = -1;
        ManagedObjectIndex = -1;
        TypeDescriptionIndex = -1;
        
        Size = -1;
        RefCount = -1;
        Data = default;
    }


    public bool IsKnownType => TypeDescriptionIndex >= 0;
    
}