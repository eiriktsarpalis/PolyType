using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private bool _needsConstructorInvoker;
    private readonly Dictionary<string, (GenericTypeModel Type, List<string> Declarations)> _genericAccessorDeclarations = new(StringComparer.Ordinal);

    private string SafeModifier => provider.UsesUpdatedMemorySafetyRules ? "safe " : "";

    private static string GetGenericAccessorClassName(TypeShapeModel type, int declaringTypeIndex)
        => $"__GenericAccessors_{type.SourceIdentifier}_{declaringTypeIndex}";

    private static string QualifyAccessorName(TypeShapeModel type, string name, int declaringTypeIndex, GenericTypeModel? genericType)
        => genericType is null ? name : $"{GetGenericAccessorClassName(type, declaringTypeIndex)}<{string.Join(", ", genericType.TypeArguments)}>.{name}";

    private string GetUnsafeAccessorModifiers(GenericTypeModel? genericType)
        => $"{(genericType is null ? "private" : "public")} static {SafeModifier}extern";

    private void FormatUnsafeAccessor(SourceWriter writer, TypeShapeModel type, int declaringTypeIndex, GenericTypeModel? genericType, string declaration)
    {
        if (genericType is null)
        {
            writer.WriteLine();
            writer.WriteLine(declaration);
            return;
        }

        string className = GetGenericAccessorClassName(type, declaringTypeIndex);
        if (!_genericAccessorDeclarations.TryGetValue(className, out var group))
        {
            group = (genericType, []);
            _genericAccessorDeclarations.Add(className, group);
        }

        group.Declarations.Add(declaration);
    }

    private void FormatGenericAccessorClasses(SourceWriter writer)
    {
        foreach (var entry in _genericAccessorDeclarations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            (GenericTypeModel genericType, List<string> declarations) = entry.Value;
            writer.WriteLine();
            writer.WriteLine($"private static class {entry.Key}<{string.Join(", ", genericType.TypeParameters)}>");
            if (genericType.ConstraintClauses.Length > 0)
            {
                writer.Indentation++;
                writer.WriteLine(genericType.ConstraintClauses);
                writer.Indentation--;
            }

            writer.WriteLine('{');
            writer.Indentation++;
            for (int i = 0; i < declarations.Count; i++)
            {
                if (i > 0)
                {
                    writer.WriteLine();
                }

                writer.WriteLine(declarations[i]);
            }

            writer.Indentation--;
            writer.WriteLine('}');
        }

        _genericAccessorDeclarations.Clear();
    }

    private static string FormatAccessorParameters(ImmutableEquatableArray<ParameterShapeModel> parameters, bool useOpenTypes)
    {
        return string.Join(", ", parameters.Select(parameter =>
        {
            string modifier = parameter.RefKind switch
            {
                RefKind.Ref or RefReadOnlyParameter => "ref ",
                RefKind.In => "in ",
                RefKind.Out => "out ",
                _ => "",
            };

            string typeName = useOpenTypes ? parameter.OpenParameterTypeName ?? parameter.ParameterType.FullyQualifiedName : parameter.ParameterType.FullyQualifiedName;
            return $"{modifier}{typeName} {FormatAccessorParameterName(parameter)}";
        }));
    }

    private static string FormatAccessorArguments(ImmutableEquatableArray<ParameterShapeModel> parameters)
    {
        return string.Join(", ", parameters.Select(parameter =>
        {
            string modifier = parameter.RefKind switch
            {
                RefKind.Ref or RefReadOnlyParameter => "ref ",
                RefKind.In => "in ",
                RefKind.Out => "out ",
                _ => "",
            };

            return $"{modifier}{FormatAccessorParameterName(parameter)}";
        }));
    }

    private static string FormatAccessorParameterName(ParameterShapeModel parameter)
        => RoslynHelpers.EscapeKeywordIdentifier(parameter.UnderlyingMemberName);

    private static string GetAccessorLocalName(ImmutableEquatableArray<ParameterShapeModel> parameters, string name, GenericTypeModel? genericType = null)
    {
        while (parameters.Any(parameter => parameter.UnderlyingMemberName == name) ||
            genericType?.TypeParameters.Contains(name) is true)
        {
            name += "_";
        }

        return name;
    }

    private string FormatAccessorMemberName(ImmutableEquatableArray<ParameterShapeModel> parameters, string name)
        => parameters.Any(parameter => parameter.UnderlyingMemberName == name) ? $"{provider.ProviderDeclaration.Id.FullyQualifiedName}.{name}" : name;

    private void FormatConstructorInvoker(SourceWriter writer)
    {
        if (!_needsConstructorInvoker)
        {
            return;
        }

        writer.WriteLine();
        writer.WriteLine("""
            private static object __InvokeConstructor(global::System.Reflection.ConstructorInfo constructor, object?[] parameters)
            {
            """);
        writer.Indentation++;

        if (provider.SupportsDoNotWrapExceptions)
        {
            writer.WriteLine("return constructor.Invoke(global::System.Reflection.BindingFlags.DoNotWrapExceptions, null, parameters, null);");
        }
        else
        {
            writer.WriteLine("""
                try
                {
                    return constructor.Invoke(parameters);
                }
                catch (global::System.Reflection.TargetInvocationException exception) when (exception.InnerException is { } innerException)
                {
                    global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(innerException).Throw();
                    throw;
                }
                """);
        }

        writer.Indentation--;
        writer.WriteLine('}');
    }
}
