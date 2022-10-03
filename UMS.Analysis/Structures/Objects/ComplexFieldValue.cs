using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures.Objects;

public struct ComplexFieldValue : IFieldValue
{
    public bool IsNull { get; }
    public bool FailedToParse { get; }
    public ulong FailedParseFromPtr { get; }
    
    public ManagedClassInstance? Value { get; }

    public ComplexFieldValue(SnapshotFile file, BasicFieldInfoCache info, ManagedClassInstance parent, Span<byte> data, int depth)
    {
        IsNull = false;
        FailedToParse = false;
        FailedParseFromPtr = 0;
        Value = null;
        
        if ((info.Flags & TypeFlags.ValueType) == TypeFlags.ValueType)
        {
            var size = info.FieldTypeSize;
            if (size > 0) //Have observed a negative size (-8), MIGHT be for pointers to value types, so we'll fall back to the below logic.
            {
                var valueTypeData = data[..size];
                Value = new(file, info.TypeDescriptionIndex, info.Flags, size, data, parent, depth);
                return;
            }
        }
            
        //General object handling
        var ptr = BitConverter.ToUInt64(data);

        if (ptr == 0)
        {
            //Null
            IsNull = true;
            return;
        }

        var mci = file.GetManagedClassInstance(ptr, parent, depth);

        if (mci == null)
        {
            FailedToParse = true;
            FailedParseFromPtr = ptr;
            return;
        }
        
        Value = mci;
    }
}