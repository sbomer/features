# Optional features

We have shipped support for many optional features in the .NET Runtime, in support of trimming and NativeAot. Some of these features are incompatible with trimming and are disabled in trimmed apps. We have also defined optional features that may be manually turned off in trimmed apps, to reduce app size or remove unwanted behavior.

Code related to such features is annotated with the attributes `RequiresDynamicCode`, `RequiresUnreferencedCode`, and `RequiresAssemblyFiles`, and we have shipped analyzers which warn on calls to such annotated code. To enable the removal of these code paths in trimmed/AOT apps, we also added support for an XML input that can declare a method to return a constant, and support in illink/ILCompiler for removing branches which are unreachable given these constants.

We would like to design a system which allows analyzers to treat these constant-returning methods as guards, to prevent warnings from guarded calls to feature-annotated APIs. As part of this effort, we want to replace the XML with an attribute model, so that the analyzer doesn't need to parse the XML, and so that third-party feature authors can use these annotations and feature guards without authoring XML.

## Goals

- Define an attribute model for feature annotations and feature guards
- Enable analyzers to understand feature guards (without reading XML)
- Allow most existing XML substitutions to be replaced by the attribute-based model (aim for 95%)
- Allow third-party libraries to define their own feature annotations and feature guards

## Non-goals

- Complete parity with the XML substitutions as they exist today. (95% should be good enough.)
- Define the precise circumstances under which calls to feature-attributed methods will warn
  - See existing spec: https://github.com/dotnet/runtime/blob/main/docs/design/tools/illink/feature-attribute-semantics.md
- Define the exact circumstances under which feature guards will prevent warnings
  - See [Supported patterns for calls to feature guards](#supported-patterns-for-calls-to-feature-guards)
- Define the exact wording of warnings produced by the analyzer

## Design

The proposal has the following pieces:
1. A way to define an attribute that makes it a "feature annotation".
2. A way to define a boolean "feature check" method which guards calls to feature-annotated APIs.
3. A way to express that one feature logically depends on another
4. Inference of default feature settings based on feature dependencies

### 1. Feature annotation attributes

`RequiresFeatureAttribute` serves as a base type for feature attributes. Attribute types derived from this which follow the naming convention `RequiresFeatureNameAttribute` (where `FeatureName` is the name of the defined feature) are treated as feature annotation attributes. Each derived attribute logically defines a separate feature. Calls to methods annotated with a feature attribute will produce a warning during compilation.

When trimming, calls to the feature-annotated method will be warn if the feature is not enabled. The feature may be disabled when trimming by passing the feature settings `--feature FeatureName false`. If such a flag is not passed, the defaults for a feature are defined according to the rules for feature dependencies (see below).

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, AllowMultiple = true)]
class RequiresFeatureAttribute : Attribute { }
```

Example:

Here, we define a new feature `Foo`. Calls to `Foo`-annotated code will produce a warning.

```csharp
Foo.Use(); // warning: Foo.Use() requires the feature "Foo"

class RequiresFooAttribute : RequiresFeatureAttribute
{
}

partial class Foo {
    [RequiresFoo]
    public static void Use() { }
}
```

### 2. Feature checks

`FeatureCheckAttribute<T>` is an attribute that can be applied to a static boolean method, causing it to be treated as a guard for calls to code annotated with the feature attribute `T`. The analyzer will not warn for guarded calls to feature-annotated methods.

ILLink and ILCompiler will also recognize the association between the feature check and the feature attribute, so that disabling a feature when trimming will cause the feature check to be substituted with the constant `false` (and enabling a feature when trimming will cause it to be substituted with `true`). This is designed to replace the majority use case for XML substitutions.

```csharp
class FeatureCheckAttribute<T> : Attribute
    where T : RequiresFeatureAttribute
{
}
```

Example:

Here we introduce a feature check for `Foo`. This check can guard calls to `Foo`-annotated features, to prevent warnings from being produced.

```csharp
if (Foo.IsEnabled)
    Foo.Use(); // no warning

partial class Foo
{
    [FeatureCheck<RequiresFooAttribute>]
    public static bool IsEnabled { get; }
}
```

### 3. Feature dependencies

A feature attribute itself may be annotated with a feature attribute to express that it requires (depends on) another feature.

Example:

Here we define a new feature `Bar` which depends on `Foo`.

```csharp
[RequiresFoo]
class RequiresBarAttribute : RequiresFeatureAttribute
{
}
```

This expresses that `Bar`-annotated code may call `Foo`-annotated code without producing a warning, and that feature checks for `Bar` should only return `true` if `Foo` is also enabled.

```csharp
class Bar
{
    [FeatureCheck<RequiresBarAttribute>]
    public static bool IsEnabled { get => Foo.IsEnabled; }

    [RequiresBar]
    public static void Use() {
        Foo.Use(); // no warning
    }
}
```

However, it allows `Bar` to be disabled even if `Foo` is enabled. `Foo`-annotated code, or calls to `Foo` feature checks, will not prevent warnings on calls to `Bar`-annotated features.

```csharp
class Program {
    static void Main() {
        if (Bar.IsEnabled) {
            Bar.Use(); // no warning
            Foo.Use(); // no warning
        }

        if (Foo.IsEnabled) {
            Foo.Use(); // no warning
            Bar.Use(); // warning: Bar.Use() requires the feature "Bar"
        }
    }

    [RequiresFoo]
    static void UseBar() {
        Bar.Use(); // warning: Bar.Use() requires the feature "Bar"
    }
}

```

### 4. Default feature settings

A feature may be explicitly enabled or disabled by passing a feature setting to the compiler (or ILlink/ILCompiler). If invalid feature settings are passed (enabling a feature but disabling a feature on which it depends), the compiler will produce an error.

If no feature setting is passed for a given feature, the default is for it to be enabled, unless it depends on another feature which is disabled.

Example:

Expressing feature dependencies this way allows us to define the default behavior for features when trimming, AOT-compiling, or single-file bundling. The build logic in the SDK will explicitly disable the feature `UnreferencedCode` when trimming, disable `DynamicCode` when AOT-compiling, and disable `AssemblyFiles` when single-file bundling. Then any features which depend on (for example) `RequiresUnreferencedCode` APIs will also be disabled by default.

```csharp
class ReflectionBasedSerializer {
    [FeatureCheck<RequiresReflectionBasedSerializerAttribute>]
    public static bool IsEnabled { get; }

    [RequiresReflectionBasedSerializer]
    public static void Serialize(object obj, Stream stream) {
        // ...
    }
}

class RequiresReflectionBasedSerializerAttribute : RequiresFeatureAttribute
{
}
```

In a trimmed app, the reflection-based serializer will automatically be disabled, and the branch which calls it will be removed.

```csharp
if (ReflectionBasedSerializer.IsEnabled) {
    ReflectionBasedSerializer.Serialize(obj, stream); // removed
} else {
    // Fallback to some other serializer
}
```

## Open questions

How important is it to support non-boolean substitutions? Even if we don't support it in the first version, we might want to consider how the attribute could evolve to support this in the future.

This has a lot of overlap with existing proposals:
- Capability-based analyzer: https://github.com/dotnet/designs/pull/261
- Experimental APIs: https://github.com/dotnet/designs/pull/285

Can we agree that the capability-based attributes and experimental APIs should use the semantics defined in https://github.com/dotnet/runtime/blob/main/docs/design/tools/illink/feature-attribute-semantics.md, and not the `Obsolete` semantics, for example? If so, those designs (particularly the capability-based analyzer) should be unified with this one.

The API shape here is not finalized. We will want to consider alternatives and iterate on the exact shape.

## Notes

### Supported patterns for calls to feature guards

ILLink and ILCompiler have different implementations of branch removal based on known constant-returning methods. The analyzer will be made to support a simple set of patterns for calls to these methods that is a subset of the patterns supported by ILLink and ILCompiler, so that code which doesn't warn in the analyzer will not warn in ILLink or ILCompiler, but there may be code which doesn't warn in ILLink or ILCompiler but does warn in the analyzer.

The specific set of supported patterns is not defined here, but will be initially chosen to be a small set of obviously useful patterns; initially, the support will likely be limited to if statements where the condition is a simple call to a single feature guard. We will expand the set of supported patterns as needed, using testing to ensure that the analyzer supports a subset of the patterns supported by ILLink and ILCompiler.

### Why we need feature dependencies

A feature may have an _optional_ dependency on another feature without any further machinery.
For example, consider two features: `Base`, and `OptionalDependency` (which calls into `Base`-attributed code, if `Base` is enabled):

```csharp
class RequiresBaseAttribute : RequiresFeatureAttribute { }

class RequiresOptionalDependencyAttribute : RequiresFeatureAttribute { }

class Base
{
    [FeatureCheck<RequiresBaseAttribute>]
    public static bool IsEnabled { get; }

    [RequiresBase]
    public static void Use() { }
}

class OptionalDependency
{
    [RequiresOptionalDependency]
    public static void Use() {
        if (Base.IsEnabled)
            Base.Use();
        else
            FallbackImplementation();
    }

    static void FallbackImplementation() { }
}
```

Because the use of `Base` is properly guarded, the two features can be enabled and disabled independently.

However, it may also be the case that one feature has a _hard_ dependency on another. Here, `HardDependency` has an unconditional call to `Base`-annotated code:

```csharp
class RequiresHardDependencyAttribute : RequiresFeatureAttribute { }

class HardDependency
{
    [RequiresHardDependency]
    public static void Use() {
        Base.Use(); // warning: Base.Use() requires the feature "Base"
    }
}
```

This produces an analyzer warning (and an ILLink/ILCompiler warning unless `Base` is enabled when trimming). The warning could be addressed by annotating the implementation with a `RequiresBase` attribute:

```csharp
class HardDependency
{
    [FeatureCheck<RequiresHardDependencyAttribute>]
    public static bool IsEnabled { get; }

    [RequiresHardDependency]
    [RequiresBase]
    public static void Use() {
        Base.Use(); // no warning
    }
}
```

However, this has poor interactions with feature guards. Feature guards for `HardDependency` will not suppress warnings about the `Base` requirement:

```csharp
if (HardDependency.IsEnabled)
    HardDependency.Use(); // warning: HardDependency.Use() requires the feature "Base"
```

Another way to address the warning is to avoid introducing a new feature attribute, and instead use the existing `RequiresBase` attribute:


```csharp
class HardDependency
{
    [RequiresBase]
    public static void Use() {
        Base.Use(); // no warning
    }
}
```

Now uses of the `HardDependency` feature can be guarded by checking the `Base` feature instead:

```csharp
if (Base.IsEnabled)
    HardDependency.Use(); // no warning
```

This effectively makes `HardDependency` a part of the `Base` feature. This may be reasonable in some cases, but note that it makes it impossible to disable `HardDependency` independently of `Base`. If `Base` is enabled, then so is `HardDependency`.

Note that it is also possible to introduce a new feature check for `HardDependency` that guards the call to `Base`:

```csharp
class HardDependency
{
    [FeatureCheck<RequiresBaseAttribute>]
    public static bool IsEnabled { get; }

    [RequiresBase]
    public static void Use() {
        Base.Use(); // no warning
    }
}
```

This also effectively makes `HardDependency` a part of the `Base` feature, but it obscures the fact that the feature check is actually guarding the `Base` feature:

```csharp
if (HardDependency.IsEnabled)
{
    HardDependency.Use(); // no warning
    Base.Use(); // no warning
}
```

This approach is similar to the current usage in dotnet/runtime, which defines a number of feature checks which are used to guard calls to `RequiresUnreferencedCode` APIs, and are disabled by default when trimming. They can all be enabled and disabled independently. For example, runtime defines the following two feature checks:

- `System.Resources.ResourceReader.AllowCustomResourceTypes`
- `System.Runtime.InteropServices.Marshal.IsBuiltInComSupported`

These can be set by passing feature settings when trimming. For example, to enable one but disable the other:

```xml
<PropertyGroup>
    <CustomResourceTypesSupport>true</CustomResourceTypesSupport>
    <BuiltInComSupport>false</BuiltInComSupport>
</PropertyGroup>
```

This would result in the following behavior when trimming this nonsensical code:

```csharp
if (System.Resources.ResourceManager.AllowCustomResourceTypes) {
    // Call an API which is normally supposed to be guarded by Marshal.IsBuiltInComSupported
    System.Runtime.InteropServices.Marshal.BindToMoniker("moniker"); // no warning, even though built-in COM support is disabled
}
```

This illustrates a problem when using a single feature attribute (`RequiresUnreferencedCodeAttribute`) together with many independent feature checks. The feature analysis is no longer correct unless all features are enabled and disabled together. Fortunately, these feature checks are not public, so the above could only happen in dotnet/runtime code. However, this approach doesn't have the right semantics for the general case.

The above examples suggest that features which depend on other features should either re-use the same feature check and attribute, becoming part of the same feature, or introduce a new feature attribute (and check, if required). When introducing a new attribute, there needs to be a way to express that the new feature depends on the existing feature.