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
        { "System.Object", -1 },
        { "System.ValueType", -1 },
        { "System.Enum", -1 },
        { "System.Bool", -1 },
        { "System.Char", -1 },
        { "System.Single", -1 },
        { "System.Double", -1 },
        { "System.IntPtr", -1 },
        { "UnityEngine.Object", -1 },
        { "UnityEngine.MonoBehavior", -1 },
        { "UnityEngine.Component", -1 },
    };

    public int this[string value] => WellKnownTypes[value];

    public int String => this["System.String"];

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
    }
}