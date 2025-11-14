using BenchmarkDotNet.Attributes;
using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using System.Reflection;

namespace PolyType.Benchmarks;

/// <summary>
/// Benchmarks comparing the performance of source generator AttributeProvider lookup vs. direct MemberInfo lookup.
/// </summary>
[MemoryDiagnoser]
public partial class AttributeProviderBenchmark
{
    private readonly ITypeShape<ClassWithAttributes> _sourceGenShape = 
        Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithAttributes>();
    
    private readonly IPropertyShape _sourceGenPropertyShape = 
        ((IObjectTypeShape)Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithAttributes>())
            .Properties.First(p => p.Name == nameof(ClassWithAttributes.Value));

    // ITypeShape: AttributeProvider vs Type

    [Benchmark(Description = "ITypeShape.AttributeProvider.IsDefined")]
    public bool TypeShape_AttributeProvider_IsDefined()
    {
        return _sourceGenShape.AttributeProvider.IsDefined<DescriptionAttribute>();
    }

    [Benchmark(Description = "ITypeShape.Type.IsDefined")]
    public bool TypeShape_Type_IsDefined()
    {
        return _sourceGenShape.Type.IsDefined(typeof(DescriptionAttribute), inherit: true);
    }

    [Benchmark(Description = "ITypeShape.AttributeProvider.GetCustomAttribute")]
    public DescriptionAttribute? TypeShape_AttributeProvider_GetCustomAttribute()
    {
        return _sourceGenShape.AttributeProvider.GetCustomAttribute<DescriptionAttribute>();
    }

    [Benchmark(Description = "ITypeShape.Type.GetCustomAttribute")]
    public DescriptionAttribute? TypeShape_Type_GetCustomAttribute()
    {
        return _sourceGenShape.Type.GetCustomAttribute<DescriptionAttribute>(inherit: true);
    }

    [Benchmark(Description = "ITypeShape.AttributeProvider.GetCustomAttributes")]
    public int TypeShape_AttributeProvider_GetCustomAttributes()
    {
        return _sourceGenShape.AttributeProvider.GetCustomAttributes<CustomAttribute>().Count();
    }

    [Benchmark(Description = "ITypeShape.Type.GetCustomAttributes")]
    public int TypeShape_Type_GetCustomAttributes()
    {
        return _sourceGenShape.Type.GetCustomAttributes<CustomAttribute>(inherit: true).Count();
    }

    // IPropertyShape: AttributeProvider vs MemberInfo

    [Benchmark(Description = "IPropertyShape.AttributeProvider.IsDefined")]
    public bool PropertyShape_AttributeProvider_IsDefined()
    {
        return _sourceGenPropertyShape.AttributeProvider.IsDefined<CustomAttribute>();
    }

    [Benchmark(Description = "IPropertyShape.MemberInfo.IsDefined")]
    public bool PropertyShape_MemberInfo_IsDefined()
    {
        MemberInfo? member = _sourceGenPropertyShape.MemberInfo;
        return member?.IsDefined(typeof(CustomAttribute), inherit: true) ?? false;
    }

    [Benchmark(Description = "IPropertyShape.AttributeProvider.GetCustomAttribute")]
    public CustomAttribute? PropertyShape_AttributeProvider_GetCustomAttribute()
    {
        return _sourceGenPropertyShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
    }

    [Benchmark(Description = "IPropertyShape.MemberInfo.GetCustomAttribute")]
    public CustomAttribute? PropertyShape_MemberInfo_GetCustomAttribute()
    {
        MemberInfo? member = _sourceGenPropertyShape.MemberInfo;
        return member?.GetCustomAttribute<CustomAttribute>(inherit: true);
    }

    [Benchmark(Description = "IPropertyShape.AttributeProvider.GetCustomAttributes")]
    public int PropertyShape_AttributeProvider_GetCustomAttributes()
    {
        return _sourceGenPropertyShape.AttributeProvider.GetCustomAttributes<CustomAttribute>().Count();
    }

    [Benchmark(Description = "IPropertyShape.MemberInfo.GetCustomAttributes")]
    public int PropertyShape_MemberInfo_GetCustomAttributes()
    {
        MemberInfo? member = _sourceGenPropertyShape.MemberInfo;
        return member?.GetCustomAttributes<CustomAttribute>(inherit: true).Count() ?? 0;
    }



    // Test types

    [GenerateShape]
    [Description("This type is for benchmarking")]
    [Custom("TypeAttribute", 42)]
    public partial class ClassWithAttributes
    {
        [Custom("PropertyAttribute", 100)]
        public int Value { get; set; }

        [Custom("PropertyAttribute2", 200)]
        public string Name { get; set; } = string.Empty;

        [Custom("FieldAttribute", 300)]
        public bool Flag;
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public class DescriptionAttribute : Attribute
    {
        public string Description { get; }

        public DescriptionAttribute(string description) => Description = description;
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class CustomAttribute : Attribute
    {
        public string? Name { get; set; }
        public int Value { get; set; }

        public CustomAttribute() { }
        public CustomAttribute(string name) => Name = name;
        public CustomAttribute(int value) => Value = value;
        public CustomAttribute(string name, int value) { Name = name; Value = value; }
    }

    [GenerateShapeFor<ClassWithAttributes>]
    public partial class Witness;
}
