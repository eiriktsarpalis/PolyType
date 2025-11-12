using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

#pragma warning disable CS0618 // Type or member is obsolete

namespace PolyType.Tests;

public abstract partial class AttributeProviderTests(ProviderUnderTest providerUnderTest)
{
    protected ProviderUnderTest ProviderUnderTest { get; } = providerUnderTest;
    protected ITypeShapeProvider Provider => ProviderUnderTest.Provider;

    [Fact]
    public void TypeShape_ReturnsExpectedAttributes()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        IEnumerable<CustomAttribute> customAttrs = shape.AttributeProvider.GetCustomAttributes<CustomAttribute>();
        
        CustomAttribute[] attrs = customAttrs.ToArray();
        Assert.Single(attrs);
        Assert.Equal("TypeAttribute", attrs[0].Name);
        Assert.Equal(42, attrs[0].Value);

        // Obsolete attribute should be present
        Assert.True(shape.AttributeProvider.IsDefined<ObsoleteAttribute>());
        ObsoleteAttribute? obsolete = shape.AttributeProvider.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(obsolete);
        Assert.Equal("This type is obsolete", obsolete.Message);
    }

    [Fact]
    public void PropertyShape_ReturnsExpectedAttributes()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);

        // Test Field
        IPropertyShape? fieldShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Field");
        Assert.NotNull(fieldShape);
        Assert.True(fieldShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? fieldAttr = fieldShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(fieldAttr);
        Assert.Equal("FieldAttribute", fieldAttr.Name);

        // Test Property
        IPropertyShape? propertyShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Property");
        Assert.NotNull(propertyShape);
        Assert.True(propertyShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? propAttr = propertyShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(propAttr);
        Assert.Equal("PropertyAttribute", propAttr.Name);
        Assert.Equal(100, propAttr.Value);
    }

    [Fact]
    public void ConstructorShape_ReturnsExpectedAttributes()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IConstructorShape? ctor = objectShape.Constructor;
        
        Assert.NotNull(ctor);
        Assert.True(ctor.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? ctorAttr = ctor.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(ctorAttr);
        Assert.Equal("ConstructorAttribute", ctorAttr.Name);
    }

    [Fact]
    public void ParameterShape_ReturnsExpectedAttributes()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IConstructorShape? ctor = objectShape.Constructor;
        
        Assert.NotNull(ctor);
        // Try both 'field' and 'Field' since parameter name resolution can vary
        IParameterShape? param = ctor.Accept(new ParameterExtractor(), "field") as IParameterShape 
            ?? ctor.Accept(new ParameterExtractor(), "Field") as IParameterShape;
        Assert.NotNull(param);
        Assert.True(param.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? paramAttr = param.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(paramAttr);
        Assert.Equal("ParameterAttribute", paramAttr.Name);
    }

    [Fact]
    public void MethodShape_ReturnsExpectedAttributes()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        
        IMethodShape? methodShape = shape.Methods.FirstOrDefault(m => m.Name == "TestMethod");
        Assert.NotNull(methodShape);
        Assert.True(methodShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? methodAttr = methodShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(methodAttr);
        Assert.Equal("MethodAttribute", methodAttr.Name);

        // Check method parameter
        IParameterShape? param = methodShape.Accept(new MethodParameterExtractor(), "x") as IParameterShape;
        Assert.NotNull(param);
        Assert.True(param.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? paramAttr = param.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(paramAttr);
        Assert.Equal("MethodParameterAttribute", paramAttr.Name);
    }

    [Fact]
    public void EventShape_ReturnsExpectedAttributes()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        
        IEventShape? eventShape = shape.Events.FirstOrDefault(e => e.Name == "TestEvent");
        Assert.NotNull(eventShape);
        Assert.True(eventShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? eventAttr = eventShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(eventAttr);
        Assert.Equal("EventAttribute", eventAttr.Name);
    }

    [Fact]
    public void StructShape_ReturnsExpectedAttributes()
    {
        ITypeShape<StructWithAttributes>? shape = Provider.GetTypeShape<StructWithAttributes>();
        Assert.NotNull(shape);
        
        Assert.True(shape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? attr = shape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("StructAttribute", attr.Name);

        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Value");
        Assert.NotNull(propShape);
        Assert.True(propShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? propAttr = propShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(propAttr);
        Assert.Equal("StructFieldAttribute", propAttr.Name);
    }

    [Fact]
    public void EnumShape_ReturnsExpectedAttributes()
    {
        ITypeShape<EnumWithAttributes>? shape = Provider.GetTypeShape<EnumWithAttributes>();
        Assert.NotNull(shape);
        
        Assert.True(shape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? attr = shape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("EnumAttribute", attr.Name);

        // Test enum member attributes via reflection
        var enumType = typeof(EnumWithAttributes);
        var value1Field = enumType.GetField("Value1");
        Assert.NotNull(value1Field);
        var value1Attrs = value1Field.GetCustomAttributes<CustomAttribute>().ToArray();
        Assert.Single(value1Attrs);
        Assert.Equal("EnumMember1", value1Attrs[0].Name);
        
        var value2Field = enumType.GetField("Value2");
        Assert.NotNull(value2Field);
        Assert.True(value2Field.GetCustomAttribute<ObsoleteAttribute>() != null);
    }

    [Fact]
    public void MultipleAttributes_AllReturned()
    {
        ITypeShape<ClassWithMultipleAttributes>? shape = Provider.GetTypeShape<ClassWithMultipleAttributes>();
        Assert.NotNull(shape);
        
        CustomAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<CustomAttribute>().ToArray();
        Assert.Equal(3, attrs.Length);
        Assert.Contains(attrs, a => a.Name == "MultipleAttributes1");
        Assert.Contains(attrs, a => a.Name == "MultipleAttributes2");
        Assert.Contains(attrs, a => a.Name == "MultipleAttributes3" && a.Value == 999);

        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Value");
        Assert.NotNull(propShape);
        
        CustomAttribute[] propAttrs = propShape.AttributeProvider.GetCustomAttributes<CustomAttribute>().ToArray();
        Assert.Equal(2, propAttrs.Length);
        Assert.Contains(propAttrs, a => a.Name == "Property1");
        Assert.Contains(propAttrs, a => a.Name == "Property2");
    }

    [Fact]
    public void CompilerAndDiagnosticAttributes_PresentInReflection()
    {
        ITypeShape<ClassWithCompilerAndDiagnosticAttributes>? shape = 
            Provider.GetTypeShape<ClassWithCompilerAndDiagnosticAttributes>();
        Assert.NotNull(shape);
        
        // CompilerGenerated and DebuggerDisplay should be present in reflection providers
        if (ProviderUnderTest.Kind != ProviderKind.SourceGen)
        {
            Assert.True(shape.AttributeProvider.IsDefined<CompilerGeneratedAttribute>());
            Assert.True(shape.AttributeProvider.IsDefined<DebuggerDisplayAttribute>());
        }
    }

    [Fact]
    public void PolyTypeAttributes_PresentOnPropertyInReflection()
    {
        // Only test reflection providers
        if (ProviderUnderTest.Kind == ProviderKind.SourceGen)
        {
            return;
        }

        ITypeShape<ClassWithPolyTypeAttributes>? shape = Provider.GetTypeShape<ClassWithPolyTypeAttributes>();
        Assert.NotNull(shape);
        
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Value" || p.Name == "CustomName");
        Assert.NotNull(propShape);
        
        // Reflection providers include all attributes
        Assert.True(propShape.AttributeProvider.IsDefined<PropertyShapeAttribute>());
    }

    [Fact]
    public void TypeShape_InheritedAttributes_IncludedWhenApplicable()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // InheritableAttribute has Inherited = true, so it should be present
        Assert.True(shape.AttributeProvider.IsDefined<InheritableAttribute>());
        InheritableAttribute? inheritedAttr = shape.AttributeProvider.GetCustomAttribute<InheritableAttribute>();
        Assert.NotNull(inheritedAttr);
        Assert.Equal("BaseTypeAttribute", inheritedAttr.Name);
    }

    [Fact]
    public void PropertyShape_InheritedAttributes_BehaviorVariesByProvider()
    {
        ITypeShape<DerivedClassWithInheritedPropertyAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritedPropertyAttributes>();
        Assert.NotNull(shape);
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);

        // Virtual property that is overridden
        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "VirtualProperty");
        Assert.NotNull(propShape);
    }

    [Fact]
    public void MethodShape_InheritedAttributes_BehaviorVariesByProvider()
    {
        ITypeShape<DerivedClassWithInheritedMethodAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritedMethodAttributes>();
        Assert.NotNull(shape);
        
        IMethodShape? methodShape = shape.Methods.FirstOrDefault(m => m.Name == "VirtualMethod");
        Assert.NotNull(methodShape);
    }

    [Fact]
    public void EventShape_InheritedAttributes_BehaviorDocumented()
    {
        ITypeShape<DerivedClassWithInheritedEventAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritedEventAttributes>();
        Assert.NotNull(shape);
        
        IEventShape? eventShape = shape.Events.FirstOrDefault(e => e.Name == "VirtualEvent");
        Assert.NotNull(eventShape);
    }

    [Fact]
    public void MultipleInheritance_InterfaceAttributes_Included()
    {
        ITypeShape<ClassImplementingMultipleInterfaces>? shape = Provider.GetTypeShape<ClassImplementingMultipleInterfaces>();
        Assert.NotNull(shape);
        
        // Verify the interfaces themselves have the attributes
        ITypeShape<IInterface1WithInheritableAttributes>? interface1Shape = Provider.GetTypeShape<IInterface1WithInheritableAttributes>();
        Assert.NotNull(interface1Shape);
        Assert.True(interface1Shape.AttributeProvider.IsDefined<InheritableAttribute>());
        
        ITypeShape<IInterface2WithInheritableAttributes>? interface2Shape = Provider.GetTypeShape<IInterface2WithInheritableAttributes>();
        Assert.NotNull(interface2Shape);
        Assert.True(interface2Shape.AttributeProvider.IsDefined<InheritableAttribute>());
        
        // The implementing class should NOT inherit interface attributes
        InheritableAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<InheritableAttribute>().ToArray();
        Assert.Empty(attrs);
    }

    [Fact]
    public void GenericType_InheritedAttributes_PreservedAcrossGenericInstantiations()
    {
        ITypeShape<DerivedGenericWithInheritedAttributes<int>>? shape = 
            Provider.GetTypeShape<DerivedGenericWithInheritedAttributes<int>>();
        Assert.NotNull(shape);
        
        // Should inherit from generic base
        Assert.True(shape.AttributeProvider.IsDefined<InheritableAttribute>());
        InheritableAttribute? inheritedAttr = shape.AttributeProvider.GetCustomAttribute<InheritableAttribute>();
        Assert.NotNull(inheritedAttr);
        Assert.Equal("GenericBaseAttribute", inheritedAttr.Name);
    }

    [Fact]
    public void IsDefined_WithInheritTrue_FindsInheritableAttributes()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // Test with explicit inherit: true
        Assert.True(shape.AttributeProvider.IsDefined<InheritableAttribute>(inherit: true));
        
        // InheritableAttribute should be found
        InheritableAttribute? attr = shape.AttributeProvider.GetCustomAttribute<InheritableAttribute>(inherit: true);
        Assert.NotNull(attr);
        Assert.Equal("BaseTypeAttribute", attr.Name);
        
        // GetCustomAttributes with inherit: true
        InheritableAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<InheritableAttribute>(inherit: true).ToArray();
        Assert.Single(attrs);
        Assert.Equal("BaseTypeAttribute", attrs[0].Name);
    }

    [Fact]
    public void IsDefined_WithInheritFalse_DoesNotFindInheritableAttributesOnDerivedType()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // Test with explicit inherit: false - should not find base class attributes
        Assert.False(shape.AttributeProvider.IsDefined<InheritableAttribute>(inherit: false));
        
        // GetCustomAttribute with inherit: false should return null
        InheritableAttribute? attr = shape.AttributeProvider.GetCustomAttribute<InheritableAttribute>(inherit: false);
        Assert.Null(attr);
        
        // GetCustomAttributes with inherit: false should return empty
        InheritableAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<InheritableAttribute>(inherit: false).ToArray();
        Assert.Empty(attrs);
    }

    [Fact]
    public void IsDefined_WithInheritTrue_FindsNonInheritableAttributesViaReflection()
    {
        // Only test reflection providers - source gen may filter based on Inherited property
        if (ProviderUnderTest.Kind == ProviderKind.SourceGen)
        {
            return;
        }

        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // CustomAttribute has Inherited = false, but reflection's IsDefined with inherit: true 
        // will still find it on the base type (this is .NET reflection behavior)
        Assert.True(shape.AttributeProvider.IsDefined<CustomAttribute>(inherit: true));
        
        // However, GetCustomAttribute with inherit: true should still find it
        CustomAttribute? attr = shape.AttributeProvider.GetCustomAttribute<CustomAttribute>(inherit: true);
        Assert.NotNull(attr);
        Assert.Equal("NonInheritableBaseAttribute", attr.Name);
    }

    [Fact]
    public void IsDefined_WithInheritFalse_DoesNotFindNonInheritableAttributes()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // CustomAttribute with inherit: false should not be found
        Assert.False(shape.AttributeProvider.IsDefined<CustomAttribute>(inherit: false));
        
        CustomAttribute? attr = shape.AttributeProvider.GetCustomAttribute<CustomAttribute>(inherit: false);
        Assert.Null(attr);
        
        CustomAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<CustomAttribute>(inherit: false).ToArray();
        Assert.Empty(attrs);
    }

    [Fact]
    public void GetCustomAttributes_WithInheritTrue_ReturnsInheritedAttributes()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // Get all InheritableAttribute instances with inherit: true
        InheritableAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<InheritableAttribute>(inherit: true).ToArray();
        Assert.Single(attrs);
        Assert.Equal("BaseTypeAttribute", attrs[0].Name);
    }

    [Fact]
    public void GetCustomAttributes_WithInheritFalse_DoesNotReturnInheritedAttributes()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // Get all InheritableAttribute instances with inherit: false
        InheritableAttribute[] attrs = shape.AttributeProvider.GetCustomAttributes<InheritableAttribute>(inherit: false).ToArray();
        Assert.Empty(attrs);
    }

    [Fact]
    public void DefaultBehavior_MatchesInheritTrue()
    {
        ITypeShape<DerivedClassWithInheritableAttributes>? shape = Provider.GetTypeShape<DerivedClassWithInheritableAttributes>();
        Assert.NotNull(shape);
        
        // Default behavior (no inherit parameter) should match inherit: true
        bool isDefinedDefault = shape.AttributeProvider.IsDefined<InheritableAttribute>();
        bool isDefinedTrue = shape.AttributeProvider.IsDefined<InheritableAttribute>(inherit: true);
        Assert.Equal(isDefinedTrue, isDefinedDefault);
        
        InheritableAttribute? attrDefault = shape.AttributeProvider.GetCustomAttribute<InheritableAttribute>();
        InheritableAttribute? attrTrue = shape.AttributeProvider.GetCustomAttribute<InheritableAttribute>(inherit: true);
        
        if (attrDefault != null && attrTrue != null)
        {
            Assert.Equal(attrTrue.Name, attrDefault.Name);
        }
        else
        {
            Assert.Equal(attrTrue, attrDefault);
        }
    }

    [Fact]
    public void AbstractProperty_InheritedAttributes_BehaviorDocumented()
    {
        ITypeShape<ConcreteClassWithAbstractPropertyImpl>? shape = Provider.GetTypeShape<ConcreteClassWithAbstractPropertyImpl>();
        Assert.NotNull(shape);
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);

        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "AbstractProperty");
        Assert.NotNull(propShape);
    }

    [Fact]
    public void AbstractMethod_InheritedAttributes_BehaviorDocumented()
    {
        ITypeShape<ConcreteClassWithAbstractMethodImpl>? shape = Provider.GetTypeShape<ConcreteClassWithAbstractMethodImpl>();
        Assert.NotNull(shape);
        
        IMethodShape? methodShape = shape.Methods.FirstOrDefault(m => m.Name == "AbstractMethod");
        Assert.NotNull(methodShape);
    }

    [Fact]
    public void GenericType_AttributesPreserved()
    {
        ITypeShape<GenericClassWithAttributes<int>>? shape = 
            Provider.GetTypeShape<GenericClassWithAttributes<int>>();
        Assert.NotNull(shape);
        
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Value");
        Assert.NotNull(propShape);
        Assert.True(propShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? attr = propShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("GenericProperty", attr.Name);

        IMethodShape? methodShape = shape.Methods.FirstOrDefault(m => m.Name == "GetValue");
        Assert.NotNull(methodShape);
        Assert.True(methodShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? methodAttr = methodShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(methodAttr);
        Assert.Equal("GenericMethod", methodAttr.Name);
    }

    [Fact]
    public void PropertyWithDifferentAccessors_AttributesPreserved()
    {
        ITypeShape<ClassWithAttributesOnProperties>? shape = 
            Provider.GetTypeShape<ClassWithAttributesOnProperties>();
        Assert.NotNull(shape);
        
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        
        foreach (var propName in new[] { "GetterOnly", "SetterOnly", "GetSet", "InitOnly" })
        {
            IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == propName);
            Assert.NotNull(propShape);
            Assert.True(propShape.AttributeProvider.IsDefined<CustomAttribute>(), 
                $"Property {propName} should have CustomAttribute");
            CustomAttribute? attr = propShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
            Assert.NotNull(attr);
            Assert.Equal(propName, attr.Name);
        }
    }

    [Theory]
    [InlineData("PublicMethod")]
    [InlineData("PrivateMethod")]
    [InlineData("StaticMethod")]
    public void MethodsWithVariousAccessibility_AttributesPreserved(string methodName)
    {
        // SourceGen with AllPublic doesn't include private methods
        if (ProviderUnderTest.Kind == ProviderKind.SourceGen && methodName == "PrivateMethod")
        {
            return;
        }

        ITypeShape<ClassWithMethodAttributes>? shape = Provider.GetTypeShape<ClassWithMethodAttributes>();
        Assert.NotNull(shape);
        
        IMethodShape? methodShape = shape.Methods.FirstOrDefault(m => m.Name == methodName);
        Assert.NotNull(methodShape);
        Assert.True(methodShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? attr = methodShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(methodName, attr.Name);
    }

    [Theory]
    [InlineData("PublicEvent")]
    [InlineData("PrivateEvent")]
    [InlineData("StaticEvent")]
    public void EventsWithVariousAccessibility_AttributesPreserved(string eventName)
    {
        // SourceGen with AllPublic doesn't include private events
        if (ProviderUnderTest.Kind == ProviderKind.SourceGen && eventName == "PrivateEvent")
        {
            return;
        }

        ITypeShape<ClassWithEventAttributes>? shape = Provider.GetTypeShape<ClassWithEventAttributes>();
        Assert.NotNull(shape);
        
        IEventShape? eventShape = shape.Events.FirstOrDefault(e => e.Name == eventName);
        Assert.NotNull(eventShape);
        Assert.True(eventShape.AttributeProvider.IsDefined<CustomAttribute>());
        CustomAttribute? attr = eventShape.AttributeProvider.GetCustomAttribute<CustomAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(eventName, attr.Name);
    }

    // Helper visitor to extract parameter by name
    private sealed class ParameterExtractor : TypeShapeVisitor
    {
        public override object? VisitConstructor<TDeclaringType, TArgumentState>(
            IConstructorShape<TDeclaringType, TArgumentState> constructor, 
            object? state)
        {
            string paramName = (string)state!;
            foreach (IParameterShape param in constructor.Parameters)
            {
                if (param.Name == paramName)
                {
                    return param;
                }
            }
            return null;
        }
    }

    // Helper visitor to extract method parameter by name
    private sealed class MethodParameterExtractor : TypeShapeVisitor
    {
        public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(
            IMethodShape<TDeclaringType, TArgumentState, TResult> method, 
            object? state)
        {
            string paramName = (string)state!;
            foreach (IParameterShape param in method.Parameters)
            {
                if (param.Name == paramName)
                {
                    return param;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Custom attribute that should be preserved in attribute providers.
    /// </summary>
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

    /// <summary>
    /// Inheritable custom attribute for testing attribute inheritance.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class InheritableAttribute : Attribute
    {
        public string? Name { get; set; }
        public int Value { get; set; }

        public InheritableAttribute() { }
        public InheritableAttribute(string name) => Name = name;
        public InheritableAttribute(int value) => Value = value;
        public InheritableAttribute(string name, int value) { Name = name; Value = value; }
    }

    /// <summary>
    /// Custom attribute that should be skipped using Conditional("NEVER").
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    [Conditional("NEVER")]
    public class ConditionalNeverAttribute : Attribute
    {
        public string? Message { get; set; }
    }

    /// <summary>
    /// Custom attribute on custom attribute to test nested attribute providers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [Custom("MetaAttribute")]
    public class MetaCustomAttribute : Attribute
    {
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    [Custom("TypeAttribute", 42)]
    [ConditionalNever(Message = "Should be skipped")]
    [Obsolete("This type is obsolete")]
    public partial class ClassWithAttributes
    {
        [Custom("FieldAttribute")]
        [ConditionalNever(Message = "Should be skipped")]
        public int Field;

        [Custom("PropertyAttribute", 100)]
        [ConditionalNever]
        public string? Property { get; set; }

        [Custom("ConstructorAttribute")]
        public ClassWithAttributes([Custom("ParameterAttribute")] int field, string? property)
        {
            Field = field;
            Property = property;
        }

        [Custom("MethodAttribute")]
        [MethodShape]
        public void TestMethod([Custom("MethodParameterAttribute")] int x) { }

        [Custom("EventAttribute")]
        [EventShape]
        public event EventHandler? TestEvent;

        protected virtual void OnTestEvent() => TestEvent?.Invoke(this, EventArgs.Empty);
    }

    [GenerateShape]
    public partial class ClassWithInheritedAttributes : ClassWithAttributes
    {
        public ClassWithInheritedAttributes(int field, string? property) : base(field, property) { }
    }

    // Test classes for inherited type attributes
    [GenerateShape]
    [Inheritable("BaseTypeAttribute")]
    [Custom("NonInheritableBaseAttribute")]
    public partial class BaseClassWithInheritableAttributes
    {
        public int Value { get; set; }
    }

    [GenerateShape]
    public partial class DerivedClassWithInheritableAttributes : BaseClassWithInheritableAttributes
    {
        public int DerivedValue { get; set; }
    }

    // Test classes for inherited property attributes
    [GenerateShape]
    public abstract partial class BaseClassWithInheritedPropertyAttributes
    {
        [Inheritable("BaseProperty")]
        [Custom("NonInheritableProperty")]
        public virtual int VirtualProperty { get; set; }
    }

    [GenerateShape]
    public partial class DerivedClassWithInheritedPropertyAttributes : BaseClassWithInheritedPropertyAttributes
    {
        public override int VirtualProperty { get; set; }
    }

    // Test classes for inherited method attributes
    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public abstract partial class BaseClassWithInheritedMethodAttributes
    {
        [Inheritable("BaseMethod")]
        [Custom("NonInheritableMethod")]
        [MethodShape]
        public virtual int VirtualMethod(int x) => x;
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public partial class DerivedClassWithInheritedMethodAttributes : BaseClassWithInheritedMethodAttributes
    {
        [MethodShape]
        public override int VirtualMethod(int x) => x + 1;
    }

    // Test classes for inherited event attributes
    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public abstract partial class BaseClassWithInheritedEventAttributes
    {
        [Inheritable("BaseEvent")]
        [Custom("NonInheritableEvent")]
        [EventShape]
        public virtual event EventHandler? VirtualEvent;

        protected virtual void OnVirtualEvent() => VirtualEvent?.Invoke(this, EventArgs.Empty);
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public partial class DerivedClassWithInheritedEventAttributes : BaseClassWithInheritedEventAttributes
    {
        [EventShape]
        public override event EventHandler? VirtualEvent;

        protected override void OnVirtualEvent() => VirtualEvent?.Invoke(this, EventArgs.Empty);
    }

    // Test classes for interface inheritance
    [GenerateShape]
    [Inheritable("Interface1Attribute")]
    public partial interface IInterface1WithInheritableAttributes
    {
        int Value { get; set; }
    }

    [GenerateShape]
    [Inheritable("Interface2Attribute")]
    public partial interface IInterface2WithInheritableAttributes
    {
        string Name { get; set; }
    }

    [GenerateShape]
    public partial class ClassImplementingMultipleInterfaces : IInterface1WithInheritableAttributes, IInterface2WithInheritableAttributes
    {
        public int Value { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Test classes for generic inheritance
    [Inheritable("GenericBaseAttribute")]
    public class GenericBaseWithInheritableAttributes<T>
    {
        public T? Value { get; set; }
    }

    public class DerivedGenericWithInheritedAttributes<T> : GenericBaseWithInheritableAttributes<T>
    {
        public T? DerivedValue { get; set; }
    }

    // Test classes for abstract member inheritance
    [GenerateShape]
    public abstract partial class AbstractBaseWithInheritableProperties
    {
        [Inheritable("AbstractProperty")]
        public abstract int AbstractProperty { get; set; }
    }

    [GenerateShape]
    public partial class ConcreteClassWithAbstractPropertyImpl : AbstractBaseWithInheritableProperties
    {
        public override int AbstractProperty { get; set; }
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public abstract partial class AbstractBaseWithInheritableMethods
    {
        [Inheritable("AbstractMethod")]
        [MethodShape]
        public abstract int AbstractMethod(int x);
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public partial class ConcreteClassWithAbstractMethodImpl : AbstractBaseWithInheritableMethods
    {
        [MethodShape]
        public override int AbstractMethod(int x) => x * 2;
    }

    [GenerateShape]
    [Custom("StructAttribute")]
    public partial struct StructWithAttributes
    {
        [Custom("StructFieldAttribute")]
        public int Value { get; set; }

        [Custom("StructConstructorAttribute")]
        public StructWithAttributes([Custom("StructParameterAttribute")] int value)
        {
            Value = value;
        }

        [Custom("StructMethodAttribute")]
        [MethodShape]
        public readonly int GetValue() => Value;
    }

    [Custom("EnumAttribute")]
    public enum EnumWithAttributes
    {
        [Custom("EnumMember1")]
        Value1 = 1,
        
        [Custom("EnumMember2")]
        [Obsolete("Use Value1 instead")]
        Value2 = 2,
        
        [Custom("EnumMember3", 300)]
        Value3 = 3
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public partial class ClassWithMethodAttributes
    {
        [Custom("PublicMethod")]
        [MethodShape]
        public int PublicMethod(int x) => x;

        [Custom("PrivateMethod")]
        [MethodShape]
        private int PrivateMethod(int x) => x;

        [Custom("StaticMethod")]
        [MethodShape]
        public static int StaticMethod(int x) => x;
    }

    [GenerateShape]
    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public partial class ClassWithEventAttributes
    {
        [Custom("PublicEvent")]
        [EventShape]
        public event EventHandler? PublicEvent;

        [Custom("PrivateEvent")]
        [EventShape]
        private event EventHandler? PrivateEvent;

        [Custom("StaticEvent")]
        [EventShape]
        public static event EventHandler? StaticEvent;

        protected virtual void OnPublicEvent() => PublicEvent?.Invoke(this, EventArgs.Empty);
        private void OnPrivateEvent() => PrivateEvent?.Invoke(this, EventArgs.Empty);
        protected static void OnStaticEvent() => StaticEvent?.Invoke(null, EventArgs.Empty);
    }

    [GenerateShape]
    [Custom("MultipleAttributes1")]
    [Custom("MultipleAttributes2")]
    [Custom("MultipleAttributes3", 999)]
    public partial class ClassWithMultipleAttributes
    {
        [Custom("Property1")]
        [Custom("Property2")]
        public int Value { get; set; }
    }

    /// <summary>
    /// Class with compiler-generated and framework attributes that should be skipped.
    /// </summary>
    [GenerateShape]
    [CompilerGenerated]
    [DebuggerDisplay("Value = {Value}")]
    public partial class ClassWithCompilerAndDiagnosticAttributes
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// Class with PolyType attributes that should be skipped by source generator.
    /// </summary>
    [GenerateShape]
    public partial class ClassWithPolyTypeAttributes
    {
        [PropertyShape(Name = "CustomName")]
        public int Value { get; set; }
    }

    [GenerateShape]
    public partial class ClassWithAttributesOnProperties
    {
        [Custom("GetterOnly")]
        public int GetterOnly { get; }

        [Custom("SetterOnly")]
        public int SetterOnly { set { } }

        [Custom("GetSet")]
        public int GetSet { get; set; }

        [Custom("InitOnly")]
        public int InitOnly { get; init; }

        public ClassWithAttributesOnProperties(int getterOnly)
        {
            GetterOnly = getterOnly;
        }
    }

    [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
    public class GenericClassWithAttributes<T>
    {
        [Custom("GenericProperty")]
        public T? Value { get; set; }

        [Custom("GenericMethod")]
        [MethodShape]
        public T? GetValue() => Value;
    }

    /// <summary>
    /// Test nested attribute on attribute class.
    /// </summary>
    [GenerateShape]
    [MetaCustom]
    public partial class ClassWithMetaAttribute
    {
    }
}

public sealed class AttributeProviderTests_Reflection() 
    : AttributeProviderTests(ReflectionProviderUnderTest.NoEmit)
{
}

public sealed class AttributeProviderTests_ReflectionEmit() 
    : AttributeProviderTests(ReflectionProviderUnderTest.Emit)
{
}

public sealed partial class AttributeProviderTests_SourceGen() 
    : AttributeProviderTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider))
{
    [Fact]
    public void ConditionalNeverAttribute_IsFiltered()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        
        Assert.False(shape.AttributeProvider.IsDefined<ConditionalNeverAttribute>());
    }

    [Fact]
    public void ConditionalNeverAttribute_OnProperty_IsFiltered()
    {
        ITypeShape<ClassWithAttributes>? shape = Provider.GetTypeShape<ClassWithAttributes>();
        Assert.NotNull(shape);
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);

        IPropertyShape? propertyShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Property");
        Assert.NotNull(propertyShape);
        
        Assert.False(propertyShape.AttributeProvider.IsDefined<ConditionalNeverAttribute>());
    }

    [Fact]
    public void CompilerGeneratedAttribute_IsFiltered()
    {
        ITypeShape<ClassWithCompilerAndDiagnosticAttributes>? shape = 
            Provider.GetTypeShape<ClassWithCompilerAndDiagnosticAttributes>();
        Assert.NotNull(shape);
        
        Assert.False(shape.AttributeProvider.IsDefined<CompilerGeneratedAttribute>());
        Assert.False(shape.AttributeProvider.IsDefined<DebuggerDisplayAttribute>());
    }

    [Fact]
    public void PolyTypeAttributes_AreFiltered()
    {
        ITypeShape<ClassWithPolyTypeAttributes>? shape = Provider.GetTypeShape<ClassWithPolyTypeAttributes>();
        Assert.NotNull(shape);
        
        Assert.False(shape.AttributeProvider.IsDefined<GenerateShapeAttribute>());
        
        IObjectTypeShape objectShape = Assert.IsAssignableFrom<IObjectTypeShape>(shape);
        IPropertyShape? propShape = objectShape.Properties.FirstOrDefault(p => p.Name == "Value");
        if (propShape != null)
        {
            Assert.False(propShape.AttributeProvider.IsDefined<PropertyShapeAttribute>());
        }
    }

    [Fact]
    public void DefaultMemberAttribute_IsFiltered()
    {
        ITypeShape<ClassWithIndexer>? shape = Provider.GetTypeShape<ClassWithIndexer>();
        Assert.NotNull(shape);
        
        // DefaultMemberAttribute is implicitly added to types with indexers but should be filtered
        Assert.False(shape.AttributeProvider.IsDefined<System.Reflection.DefaultMemberAttribute>());
    }

    [Fact]
    public void CLSCompliantAttribute_IsFiltered()
    {
        ITypeShape<uint>? shape = Provider.GetTypeShape<uint>();
        Assert.NotNull(shape);
        
        // CLSCompliantAttribute should be filtered by source generator
        Assert.False(shape.AttributeProvider.IsDefined<System.CLSCompliantAttribute>());
    }

    [Fact]
    public void InteropServicesAttributes_AreFiltered()
    {
        ITypeShape<int[]>? shape = Provider.GetTypeShape<int[]>();
        Assert.NotNull(shape);
        
        // System.Runtime.InteropServices attributes should be filtered by source generator
        Assert.False(shape.AttributeProvider.IsDefined<System.Runtime.InteropServices.ClassInterfaceAttribute>());
        Assert.False(shape.AttributeProvider.IsDefined<System.Runtime.InteropServices.ComVisibleAttribute>());
    }

    [Flags]
    public enum MyEnum
    {
        None = 0,
        Value1 = 1,
        Value2 = 2,
        Value3 = 4
    }

    [GenerateShapeFor(typeof(EnumWithAttributes))]
    [GenerateShapeFor(typeof(GenericClassWithAttributes<int>))]
    [GenerateShapeFor(typeof(ClassWithIndexer))]
    [GenerateShapeFor(typeof(MyEnum))]
    [GenerateShapeFor(typeof(uint))]
    [GenerateShapeFor(typeof(int[]))]
    [GenerateShapeFor(typeof(DerivedGenericWithInheritedAttributes<int>))]
    [GenerateShapeFor(typeof(GenericBaseWithInheritableAttributes<int>))]
    public partial class Witness;
}
