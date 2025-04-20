using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace PolyType.SourceGenerator.Model;

public record struct AssociatedTypeId(string Open, string Closed, string CSharpTypeName, TypeId ClosedTypeId);
