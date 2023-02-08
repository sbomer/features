using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

class Program {
    public static void Main() {
        if (MyLargeFeature.IsSupported)
            MyLargeFeature.DoSomethingThatHasLargeDependencies(); // Trimmed if MyLargeFeature=false

        if (MyRUCFeature.IsSupported)
            MyRUCFeature.DoSomethingWithUnreferencedCode(); // Should not warn.

        if (MyRDCFeature.IsSupported)
            MyRDCFeature.DoSomethingWithDynamicCode(); // Should not warn.

        if (RuntimeFeature.IsDynamicCodeSupported)
            MyRDCFeature.DoSomethingWithDynamicCode(); // Should not warn.

        if (RuntimeFeatures.IsUnreferencedCodeSupported) // !RuntimeFeatures.IsTrimmedApp
            MyRUCFeature.DoSomethingWithUnreferencedCode(); // Should not warn.
    }
}

class MyLargeFeature {
    public static bool IsSupported {
        // If my feature is disabled when trimming, this property returns false.
        // To disable it when trimming, need to pass MyLargeFeature=false in MSBuild.
#if TRIMMING
        get => false;
#else
        // Means:
        // if ((featureDefault && MyFeature == null) || MyFeature == featureValue)
        //    stub out to stubValue;
        [FeatureStub(nameof(MyLargeFeature), featureValue: false, stubValue: false)]
        get => (bool)AppContext.GetData(nameof(MyLargeFeature));
#endif
    }

    public static void DoSomethingThatHasLargeDependencies() {}
}

class MyRUCFeature {
    [FeatureCheck<RequiresUnreferencedCodeAttribute>]
    public static bool IsSupported {
        // Disabled when trimming. Trim analyzer could treat this as guard for RCD based on the featureDefault.
        // When trimming:
        // if ((featureDefault && MyFeature == null) || MyFeature == featureValue)
        //    stub out to stubValue;
        [FeatureStub("MyRUCFeature", featureValue: false, stubValue: false, defaultWhenTrimming: true)]
        get => true;
    }

    [RequiresUnreferencedCode(nameof(DoSomethingWithUnreferencedCode) + " check IsSupported")]
    public static void DoSomethingWithUnreferencedCode() {}
}

class MyRDCFeature {
    public static bool IsSupported {
        // Should be disabled for AOT. Analyzer needs to treat this as a guard for RDC but not RUC.
        // Currently requires consuming library to pass MyRDCFeature=false in MSBuild.
        [FeatureStub("MyRDCFeature", false, false)]
        get => true;
    }

    [RequiresDynamicCode(nameof(DoSomethingWithDynamicCode))]
    public static void DoSomethingWithDynamicCode() {}
}

class RuntimeFeatures {
    public static bool IsTrimmedApp {
        [FeatureStub("PublishTrimmed", true, true, defaultWhenTrimming: true)]
        get => false;
    }
    public static bool IsUnreferencedCodeSupported {
        [FeatureStub("IsUnreferencedCodeSupported", false, false, defaultWhenTrimming: true)]
        get => true;
    }
}

// PLUS MSBuild logic:

// <ItemGroup>
//   <RuntimeHostConfigurationOption Include="MyFeature"
//                                   Condition="'$(PublishTrimmed)' == 'true'"
//                                   Value="false"
//                                   Trim="true" />
// </ItemGroup>

// <ItemGroup>
//   <RuntimeHostConfigurationOption Include="PublishTrimmed"
//                                   Condition="'$(PublishTrimmed)' == 'true'"
//                                   Value="true"
//                                   Trim="true" />
// </ItemGroup>

// <ItemGroup>
//   <RuntimeHostConfigurationOption Include="IsUnreferencedCodeSupported"
//                                   Condition="'$(PublishTrimmed)' == 'true'"
//                                   Value="false"
//                                   Trim="true" />
// </ItemGroup>
