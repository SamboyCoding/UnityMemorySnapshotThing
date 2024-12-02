namespace UMS.Analysis.Structures.Objects;

public interface IFieldValue
{
    public bool IsNull { get; }
    public bool FailedToParse { get; }
    public ulong FailedParseFromPtr { get; }

    public static IFieldValue Read(SnapshotFile file, BasicFieldInfoCache info, Span<byte> fieldData, int depth, LoadedReason loadedReason, ManagedClassInstance? parent = null)
    {
        //For all integer types, we just handle unsigned as signed
        if (info.TypeDescriptionIndex == file.WellKnownTypes.String)
            return new StringFieldValue(file, fieldData);
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Boolean || info.TypeDescriptionIndex == file.WellKnownTypes.Byte || info.TypeDescriptionIndex == file.WellKnownTypes.SByte)
            return new IntegerFieldValue(fieldData[0]);
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Int16 || info.TypeDescriptionIndex == file.WellKnownTypes.UInt16 || info.TypeDescriptionIndex == file.WellKnownTypes.Char)
            return new IntegerFieldValue(BitConverter.ToInt16(fieldData));
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Int32 || info.TypeDescriptionIndex == file.WellKnownTypes.UInt32)
            return new IntegerFieldValue(BitConverter.ToInt32(fieldData));
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Int64 || info.TypeDescriptionIndex == file.WellKnownTypes.UInt64 || info.TypeDescriptionIndex == file.WellKnownTypes.IntPtr)
            return new IntegerFieldValue(BitConverter.ToInt64(fieldData));
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Single)
            return new FloatingPointFieldValue(BitConverter.ToSingle(fieldData));
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Double)
            return new FloatingPointFieldValue(BitConverter.ToDouble(fieldData));
        
        //Check for enums
        var typeInfo = file.GetTypeInfo(info.TypeDescriptionIndex);
        if(typeInfo.BaseTypeIndex == file.WellKnownTypes.Enum)
            return new EnumFieldValue(file, typeInfo, fieldData);
        
        return new ComplexFieldValue(file, info, parent, fieldData, depth + 1, loadedReason);
    }
}