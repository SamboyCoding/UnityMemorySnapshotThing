namespace UMS.Analysis.Structures.Objects;

public class EnumFieldValue : IFieldValue
{
    public bool IsNull { get; }
    public bool FailedToParse { get; }
    public ulong FailedParseFromPtr { get; }
    
    private SnapshotFile _file;

    private int _enumTypeIndex;
    
    private long _value;

    public EnumFieldValue(SnapshotFile file, BasicTypeInfoCache typeInfo, Span<byte> fieldData)
    {
        var allFields = file.GetInstanceFieldInfoForTypeIndex(typeInfo.TypeIndex);
        var valueField = allFields.Single();
        _enumTypeIndex = typeInfo.TypeIndex;
        _file = file;
        
        var backingTypeIndex = valueField.TypeDescriptionIndex;
        
        if (backingTypeIndex == file.WellKnownTypes.Boolean || backingTypeIndex == file.WellKnownTypes.Byte || backingTypeIndex == file.WellKnownTypes.SByte)
            _value = fieldData[0];
        else if (backingTypeIndex == file.WellKnownTypes.Int16 || backingTypeIndex == file.WellKnownTypes.UInt16 || backingTypeIndex == file.WellKnownTypes.Char)
            _value = BitConverter.ToInt16(fieldData);
        else if (backingTypeIndex == file.WellKnownTypes.Int32 || backingTypeIndex == file.WellKnownTypes.UInt32)
            _value = BitConverter.ToInt32(fieldData);
        else if (backingTypeIndex == file.WellKnownTypes.Int64 || backingTypeIndex == file.WellKnownTypes.UInt64 || backingTypeIndex == file.WellKnownTypes.IntPtr)
            _value = BitConverter.ToInt64(fieldData);
        else 
            throw new NotImplementedException($"Enum backing type {file.GetTypeName(backingTypeIndex)} not implemented");

        //TODO Maybe consider getting enum names here
        
        IsNull = false;
        FailedToParse = false;
        FailedParseFromPtr = 0;
    }

    public override string ToString()
    {
        var enumName = _file.GetTypeName(_enumTypeIndex);
        
        return $"({enumName}) {_value}";
    }
}