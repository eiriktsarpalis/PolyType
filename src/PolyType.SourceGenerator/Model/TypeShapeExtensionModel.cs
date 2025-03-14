using PolyType.Roslyn;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace PolyType.SourceGenerator.Model;

public sealed record TypeShapeExtensionModel
{
    public required TypeId Target { get; init; }

    public required ImmutableEquatableArray<TypeId> AssociatedTypes { get; init; }
}
