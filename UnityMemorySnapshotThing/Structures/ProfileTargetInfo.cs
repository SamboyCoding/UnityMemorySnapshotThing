using System.Text;

namespace UnityMemorySnapshotThing.Structures;

public unsafe struct ProfileTargetInfo
{
    public uint SessionGUID;
    public RuntimePlatform Platform;
    public GraphicsDeviceType GraphicsDeviceType;
    public ulong TotalPhysicalMemory;
    public ulong TotalGraphicsMemory;
    public ScriptingImplementation ScriptingBackend;
    public double TimeSinceStartup;
    private uint UnityVersionLength;
    private fixed byte UnityVersion[16];
    private uint ProductNameLength;
    private fixed byte ProductName[256];
    private fixed byte Padding[192];

    public override string ToString()
    {
        return $"SessionGUID: {SessionGUID}, Platform: {Platform}, GraphicsDeviceType: {GraphicsDeviceType}, TotalPhysicalMemory: {TotalPhysicalMemory}, TotalGraphicsMemory: {TotalGraphicsMemory}, ScriptingBackend: {ScriptingBackend}, TimeSinceStartup: {TimeSinceStartup}, UnityVersion: {UnityVersionString}, ProductName: {ProductNameString}";
    }

    public string UnityVersionString
    {
        get
        {
            fixed(byte* ptr = UnityVersion)
            {
                return Encoding.UTF8.GetString(ptr, (int)UnityVersionLength);
            }
        }
    }
    
    public string ProductNameString
    {
        get
        {
            fixed(byte* ptr = ProductName)
            {
                return Encoding.UTF8.GetString(ptr, (int)ProductNameLength);
            }
        }
    }
}