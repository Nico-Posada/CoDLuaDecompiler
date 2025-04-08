namespace CoDLuaDecompiler.Common;

public static class AppInfo
{
    public static string Version { get; } = "2.4.2";
    public static bool ShowFunctionData { get; set; } = false;
    public static bool ShowHashType { get; set; } = false;
    public static string? OutputDirectory { get; set; } = null;
    public static ulong HashMask { get; set; } = ~0UL;
}
