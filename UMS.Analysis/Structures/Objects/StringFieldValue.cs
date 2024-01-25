using System.Runtime.InteropServices;
using System.Text;

namespace UMS.Analysis.Structures.Objects;

public struct StringFieldValue : IFieldValue
{
    public bool IsNull { get; }
    public bool FailedToParse { get; }
    public ulong FailedParseFromPtr { get; }
    
    public string? Value { get; }

    public StringFieldValue(SnapshotFile file, Span<byte> data)
    {
        var ptr = MemoryMarshal.Read<ulong>(data);

        IsNull = false;
        FailedToParse = false;
        FailedParseFromPtr = 0;
        Value = null;
            
        if (ptr == 0)
        {
            //Null
            IsNull = true;
            return;
        }

        var managedObjectInfo = file.ParseManagedObjectInfo(ptr);
        if (!managedObjectInfo.IsKnownType)
        {
            FailedToParse = true;
            FailedParseFromPtr = ptr;
            return;
        }

        data = managedObjectInfo.Data;

        var offset = file.VirtualMachineInformation.ObjectHeaderSize + 4;
        
        if(offset > data.Length)
        {
            FailedToParse = true;
            FailedParseFromPtr = ptr;
            return;
        }
        
        var stringPtr = data[offset..];

        var end = stringPtr.LastIndexOf(new byte[] { 0, 0 });
        var stringData = stringPtr[..end];
            
        Value = Encoding.Unicode.GetString(stringData);
    }
}