namespace UMS.LowLevel.Structures.FileStructure;

public enum EntryFormat : ushort
{
    Undefined,
    SingleElement,
    ConstantSizeElementArray,
    DynamicSizeElementArray,
}