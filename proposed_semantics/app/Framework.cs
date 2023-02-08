interface IFeatureAttribute<TSelf> where TSelf : IFeatureAttribute<TSelf> {
    static abstract string FeatureName { get; }

    // Features are enabled by default, unless otherwise specified.
    static virtual bool DefaultSetting { get => true; }

    static bool IsSupported() {
        return AppContext.TryGetSwitch(TSelf.FeatureName, out bool value) ? value : TSelf.DefaultSetting;
    }
}

static class Feature {
    public static bool IsSupported<TFeature>() where TFeature : IFeatureAttribute<TFeature> {
        return TFeature.IsSupported();
    }
}

class RequiresUnreferencedCodeAttribute : Attribute, IFeatureAttribute<RequiresUnreferencedCodeAttribute> {
    public RequiresUnreferencedCodeAttribute(string message) {}

    public static string FeatureName => "UnreferencedCode";
}

class RequiresDynamicCodeAttribute : Attribute, IFeatureAttribute<RequiresDynamicCodeAttribute> {
    public RequiresDynamicCodeAttribute(string message) {}

    public static string FeatureName => "DynamicCode";
}
