namespace UnityMemorySnapshotLib.Structures;

public enum RuntimePlatform
{
    // In the Unity editor on Mac OS X.
    OSXEditor = 0,

    // In the player on Mac OS X.
    OSXPlayer = 1,

    // In the player on Windows.
    WindowsPlayer = 2,

    //*undocumented*
    OSXWebPlayer = 3,

    // In the Dashboard widget on Mac OS X.
    OSXDashboardPlayer = 4,

    //*undocumented*
    WindowsWebPlayer = 5,

    // In the Unity editor on Windows.
    WindowsEditor = 7,

    // In the player on the iPhone.
    IPhonePlayer = 8,

    //*undocumented*
    XBOX360 = 10,

    //*undocumented*
    PS3 = 9,

    // In the player on Android devices.
    Android = 11,
    NaCl = 12,
    FlashPlayer = 15,

    //*undocumented*
    LinuxPlayer = 13,
    LinuxEditor = 16,
    WebGLPlayer = 17,

    //*undocumented*
    MetroPlayerX86 = 18,
    WSAPlayerX86 = 18,

    //*undocumented*
    MetroPlayerX64 = 19,
    WSAPlayerX64 = 19,

    //*undocumented*
    MetroPlayerARM = 20,
    WSAPlayerARM = 20,
    WP8Player = 21,
    BB10Player = 22,
    BlackBerryPlayer = 22,
    TizenPlayer = 23,

    // In the player on PS Vita
    PSP2 = 24,

    // In the player on PS4
    PS4 = 25,

    // In the player on PSM
    PSM = 26,

    // In the player on XboxOne
    XboxOne = 27,
    SamsungTVPlayer = 28,
    WiiU = 30,

    // tvOS
    tvOS = 31,

    // Nintendo Switch
    Switch = 32,

    // Lumin
    Lumin = 33,

    // BJM
    Stadia = 34,

    // Cloud Rendering
    CloudRendering = 35,

    // Game Core
    GameCoreScarlett = -1, // GameCoreScarlett renumbered here so it's destinct from GameCoreXboxSeries.
    GameCoreXboxSeries = 36,
    GameCoreXboxOne = 37,

    // PS5
    PS5 = 38,

    // Embedded Linux
    EmbeddedLinuxArm64 = 39,
    EmbeddedLinuxArm32 = 40,
    EmbeddedLinuxX64 = 41,
    EmbeddedLinuxX86 = 42,

    // Server (Standalone subtarget)
    LinuxServer = 43,
    WindowsServer = 44,
    OSXServer = 45,

    // QNX
    QNXArm32 = 46,
    QNXArm64 = 47,
    QNXX64 = 48,
    QNXX86 = 49,
}