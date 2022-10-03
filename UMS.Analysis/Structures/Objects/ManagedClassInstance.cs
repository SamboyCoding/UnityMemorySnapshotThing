using System.Runtime.CompilerServices;
using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures.Objects;

public readonly struct ManagedClassInstance
{
    private readonly object? _parent;
    private readonly ulong _objectAddress;

    public readonly BasicTypeInfoCache TypeInfo;
    public readonly IFieldValue[] Fields;
    public readonly TypeFlags TypeDescriptionFlags;
    
    private readonly bool IsInitialized;

    private ManagedClassInstance TypedParent => _parent == null ? default : Unsafe.Unbox<ManagedClassInstance>(_parent!);

    public ManagedClassInstance(SnapshotFile file, int typeDescriptionIndex, TypeFlags flags, int size, Span<byte> data, ManagedClassInstance parent, int depth)
    {
        if ((flags & TypeFlags.ValueType) != TypeFlags.ValueType)
            throw new("This constructor can only be used for value types");
        
        _parent = parent;
        _objectAddress = 0;
        TypeInfo = file.GetTypeInfo(typeDescriptionIndex);
        TypeDescriptionFlags = flags;
        IsInitialized = true;

        if (IsEnumType(file))
        {
            Fields = new IFieldValue[1];

            var value = size switch
            {
                1 => data[0],
                2 => BitConverter.ToInt16(data),
                4 => BitConverter.ToInt32(data),
                8 => BitConverter.ToInt64(data),
                _ => throw new("Invalid enum size")
            };

            Fields[0] = new IntegerFieldValue(value);
            return;
        }

        Fields = ReadFields(file, data, depth);
    }
    
    public ManagedClassInstance(SnapshotFile file, RawManagedObjectInfo info, ManagedClassInstance? parent = null, int depth = 0)
    {
        _parent = parent;
        _objectAddress = info.SelfAddress;
        TypeInfo = file.GetTypeInfo(info.TypeDescriptionIndex);
        TypeDescriptionFlags = info.Flags;
        IsInitialized = true;

        var data = info.Data;

        if ((TypeDescriptionFlags & TypeFlags.Array) == TypeFlags.Array)
        {
            //TODO array items
            Fields = Array.Empty<IFieldValue>();
            return;
        }

        Fields = ReadFields(file, data, depth);
    }

    private IFieldValue[] ReadFields(SnapshotFile file, Span<byte> data, int depth)
    {
        if (CheckIfRecursiveReference())
            return Array.Empty<IFieldValue>();

        if (depth > 250)
        {
            Console.WriteLine($"Stopped reading fields due to too-deeply nested object at depth {depth}");
            return Array.Empty<IFieldValue>();
        }

        var fieldInfo = file.GetFieldInfoForTypeIndex(TypeInfo.TypeIndex);

        var fields = new IFieldValue[fieldInfo.Length];
        
        var isValueType = (TypeDescriptionFlags & TypeFlags.ValueType) == TypeFlags.ValueType;

        for (var index = 0; index < fieldInfo.Length; index++)
        {
            var info = fieldInfo[index];
            var fieldOffset = info.FieldOffset;

            if (isValueType)
                fieldOffset -= file.VirtualMachineInformation.ObjectHeaderSize;

            var fieldPtr = data[fieldOffset..];

            //For all integer types, we just handle unsigned as signed
            if (info.TypeDescriptionIndex == file.WellKnownTypes.String)
                fields[index] = new StringFieldValue(file, fieldPtr);
            else if (info.TypeDescriptionIndex == file.WellKnownTypes.Boolean || info.TypeDescriptionIndex == file.WellKnownTypes.Byte)
                fields[index] = new IntegerFieldValue(fieldPtr[0]);
            else if (info.TypeDescriptionIndex == file.WellKnownTypes.Int16 || info.TypeDescriptionIndex == file.WellKnownTypes.UInt16 || info.TypeDescriptionIndex == file.WellKnownTypes.Char)
                fields[index] = new IntegerFieldValue(BitConverter.ToInt16(fieldPtr));
            else if (info.TypeDescriptionIndex == file.WellKnownTypes.Int32 || info.TypeDescriptionIndex == file.WellKnownTypes.UInt32)
                fields[index] = new IntegerFieldValue(BitConverter.ToInt32(fieldPtr));
            else if (info.TypeDescriptionIndex == file.WellKnownTypes.Int64 || info.TypeDescriptionIndex == file.WellKnownTypes.UInt64 || info.TypeDescriptionIndex == file.WellKnownTypes.IntPtr)
                fields[index] = new IntegerFieldValue(BitConverter.ToInt64(fieldPtr));
            else if (info.TypeDescriptionIndex == file.WellKnownTypes.Single)
                fields[index] = new FloatingPointFieldValue(BitConverter.ToSingle(fieldPtr));
            else if (info.TypeDescriptionIndex == file.WellKnownTypes.Double)
                fields[index] = new FloatingPointFieldValue(BitConverter.ToDouble(fieldPtr));
            else
                fields[index] = new ComplexFieldValue(file, info, this, fieldPtr, depth + 1);
        }

        return fields;
    }

    private bool IsEnumType(SnapshotFile file) 
        => TypeInfo.BaseTypeIndex == file.WellKnownTypes.Enum;

    private bool CheckIfRecursiveReference()
    {
        var parent = TypedParent;
        while (parent.IsInitialized)
        {
            if (parent._objectAddress == _objectAddress)
                return true;
            
            parent = parent.TypedParent;
        }

        return false;
    }

    public bool InheritsFromUnityEngineObject(SnapshotFile file)
    {
        if((TypeDescriptionFlags & TypeFlags.Array) == TypeFlags.Array)
            return false;
        
        var parent = TypeInfo.BaseTypeIndex;
        while (parent != -1)
        {
            if (parent == file.WellKnownTypes.UnityEngineObject)
                return true;

            parent = file.GetTypeInfo(parent).BaseTypeIndex;
        }

        return false;
    }
}