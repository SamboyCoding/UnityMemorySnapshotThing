namespace UMS.Analysis.Structures;

public enum LoadedReason : byte
{
    GcRoot,
    StaticField,
    InstanceField,
    ArrayElement
}