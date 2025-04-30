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

                public record struct PointSurrogate(int X, int Y);
            }

            public class PointMarshaller2: IMarshaller<Point, PointMarshaller2.PointSurrogate>
            {
                public Point FromSurrogate(PointSurrogate surrogate) => throw new System.NotImplementedException();

                public PointSurrogate ToSurrogate(Point value) => throw new System.NotImplementedException();

                public record struct PointSurrogate(int X, int Y);
            }

            [GenerateShape<Point>]
            partial class Witness;
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

                public record struct PointSurrogate(int X, int Y);
            }

            public class SizeMarshaller: IMarshaller<Size, SizeMarshaller.SizeSurrogate>
            {
                public Size FromSurrogate(SizeSurrogate surrogate) => throw new System.NotImplementedException();

                public SizeSurrogate ToSurrogate(Size value) => throw new System.NotImplementedException();

                public record struct SizeSurrogate(int X, int Y);
            }

            [GenerateShape<Point>]
            partial class Witness;
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task CompatibleTypeExtensions()
    {
        string source = /* lang=c#-test */ """
            using System.Drawing;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(Point), Marshaller = typeof(PointMarshaller))]
            [assembly: TypeShapeExtension(typeof(Point), AssociatedTypes = [typeof(SomeOtherType)])]

            public class PointMarshaller : IMarshaller<Point, PointMarshaller.PointSurrogate>
            {
                public Point FromSurrogate(PointSurrogate surrogate) => throw new System.NotImplementedException();

                public PointSurrogate ToSurrogate(Point value) => throw new System.NotImplementedException();

                public record struct PointSurrogate(int X, int Y);
            }

            public class SomeOtherType;

            [GenerateShape<Point>]
            partial class Witness;
            """;

        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
