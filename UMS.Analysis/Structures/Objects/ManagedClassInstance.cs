using System.Runtime.CompilerServices;
using System.Text;
using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures.Objects;

public readonly struct ManagedClassInstance
{
    private readonly object? _parent;
    public readonly ulong ObjectAddress;
    
    public readonly BasicTypeInfoCache TypeInfo;
    public readonly IFieldValue[] Fields;
    
    public readonly TypeFlags TypeDescriptionFlags;
    public int FieldIndexOrArrayOffset { get; init; }
    
    private readonly bool IsInitialized;
    public readonly LoadedReason LoadedReason;

    private ManagedClassInstance TypedParent => _parent == null ? default : Unsafe.Unbox<ManagedClassInstance>(_parent!);

    public bool IsArray => (TypeDescriptionFlags & TypeFlags.Array) == TypeFlags.Array;
    
    public bool IsValueType => (TypeDescriptionFlags & TypeFlags.ValueType) == TypeFlags.ValueType;

    public ManagedClassInstance(SnapshotFile file, int typeDescriptionIndex, TypeFlags flags, int size, Span<byte> data, ManagedClassInstance parent, int depth, LoadedReason loadedReason, int fieldIndexOrArrayOffset = int.MinValue)
    {
        TypeDescriptionFlags = flags;
        
        if (!IsValueType)
            throw new("This constructor can only be used for value types");
        
        _parent = parent;
        ObjectAddress = 0;
        TypeInfo = file.GetTypeInfo(typeDescriptionIndex);
        IsInitialized = true;
        LoadedReason = loadedReason;
        FieldIndexOrArrayOffset = fieldIndexOrArrayOffset;
        
        if(LoadedReason != LoadedReason.GcRoot && FieldIndexOrArrayOffset == int.MinValue)
            throw new("FieldIndexOrArrayOffset must be set for non-GcRoot instances");

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
    
    public ManagedClassInstance(SnapshotFile file, RawManagedObjectInfo info, ManagedClassInstance? parent = null, int depth = 0, LoadedReason loadedReason = LoadedReason.GcRoot, int fieldIndexOrArrayOffset = int.MinValue)
    {
        _parent = parent;
        ObjectAddress = info.SelfAddress;
        TypeInfo = file.GetTypeInfo(info.TypeDescriptionIndex);
        TypeDescriptionFlags = info.Flags;
        IsInitialized = true;
        LoadedReason = loadedReason;
        FieldIndexOrArrayOffset = fieldIndexOrArrayOffset;
        
        if(LoadedReason != LoadedReason.GcRoot && FieldIndexOrArrayOffset == int.MinValue)
            throw new("FieldIndexOrArrayOffset must be set for non-GcRoot instances");

        var data = info.Data;

        if (IsArray)
        {
            var arrayElementCount = file.ReadArrayLength(TypeDescriptionFlags, data);
            
            if (arrayElementCount == 0)
            {
                Fields = Array.Empty<IFieldValue>();
                return;
            }

            var typeInfo = file.GetTypeInfo(info.TypeDescriptionIndex);

            if (typeInfo.BaseTypeIndex < 0)
            {
                Console.WriteLine("WARNING: Skipping uninitialized array type");
                Fields = Array.Empty<IFieldValue>();
                return;
            }
            
            var elementType = file.GetTypeInfo(typeInfo.BaseTypeIndex);
            var elementFlags = file.GetTypeFlagsByIndex(elementType.TypeIndex);
            var elementTypeSize = (elementFlags & TypeFlags.ValueType) != 0 ? file.GetTypeDescriptionSizeBytes(elementType.TypeIndex) : 8;
            var arrayData = info.Data.AsSpan(file.VirtualMachineInformation.ArrayHeaderSize..);

            Fields = new IFieldValue[arrayElementCount];
            for (var i = 0; i < arrayElementCount; i++)
            {
                var elementData = arrayData[(i * elementTypeSize)..];
                Fields[i] = ReadFieldValue(file, elementData, depth, elementFlags, elementTypeSize, elementType.TypeIndex, i);
            }

            return;
        }

        Fields = ReadFields(file, data, depth);
    }

    private IFieldValue[] ReadFields(SnapshotFile file, Span<byte> data, int depth)
    {
        if (CheckIfRecursiveReference())
            return Array.Empty<IFieldValue>();

        if (depth > 350)
        {
            Console.WriteLine($"Stopped reading fields due to too-deeply nested object at depth {depth} (this object is of type {file.GetTypeName(TypeInfo.TypeIndex)}, parent is of type {file.GetTypeName(TypedParent.TypeInfo.TypeIndex)})");
            return Array.Empty<IFieldValue>();
        }

        var fieldInfo = file.GetInstanceFieldInfoForTypeIndex(TypeInfo.TypeIndex);

        var fields = new IFieldValue[fieldInfo.Length];
        
        var isValueType = IsValueType;

        for (var index = 0; index < fieldInfo.Length; index++)
        {
            var info = fieldInfo[index];
            var fieldOffset = info.FieldOffset;

            if (isValueType)
                fieldOffset -= file.VirtualMachineInformation.ObjectHeaderSize;

            var fieldData = data[fieldOffset..];
            
            fields[index] = ReadFieldValue(file, info, fieldData, depth, false);
        }

        return fields;
    }

    private IFieldValue ReadFieldValue(SnapshotFile file, BasicFieldInfoCache info, Span<byte> fieldData, int depth, bool array)
    {
        //For all integer types, we just handle unsigned as signed
        if (info.TypeDescriptionIndex == file.WellKnownTypes.String)
            return new StringFieldValue(file, fieldData);
        if (info.TypeDescriptionIndex == file.WellKnownTypes.Boolean || info.TypeDescriptionIndex == file.WellKnownTypes.Byte)
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
        
        return new ComplexFieldValue(file, info, this, fieldData, depth + 1, array);
    }

    private IFieldValue ReadFieldValue(SnapshotFile file, Span<byte> fieldData, int depth, TypeFlags fieldTypeFlags, int fieldTypeSize, int fieldTypeIndex, int arrayOffset)
    {
        BasicFieldInfoCache info = new()
        {
            Flags = fieldTypeFlags,
            FieldIndex = arrayOffset,
            TypeDescriptionIndex = fieldTypeIndex,
            FieldTypeSize = fieldTypeSize,
        };
        
        return ReadFieldValue(file, info, fieldData, depth, true);
    }

    private bool IsEnumType(SnapshotFile file) 
        => TypeInfo.BaseTypeIndex == file.WellKnownTypes.Enum;

    private bool CheckIfRecursiveReference()
    {
        var parent = TypedParent;
        while (parent.IsInitialized)
        {
            if (parent.ObjectAddress == ObjectAddress)
                return true;
            
            parent = parent.TypedParent;
        }

        return false;
    }

    public bool InheritsFromUnityEngineObject(SnapshotFile file) 
        => InheritsFrom(file, file.WellKnownTypes.UnityEngineObject);

    public bool InheritsFrom(SnapshotFile file, int baseTypeIndex)
    {
        if((TypeDescriptionFlags & TypeFlags.Array) == TypeFlags.Array)
            return false;
        
        var baseClass = TypeInfo.BaseTypeIndex;
        while (baseClass != -1)
        {
            if (baseClass == baseTypeIndex)
                return true;

            baseClass = file.GetTypeInfo(baseClass).BaseTypeIndex;
        }

        return false;
    }

    public string GetFirstObservedRetentionPath(SnapshotFile file)
    {
        var name = file.GetTypeName(TypeInfo.TypeIndex);

        var sb = new StringBuilder(name);
        sb.Append(" at 0x").Append(ObjectAddress.ToString("X"));
        sb.Append(" (target) <- ");

        var parent = TypedParent;
        AppendRetentionReason(sb, file, this, parent);
        
        while (parent.IsInitialized)
        {
            var child = parent;
            parent = child.TypedParent;
            AppendRetentionReason(sb, file, child, parent);
        }

        return sb.ToString();
    }

    private void AppendRetentionReason(StringBuilder sb, SnapshotFile file, ManagedClassInstance child, ManagedClassInstance parent)
    {
        switch (child.LoadedReason)
        {
            case LoadedReason.GcRoot:
                sb.Append("GC Root");
                return;
            case LoadedReason.StaticField:
            {
                var staticFieldDeclaringType = file.StaticFieldsToOwningTypes[child.FieldIndexOrArrayOffset];
                var fieldList = file.GetStaticFieldInfoForTypeIndex(staticFieldDeclaringType);
                var parentName = file.GetTypeName(staticFieldDeclaringType);
                var field = fieldList.First(f => f.FieldIndex == child.FieldIndexOrArrayOffset);
                sb.Append("Static Field ").Append(file.GetFieldName(field.FieldIndex)).Append(" of ");
                sb.Append(parentName);
                break;
            }
            case LoadedReason.InstanceField:
            {
                var fieldList = file.GetInstanceFieldInfoForTypeIndex(parent.TypeInfo.TypeIndex);
                var parentName = file.GetTypeName(parent.TypeInfo.TypeIndex);
                var field = fieldList.First(f => f.FieldIndex == child.FieldIndexOrArrayOffset);
                sb.Append("Field ").Append(file.GetFieldName(field.FieldIndex)).Append(" of ");
                sb.Append(parentName).Append(" at 0x").Append(parent.ObjectAddress.ToString("X"));
                sb.Append(" <- ");
                break;
            }
            case LoadedReason.ArrayElement:
            {
                var parentName = file.GetTypeName(parent.TypeInfo.TypeIndex);
                sb.Append("Array Element ").Append(child.FieldIndexOrArrayOffset).Append(" of ");
                sb.Append(parentName).Append(" at 0x").Append(parent.ObjectAddress.ToString("X"));
                sb.Append(" <- ");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(child), "Invalid LoadedReason");
        }
    }
}