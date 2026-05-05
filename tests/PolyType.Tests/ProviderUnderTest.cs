using Microsoft.FSharp.Reflection;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using PolyType.SourceGenModel;
using System.Collections;
using System.Collections.Immutable;
using System.Reflection;

namespace PolyType.Tests;

public abstract class ProviderUnderTest
{
    public abstract ProviderKind Kind { get; }
    public abstract bool ResolvesNullableAnnotations { get; }
    public abstract ITypeShapeProvider Provider { get; }
    public abstract ITypeShape ResolveShape(ITestCase testCase);

    public ITypeShape<T> ResolveShape<T>(TestCase<T> testCase) =>
        (ITypeShape<T>)ResolveShape((ITestCase)testCase);

    public bool HasConstructor(ITestCase testCase)
    {
        if (testCase.IsUnion)
        {
            if (!testCase.IsAbstract)
            {
                return true;
            }

            return FSharpType.IsUnion(testCase.Type, null) || HasConstructibleSubtype(testCase.Type);
        }

        return !(testCase.IsAbstract && !typeof(IEnumerable).IsAssignableFrom(testCase.Type)) &&
            !testCase.IsMultiDimensionalArray &&
            !(testCase.HasOutConstructorParameters && Kind is not ProviderKind.SourceGen) &&
            !testCase.IsFunctionType &&
            testCase.CustomKind is not TypeShapeKind.None &&
            (!testCase.UsesSpanConstructor || Kind is not ProviderKind.ReflectionNoEmit);
    }

    private static bool HasConstructibleSubtype(Type type)
    {
        foreach (DerivedTypeShapeAttribute attr in type.GetCustomAttributes<DerivedTypeShapeAttribute>(false))
        {
            if (!attr.Type.IsAbstract && !attr.Type.IsInterface)
            {
                return true;
            }
        }

        foreach (Attribute attr in type.GetCustomAttributes(false).OfType<Attribute>())
        {
            if (attr.GetType().FullName is "System.Runtime.CompilerServices.ClosedSubtypeAttribute")
            {
                Type? subtypeType = (Type?)attr.GetType().GetProperty("SubtypeType")?.GetValue(attr);
                if (subtypeType is { IsAbstract: false, IsInterface: false })
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public enum ProviderKind
{
    SourceGen = 1,
    ReflectionNoEmit = 2,
    ReflectionEmit = 3
};

public sealed class SourceGenProviderUnderTest(SourceGenTypeShapeProvider sourceGenProvider) : ProviderUnderTest
{
    public static SourceGenProviderUnderTest Default { get; } = new(Witness.GeneratedTypeShapeProvider);

    public override ProviderKind Kind => ProviderKind.SourceGen;
    public override bool ResolvesNullableAnnotations => true;
    public override ITypeShapeProvider Provider => sourceGenProvider;
    public override ITypeShape ResolveShape(ITestCase testCase) => testCase.DefaultShape;
}

public sealed class ReflectionProviderUnderTest(ReflectionTypeShapeProviderOptions options) : ProviderUnderTest
{
    private static readonly ImmutableArray<Assembly> TypeShapeExtensionAssemblies = [typeof(TestCase).Assembly, Assembly.GetExecutingAssembly()];

    public static ReflectionProviderUnderTest Emit { get; } = new(new() { UseReflectionEmit = true, TypeShapeExtensionAssemblies = TypeShapeExtensionAssemblies });
    public static ReflectionProviderUnderTest NoEmit { get; } = new(new() { UseReflectionEmit = false, TypeShapeExtensionAssemblies = TypeShapeExtensionAssemblies });

    public override ITypeShapeProvider Provider { get; } = ReflectionTypeShapeProvider.Create(options);
    public override ProviderKind Kind => options.UseReflectionEmit ? ProviderKind.ReflectionEmit : ProviderKind.ReflectionNoEmit;
    public override bool ResolvesNullableAnnotations => ReflectionHelpers.IsNullabilityInfoContextSupported;
    public override ITypeShape ResolveShape(ITestCase testCase) => Provider.GetTypeShapeOrThrow(testCase.Type);
}