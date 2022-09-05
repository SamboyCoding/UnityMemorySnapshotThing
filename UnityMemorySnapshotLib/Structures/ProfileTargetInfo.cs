using System.Text;
#pragma warning disable CS0169, CS0649 // Field never used, field never assigned

namespace UnityMemorySnapshotLib.Structures;

// ReSharper disable UnassignedField.Global
public unsafe struct ProfileTargetInfo
{
    public uint SessionGuid;
    public RuntimePlatform Platform;
    public GraphicsDeviceType GraphicsDeviceType;
    public ulong TotalPhysicalMemory;
    public ulong TotalGraphicsMemory;
    public ScriptingImplementation ScriptingBackend;
    public double TimeSinceStartup;
    private uint _unityVersionLength;
    private fixed byte _unityVersion[16];
    private uint _productNameLength;
    private fixed byte _productName[256];
    private fixed byte _padding[192];

    public override string ToString()
    {
        return $"SessionGUID: {SessionGuid}, Platform: {Platform}, GraphicsDeviceType: {GraphicsDeviceType}, TotalPhysicalMemory: {TotalPhysicalMemory}, TotalGraphicsMemory: {TotalGraphicsMemory}, ScriptingBackend: {ScriptingBackend}, TimeSinceStartup: {TimeSinceStartup}, UnityVersion: {UnityVersionString}, ProductName: {ProductNameString}";
    }

    public string UnityVersionString
    {
        get
        {
            fixed(byte* ptr = _unityVersion)
            {
                return Encoding.UTF8.GetString(ptr, (int)_unityVersionLength);
            }
        }
    }
    
    public string ProductNameString
    {
        get
        {
            fixed(byte* ptr = _productName)
            {
                return Encoding.UTF8.GetString(ptr, (int)_productNameLength);
            }
        }
    }
}