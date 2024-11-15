﻿using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

public abstract record TypeShapeModel
{
    public required TypeId Type { get; init; }
}
