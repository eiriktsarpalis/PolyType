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

            [assembly: TypeShapeExtension(typeof(Point), Marshaller = typeof(PointMarshaller))]
            [assembly: TypeShapeExtension(typeof(Point), Marshaller = {|PT0018:typeof(PointMarshaller2)|})]

            public class PointMarshaller : IMarshaller<Point, PointMarshaller.PointSurrogate>
            {
                public Point FromSurrogate(PointSurrogate surrogate) => throw new System.NotImplementedException();

                public PointSurrogate ToSurrogate(Point value) => throw new System.NotImplementedException();

                public struct PointSurrogate
                {
                    public PointSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                }
            }

            public class PointMarshaller2: IMarshaller<Point, PointMarshaller2.PointSurrogate>
            {
                public Point FromSurrogate(PointSurrogate surrogate) => throw new System.NotImplementedException();

                public PointSurrogate ToSurrogate(Point value) => throw new System.NotImplementedException();

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

            [assembly: TypeShapeExtension(typeof(Point), Marshaller = typeof(PointMarshaller))]
            [assembly: TypeShapeExtension(typeof(Size), Marshaller = typeof(SizeMarshaller))]

            public class PointMarshaller : IMarshaller<Point, PointMarshaller.PointSurrogate>
            {
                public Point FromSurrogate(PointSurrogate surrogate) => throw new System.NotImplementedException();

                public PointSurrogate ToSurrogate(Point value) => throw new System.NotImplementedException();

                public struct PointSurrogate
                {
                    public PointSurrogate(int x, int y) => (X, Y) = (x, y);
                    public int X { get; }
                    public int Y { get; }
                };
            }

            public class SizeMarshaller: IMarshaller<Size, SizeMarshaller.SizeSurrogate>
            {
                public Size FromSurrogate(SizeSurrogate surrogate) => throw new System.NotImplementedException();

                public SizeSurrogate ToSurrogate(Size value) => throw new System.NotImplementedException();

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

            [assembly: TypeShapeExtension(typeof(Point), Marshaller = typeof(PointMarshaller))]
            [assembly: TypeShapeExtension(typeof(Point), AssociatedTypes = new Type[] { typeof(SomeOtherType) })]

            public class PointMarshaller : IMarshaller<Point, PointMarshaller.PointSurrogate>
            {
                public Point FromSurrogate(PointSurrogate surrogate) => throw new System.NotImplementedException();

                public PointSurrogate ToSurrogate(Point value) => throw new System.NotImplementedException();

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
