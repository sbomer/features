## Goals

- Specify patterns and APIs that can be used to define feature switches
- Specify supported patterns which can be inlined by the JIT, illink, nativeaot

## Design considerations

Considering solved, pending some discussion, decision-making.

- Shape/name of the attribute used to mark feature-related code
  - For now, `[RequiresFeature("Foo")]`, `RequiresFeatureAttribute(string featureName)`
  - Could also be `[RequiresFoo]`, where `class RequiresFooAttribute : RequiresFeatureAttribute`

- Semantics of attribute "virality"
  - For now, https://github.com/dotnet/runtime/blob/main/docs/design/tools/illink/feature-attribute-semantics.md
  - Alternative: `Obsolete` semantics
  - Alternative: `SupportedOSPlatform` semantics

- Interaction with dataflow
  - `RequiresUnreferencedCode` silences `DynamicallyAccessedMembers` dataflow warnings
  - Will likely have similar pattern with `IsDynamicCodeSupported`
  - Needs to be integrated with dataflow analysis

- Specifying feature checks
  - Could hard-code per attribute
  - For now, `[FeatureCheck("Foo")]`
  - Could also be `SupportedOSPlatformGuardAttribute`
  - Could also be `CapabilityCheck` on attribute to indicate main, with extra `CapabilityGuard` on other check APIs

- Supported feature checks
  - Read-only static bool property... other patterns allowed too?

- Supported calls to feature checks
  - Just start with some small obviously useful subset. Direct call to the feature is guaranteed.
  - If some tools do better, fine - but analyzer will start with the smallest subset of supported patterns.

- Feature removal
  - Compile-time: use `DefineConstants`. No extra machinery required.
  - ILLink/ILCompile: pass MSBuild items for disabled features. Use existing `{(name, bool)}` bag from `RuntimeHostConfigurationOption`
    - TODO: could define more user-friendly way to set these: `FeatureSetting`
    - When it encounters a feature check, it knows (via attributes) that it is associated with the feature, and can stub.
  - JIT: can inline constant feature checks at runtime

Open questions:

- Namespace of features
  - Existing: opaque strings (MSBuild properties -> opaque strings -> feature check method (via XML))
  - Alternative: could be names of attributes derived from `RequiresFeatureAttribute`
  - What if two libraries use the same feature name??? Should they be namespaced by the assembly name?

- Accessibility of features
  - All features live in a global, public namespace. Trimming may remove features that don't have public feature checks.
  - Non-public features are considered implementation details of a library, so feature settings are a way to alter at trim time
    the implementation behavior of the library.

- Removal of feature checks?
  - Can be done as optimization when trimming an application. What about libraries?
  - Say library A defines feature Foo. B uses it. Now A makes a breaking change and removes it.
  - If B is recompiled, would get compile errors. If not, fails at runtime. Whole-program analysis could help catch it earlier.
  - In any case, removing a feature from a library is a breaking change.
  - Libraries should basically never depend on experimental features, _even_ behind a feature check.
  - Unless they put the usage of the feature behind a feature attribute, so consumer can disable it if they hit the break.

- Hard dependency on an optional feature?
  - Such library will only be usable in context where the feature is enabled.
  - Will get build warnings by default. Asking you to annotate your APIs as requiring the feature.
    Or could mark the whole assembly. But should be your choice.
    Maybe have an easy way to say "this feature is enabled", which silences all warings, and marks the whole assembly as requiring feature.

- As a library author, do I get warnings when using features behind a feature attribute?
  - In rust: no for available features, yes for disabled ones.
  - In .NET: you get a warning. If your library requires this feature, fine. Encode this in the library metadata.
  - Library will only be supported in environment where the feature is enabled. Build-time warnings otherwise.
  - Depending on library means you need to propagate the feature requirement.

  - lib A declares dependency on B with feature X.
    - consumer App depends on A. Doesn't need to say anything about B with feature X.
    - but if X is disabled when building App, there should be warnings.
    - so now using A means App requires feature X to be enabled.
    - Should A declare that it requires feature B.X?
    - In rust, I believe no.
    
  - experimental features are just ones that are "disabled by default".
  - usually, no build warnings when depending on default features.
  - trimming turns off some features.
  - optional features with trimming?
  - don't want warnings if you're just depending on the defaults.
  - if someone turns off a default, they are depending on a smaller surface. but still must be compatible with
  - those features being turned on by another dependency.


- Can a library use another library's feature name for its own code?
  - Corelib defines feature "dynamic code". MySerializer has a code path that uses this.
  - Can I put that code behind the same "dynamic code" feature switch?
  - Or do I need to define a new feature switch "MySerializer dynamic code"?
  - Answer by referencing expected developer behavior. If I turn off dynamic code, I expect:
    - Library APIs which weren't marked should "just work". Implementation changes, but I don't need to be aware.
    - Library APIs which were marked


  - Rust: removing features will be caught when compiling whole program.
  - .NET: removing features will not be caught until runtime. ILLink has opportunity to help.

   If a feature check is completely removed with `DefineConstants`, this breaks the public API of the library. Not allowed.
   Can remove optional features
   Experimental APIs proposal has this problem. Solving it by 

  - If a feature check 
  - If a feature is disabled at compile time (or library build time, even if implemented with linker directives)...
  - Current proposal doesn't allow removal of feature checks. Unfortunate: must keep containing type/assembly.

- Features depending on other features?
  - COM depends on UnreferencedCode.
  - MySerializer depends on DynamicCode.
  - if DynamicCode is disabled, so is MySerializer. Or parts of it.
  - Could mark whole of MySerializer as RequiresDynamicCode.

  - Say MySerializer has two modes. Dynamic and static.
  - MySerializer.Dynamic is marked RequiresDynamiCode.
  - MySerializer.Static is fine.