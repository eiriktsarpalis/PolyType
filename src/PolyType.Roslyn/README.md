# PolyType.Roslyn

Exposes an opinionated set of components for extracting data models from types using the Roslyn symbol API.

Shared source-generation utilities include type identifiers with generic arguments, tuple flattening, array ranks, namespace qualification, and keyword escaping. The built-in PolyType generator and the independent PolyType.FastPathJson prototype consume the same implementation without sharing generator-specific code.
