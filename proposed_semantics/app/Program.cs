
public class Program {
    public static void Main() {
        BaseFeature.Use(); // Warning
        if (BaseFeature.BaseFeatureAttribute.IsSupported)
            BaseFeature.Use(); // No warning

        DependencyFeature.Use(); // Warning
        if (DependencyFeature.DependencyFeatureAttribute.IsSupported)
            DependencyFeature.Use(); // No warning

        // If BaseFeature is off, DependencyFeature automatically becomes off.
        // (So if DependencyFeature is on, BaseFeature must be on.)
        if (DependencyFeature.DependenycFeatureAttribute.IsSupported)
            BaseFeature.Use(); // No warning

        // If BaseFeature is on, DependencyFeature may be on or off by default.
        if (BaseFeature.BaseFeature.IsSupported)
            DependencyFeature.Use(); // Warning

    }
}


class BaseFeature {

    [BaseFeature]
    public static void Use() {}

    public class BaseFeatureAttribute : Attribute, IFeatureAttribute<BaseFeatureAttribute> {
        public static string FeatureName => "BaseFeature";
    }
}


class DependencyFeature {

    [DependencyFeature]
    public static void Use() {}

    public class DependencyFeatureAttribute : Attribute, IFeatureAttribute<DependencyFeatureAttribute> {
        public static string FeatureName => "DependencyFeature";
    }
}

