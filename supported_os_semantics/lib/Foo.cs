namespace Lib;

public class Foo
{
    [FeatureCheck(nameof(Foo))]
    public static bool IsSupported => true;

    [RequiresFeature(nameof(Foo))]
    public static void Use() => Console.WriteLine("Foo.Use");
}
