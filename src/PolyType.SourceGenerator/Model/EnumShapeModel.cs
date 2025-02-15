﻿namespace PolyType.SourceGenerator.Model;

public sealed record EnumShapeModel : TypeShapeModel
{
    public required TypeId UnderlyingType { get; init; }
}
