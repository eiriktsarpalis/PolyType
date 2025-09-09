using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace PolyType.SourceGenerator.Model;

public record struct AssociatedTypeId(TypeId ClosedTypeId, string ClosedTypeReflectionName, (TypeId TypeId, string ReflectionName)? OpenTypeInfo);
