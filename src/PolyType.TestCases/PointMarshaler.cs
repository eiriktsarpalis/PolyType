using PolyType;
using PolyType.Tests;
using System.Drawing;

[assembly: TypeShapeExtension(typeof(Point), Marshaler = typeof(PointMarshaler))]

namespace PolyType.Tests;

public class PointMarshaler : IMarshaler<Point, PointMarshaler.PointSurrogate>
{
    public Point Unmarshal(PointSurrogate surrogate) => new(surrogate.X - 1, surrogate.Y);

    public PointSurrogate Marshal(Point value) => new(value.X + 1, value.Y);

    public record struct PointSurrogate(int X, int Y);
}
