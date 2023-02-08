using Lib;


// Baz.Use();


// Foo.Use();

// Bar.Use();


class Program {
    [RequiresFeature("Baz")]
    public static void Main() {
        // If running on MacCatalyst, all iOS APIs are OK to call.
        // Is that right?
        if (SupersetFeature.IsSupported)
            SubsetFeature.Use();

        // Expected according to docs.
        // But this says: if running on iOS, then we're also running
        // on MacCatalyst.
        if (SubsetFeature.IsSupported)
            SupersetFeature.Use();

    }
}

// Baz depends on Foo.
class Baz {

    [FeatureCheck("Baz")]
    public static bool IsSupported => Foo.IsSupported;

    [RequiresFeature("Baz")]
    [RequiresFeature("Foo")]
    public static void Use() {
        Console.WriteLine("Baz.Use");
    }
}


class SubsetFeature {
    [FeatureCheck("SubsetFeature")]
    public static bool IsSupported => true;

    // Supported on both SubsetFeature and SupersetFeature.
    [RequiresFeature("SubsetFeature")]
    public static void Use() {
        Console.WriteLine("SubsetFeature.Use");
    }
}

class SupersetFeature {
    [FeatureCheck("SupersetFeature")]
    public static bool IsSupported => SubsetFeature.IsSupported;

    [RequiresFeature("SupersetFeature")]
    public static void Use() {
        Console.WriteLine("SupersetFeature.Use");
    }
}


// If my library has a hard dependency on an optional feature? How do I annotate it?
// - Redeclare feature requirement (SAME feature)
// - Declare a new feature requirement (NEW feature)

// - RUST: you don't. You just use the feature. If it's not available, you get a compile error.
// - C#: you will get a warning. you can annotate with the _same_ attribute currently (should this be allowed?)
//       if you use a new attribute, it won't be silenced. should there be a way to say a feature depends on another?

// If feature Foo depends on Bar, then RequiresFoo => RequiresBar. FooEnabled => BarEnabled.

// The following concerns arise when letting a library declare that its APIs require a feature that was declared in a dependency:

// Separate issues:
// - feature namespaces. multiple libraries should be able to use same feature names, different features
// - using feature attribute condition from a dependency

// Experimental features:
// - example where it may be completely removed. including feature check.
// - illink will end up analyzing call to some API that doesn't RESOLVE.

// high might partially depend on low.
// so high check may or may not imply low. we get to decide.
// let's say high does depend on low, as per feature check.

// supported on iOS means supported on maccatalyst.
// unsupported on iOS means unsupported on maccatalyst.
// same isn't true of features.
// high depends on low.
// requires high means requires low
// not requires high _doesn't_ mean not requires low.

namespace System {
    class OperatingSystem {
        [System.Runtime.Versioning.SupportedOSPlatformGuard("Foo")]
        public static bool IsBaz() => false;

        public static bool IsFoo() => false;
        public static bool IsBar() => false;


        [System.Runtime.Versioning.SupportedOSPlatformGuard("SupersetFeature")]
        public static bool IsSubsetFeature() => false;

        public static bool IsSupersetFeature() => false;
    }
}