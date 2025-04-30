using PolyType;
using PolyType.Tests;
using System.Drawing;

[assembly: TypeShapeExtension(typeof(Point), Marshaller = typeof(PointMarshaller))]

namespace PolyType.Tests;

public class PointMarshaller : IMarshaller<Point, PointMarshaller.PointSurrogate>
{
    public Point FromSurrogate(PointSurrogate surrogate) => new(surrogate.X - 1, surrogate.Y);

    public PointSurrogate ToSurrogate(Point value) => new(value.X + 1, value.Y);

    public record struct PointSurrogate(int X, int Y);
}
