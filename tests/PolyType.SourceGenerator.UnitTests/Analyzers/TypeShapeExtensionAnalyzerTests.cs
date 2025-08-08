using PolyType.SourceGenerator.Analyzers;

namespace PolyType.SourceGenerator.UnitTests.Analyzers;

using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = CodeFixVerifier<TypeShapeExtensionAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class TypeShapeExtensionAnalyzerTests
{
    [Fact]
    public async Task IncompatibleTypeExtensions()
    {
        string source = /* lang=c#-test */ """
            using System.Drawing;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(Point), Marshaler = typeof(PointMarshaler))]
            [assembly: TypeShapeExtension(typeof(Point), Marshaler = {|PT0018:typeof(PointMarshaler2)|})]

            public class PointMarshaler : IMarshaler<Point, PointMarshaler.PointSurrogate>
            {
                public Point Unmarshal(PointSurrogate value) => throw new System.NotImplementedException();

                public PointSurrogate Marshal(Point value) => throw new System.NotImplementedException();

                public struct PointSurrogate
                {
                    public PointSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                }
            }

            public class PointMarshaler2: IMarshaler<Point, PointMarshaler2.PointSurrogate>
            {
                public Point Unmarshal(PointSurrogate value) => throw new System.NotImplementedException();

                public PointSurrogate Marshal(Point value) => throw new System.NotImplementedException();

                public struct PointSurrogate
                {
                    public PointSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                }
            }

            [GenerateShapeFor(typeof(Point))]
            partial class Witness { }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ExtendingMultipleTypes()
    {
        string source = /* lang=c#-test */ """
            using System.Drawing;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(Point), Marshaler = typeof(PointMarshaler))]
            [assembly: TypeShapeExtension(typeof(Size), Marshaler = typeof(SizeMarshaler))]

            public class PointMarshaler : IMarshaler<Point, PointMarshaler.PointSurrogate>
            {
                public Point Unmarshal(PointSurrogate value) => throw new System.NotImplementedException();

                public PointSurrogate Marshal(Point value) => throw new System.NotImplementedException();

                public struct PointSurrogate
                {
                    public PointSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                };
            }

            public class SizeMarshaler: IMarshaler<Size, SizeMarshaler.SizeSurrogate>
            {
                public Size Unmarshal(SizeSurrogate value) => throw new System.NotImplementedException();

                public SizeSurrogate Marshal(Size value) => throw new System.NotImplementedException();

                public struct SizeSurrogate
                {
                    public SizeSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                };
            }

            [GenerateShapeFor(typeof(Point))]
            partial class Witness { }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task CompatibleTypeExtensions()
    {
        string source = /* lang=c#-test */ """
            using System;
            using System.Drawing;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(Point), Marshaler = typeof(PointMarshaler))]
            [assembly: TypeShapeExtension(typeof(Point), AssociatedTypes = new Type[] { typeof(SomeOtherType) })]

            public class PointMarshaler : IMarshaler<Point, PointMarshaler.PointSurrogate>
            {
                public Point Unmarshal(PointSurrogate value) => throw new System.NotImplementedException();

                public PointSurrogate Marshal(Point value) => throw new System.NotImplementedException();

                public struct PointSurrogate
                {
                    public PointSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                }
            }

            public class SomeOtherType { }

            [GenerateShapeFor(typeof(Point))]
            partial class Witness { }
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
