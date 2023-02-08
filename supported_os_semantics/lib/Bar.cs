namespace Lib;

public class Bar
{
    [FeatureCheck(nameof(Bar))]
    public static bool IsSupported =>
#if FEATURE_BAR
        true;
#else
        false;
#endif

    [RequiresFeature(nameof(Bar))]
    public static void Use() =>
#if FEATURE_BAR
    Console.WriteLine("Bar.Use");
#else
    throw new NotSupportedException();
#endif
}

