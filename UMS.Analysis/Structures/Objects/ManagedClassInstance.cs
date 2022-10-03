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

        var fieldInfo = file.GetInstanceFieldInfoForTypeIndex(TypeInfo.TypeIndex);

        var fields = new IFieldValue[fieldInfo.Length];
        
        var isValueType = IsValueType;

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
                //TODO
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(child), "Invalid LoadedReason");
        }
    }
}