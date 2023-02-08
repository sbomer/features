using System.Runtime.Versioning;

class IOS {
    public static void Helper() {
        if (OperatingSystem.IsIOS()) {
            ApiOnlySupportedOnMacCatalyst();
        }

        ApiOnlySupportedOnMacCatalyst();
    }

    [SupportedOSPlatform("MacCatalyst")]
    public static void ApiOnlySupportedOnMacCatalyst() {}
}

class OperatingSystem {
    [SupportedOSPlatformGuard("maccatalyst")]
    public static bool IsIOS() => false;
}