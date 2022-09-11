namespace UMS.LowLevel.Structures;

[Flags]
public enum ObjectFlags
{
    None = 0,
    IsDontDestroyOnLoad = 0x1,
    IsPersistent = 0x2,
    IsManager = 0x4,
}