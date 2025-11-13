using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

[Generator]
public sealed class PolyTypeGenerator : IIncrementalGenerator
{
    public static string SourceGeneratorName { get; } = typeof(PolyTypeGenerator).FullName;
    public static string SourceGeneratorVersion { get; } = typeof(SourceFormatter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        if (Environment.GetEnvironmentVariable("POLYTYPE_LAUNCH_DEBUGGER_ON_START") is "1")
        {
            Debugger.Launch();
        }
#pragma warning restore RS1035 // Do not use APIs banned for analyzers

        IncrementalValueProvider<PolyTypeKnownSymbols> knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new PolyTypeKnownSymbols(compilation));

        IncrementalValueProvider<TypeShapeProviderModel?> providerModel = context.SyntaxProvider
            .ForTypesWithAttributeDeclarations(
                attributeFullyQualifiedNames: ["PolyType.GenerateShapeForAttribute<T>", "PolyType.GenerateShapeForAttribute", "PolyType.GenerateShapeAttribute"],
                (node, _) => node is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => DebugGuard(() => Parser.ParseFromGenerateShapeAttributes(tuple.Left, tuple.Right, token)));

        context.RegisterSourceOutput(providerModel, (ctxt, model) => DebugGuard(() => GenerateSource(ctxt, model)));
    }

    private void GenerateSource(SourceProductionContext context, TypeShapeProviderModel? provider)
    {
        if (provider is null)
        {
            return;
        }

        OnGeneratingSource?.Invoke(provider);

        foreach (EquatableDiagnostic diagnostic in provider.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.CreateDiagnostic());
        }

        SourceFormatter.GenerateSourceFiles(context, provider);
    }

    public Action<TypeShapeProviderModel>? OnGeneratingSource { get; init; }

    private static bool MaybeLaunchDebuggerButNeverHandleException()
    {
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        if (Environment.GetEnvironmentVariable("POLYTYPE_LAUNCH_DEBUGGER_ON_EXCEPTION") is "1")
        {
            Debugger.Launch();
        }
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
        return false;
    }

    private static void DebugGuard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception) when (MaybeLaunchDebuggerButNeverHandleException())
        {
            // Never runs.
        }
    }

    private static T DebugGuard<T>(Func<T> func)
    {
        try
        {
            return func();
        }
        catch (Exception) when (MaybeLaunchDebuggerButNeverHandleException())
        {
            // Never runs.
            throw;
        }
    }
}
