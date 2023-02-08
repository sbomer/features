
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
class FeatureStubAttribute : Attribute {
    public FeatureStubAttribute(string featureName, bool featureValue, bool stubValue, bool defaultWhenTrimming = false) {
        FeatureName = featureName;
        FeatureValue = featureValue;
        StubValue = stubValue;
        FeatureDefault = defaultWhenTrimming;
    }
    public string FeatureName { get; }
    public object FeatureValue { get; }
    public bool StubValue { get; }
    public bool FeatureDefault { get; }
}

class FeatureCheckAttribute<T> : Attribute
    where T : RequiresFeatureAttribute
{
    public FeatureGuardAttribute() {}
}


class RequiresFeatureAttribute : Attribute {
    public RequiresFeatureAttribute() {}
}

class RequiresUnreferencedCodeAttribute : RequiresFeatureAttribute {
    public RequiresUnreferencedCodeAttribute(string message) {}
}

class FeatureHelper {
    public static bool Check(string featureName, bool defaultValue) {
        if (AppContext.TryGetSwitch(featureName, out bool featureValue))
            return featureValue;
        return default;
    }
}