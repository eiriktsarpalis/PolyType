using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using PolyType.Abstractions;

namespace PolyType.Examples.JsonSerializer;

/// <summary>
/// Extensions for the <see cref="JsonSerializerTS"/> type.
/// </summary>
public static class JsonSerializerTSExtensions
{
    extension(JsonSerializerTS)
    {
        /// <summary>
        /// Builds a <see cref="JsonConverter{T}"/> instance from the specified shape.
        /// </summary>
        /// <typeparam name="T">The type for which to build the converter.</typeparam>
        /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
        /// <exception cref="NotSupportedException">No source generated implementation for <typeparamref name="T"/> was found.</exception>
#if NET8_0
        [RequiresDynamicCode("Dynamic resolution of IShapeable<T> interface may require dynamic code generation in .NET 8 Native AOT. It is recommended to switch to statically resolved IShapeable<T> APIs or upgrade your app to .NET 9 or later.")]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that T actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If T is declared in an assembly that does not target .NET, use {nameof(JsonSerializerTS)}.{nameof(JsonSerializerTS.CreateConverter)}({nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamic)}<T>()) instead.", error: true)]
#endif
        public static JsonConverter<T> CreateConverter<T>() =>
            JsonSerializerTS.CreateConverter(TypeShapeResolver.Resolve<T>());
    }
}
