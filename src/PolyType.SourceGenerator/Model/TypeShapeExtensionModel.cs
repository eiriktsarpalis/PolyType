using PolyType.Roslyn;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace PolyType.SourceGenerator.Model;

public sealed record TypeShapeExtensionModel
{
    public required TypeId Target { get; init; }

    /// <summary>
    /// A map of type IDs for associated types and their requirements.
    /// </summary>
    public required ImmutableEquatableDictionary<AssociatedTypeId, EquatableEnum<TypeShapeDepth>> AssociatedTypes { get; init; }
}
