﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using PolyType.SourceGenerator;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.SourceGenerator.UnitTests;

internal class CodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public static DiagnosticResult Diagnostic()
         => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic();

    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, DefaultVerifier>.Diagnostic(diagnosticId);

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => new DiagnosticResult(descriptor);

    public static Task VerifyAnalyzerAsync([StringSyntax("c#-test")] string source, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, [StringSyntax("c#-test")] string fixedSource)
        => VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

    public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, DiagnosticResult expected, [StringSyntax("c#-test")] string fixedSource)
        => VerifyCodeFixAsync(source, new[] { expected }, fixedSource);

    public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, DiagnosticResult[] expected, [StringSyntax("c#-test")] string fixedSource)
    {
        var test = new Test
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string[] source, [StringSyntax("c#-test")] string[] fixedSource)
    {
        var test = new Test
        {
        };

        foreach (var src in source)
        {
            test.TestState.Sources.Add(src);
        }

        foreach (var src in fixedSource)
        {
            test.FixedState.Sources.Add(src);
        }

        return test.RunAsync();
    }

    internal class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        internal Test()
        {
            this.ReferenceAssemblies = ReferencesHelper.References;
            this.CompilerDiagnostics = CompilerDiagnostics.Warnings;
            this.TestState.AdditionalReferences.AddRange(ReferencesHelper.GetReferences());
            this.FixedState.AdditionalReferences.AddRange(ReferencesHelper.GetReferences());

            this.TestState.AdditionalFilesFactories.Add(() =>
            {
                const string additionalFilePrefix = "AdditionalFiles.";
                return from resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames()
                       where resourceName.StartsWith(additionalFilePrefix, StringComparison.Ordinal)
                       let content = ReadManifestResource(Assembly.GetExecutingAssembly(), resourceName)
                       select (filename: resourceName.Substring(additionalFilePrefix.Length), SourceText.From(content));
            });
        }

        protected override ParseOptions CreateParseOptions()
        {
            return ((CSharpParseOptions)base.CreateParseOptions())
#if NET
				.WithPreprocessorSymbols("NET")
#endif
                .WithLanguageVersion(CompilationHelpers.DefaultLanguageVersion);
        }

        protected override CompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = (CSharpCompilationOptions)base.CreateCompilationOptions();
            return compilationOptions
                .WithWarningLevel(99)
                .WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions
                    .SetItem("CS1591", ReportDiagnostic.Suppress) // documentation required
                    .SetItem("CS1701", ReportDiagnostic.Suppress) // binding redirects
                    .SetItem("CS1702", ReportDiagnostic.Suppress) // binding redirects
                    .SetItem("CS0169", ReportDiagnostic.Suppress) // unused field
                    .SetItem("CS0414", ReportDiagnostic.Suppress)); // field assigned but never used
        }

        protected override IEnumerable<Type> GetSourceGenerators()
        {
            foreach (Type sg in base.GetSourceGenerators())
            {
                yield return sg;
            }

            yield return typeof(PolyTypeGenerator);
        }

        private static string ReadManifestResource(Assembly assembly, string resourceName)
        {
            using (var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName) ?? throw new ArgumentException("No such resource stream", nameof(resourceName))))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
