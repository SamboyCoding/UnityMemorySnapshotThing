namespace UnityMemorySnapshotThing.Structures.LowLevel;

public enum EntryFormat : ushort
{
    Undefined,
    SingleElement,
    ConstantSizeElementArray,
    DynamicSizeElementArray,
}