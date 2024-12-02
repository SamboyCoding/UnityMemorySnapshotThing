using UMS.LowLevel.Structures;

namespace UMS.Analysis.Structures;

public class WellKnownTypeHelper
{
    private readonly Dictionary<string, int> WellKnownTypes = new Dictionary<string, int>()
    {
        { "System.String", -1 },
        { "System.Int16", -1 },
        { "System.UInt16", -1 },
        { "System.Int32", -1 },
        { "System.UInt32", -1 },
        { "System.Int64", -1 },
        { "System.UInt64", -1 },
        { "System.Byte", -1 },
        { "System.SByte", -1 },
        { "System.Object", -1 },
        { "System.ValueType", -1 },
        { "System.Enum", -1 },
        { "System.Boolean", -1 },
        { "System.Char", -1 },
        { "System.Single", -1 },
        { "System.Double", -1 },
        { "System.IntPtr", -1 },
        { "UnityEngine.Object", -1 },
        { "UnityEngine.MonoBehaviour", -1 },
        { "UnityEngine.Component", -1 },
    };

    public int this[string value] => WellKnownTypes[value];
    
    public int String {get;}
    public int Int16 {get;}
    public int UInt16 {get;}
    public int Int32 {get;}
    public int UInt32 {get;}
    public int Int64 {get;}
    public int UInt64 {get;}
    public int Byte {get;}
    public int SByte {get;}
    public int Object {get;}
    public int ValueType {get;}
    public int Enum {get;}
    public int Boolean {get;}
    public int Char {get;}
    public int Single {get;}
    public int Double {get;}
    public int UnityEngineObject {get;}
    public int IntPtr {get;}

    public WellKnownTypeHelper(SnapshotFile file)
    {
        var toFind = WellKnownTypes.Count;
        var found = 0;
        
        var tdn = file.TypeDescriptionNames;
        for (var index = 0; index < tdn.Length; index++)
        {
            var fileTypeDescriptionName = tdn[index];
            
            if (WellKnownTypes.ContainsKey(fileTypeDescriptionName))
            {
                found++;
                WellKnownTypes[fileTypeDescriptionName] = index;
                
                if (found == toFind)
                    break;
            }
        }
        
        Console.WriteLine($"Found {found}/{toFind} well known types");
        
        String = this["System.String"];
        Int16 = this["System.Int16"];
        UInt16 = this["System.UInt16"];
        Int32 = this["System.Int32"];
        UInt32 = this["System.UInt32"];
        Int64 = this["System.Int64"];
        UInt64 = this["System.UInt64"];
        Byte = this["System.Byte"];
        SByte = this["System.SByte"];
        Object = this["System.Object"];
        ValueType = this["System.ValueType"];
        Enum = this["System.Enum"];
        Boolean = this["System.Boolean"];
        Char = this["System.Char"];
        Single = this["System.Single"];
        Double = this["System.Double"];
        UnityEngineObject = this["UnityEngine.Object"];
        IntPtr = this["System.IntPtr"];
    }
}