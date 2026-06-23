using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using PolyType.Abstractions;

namespace PolyType.Examples.CborSerializer;

/// <summary>
/// Extensions for the <see cref="CborSerializer"/> type.
/// </summary>
public static class CborSerializerExtensions
{
    extension(CborSerializer)
    {
        /// <summary>
        /// Builds an <see cref="CborConverter{T}"/> instance from the specified shape.
        /// </summary>
        /// <typeparam name="T">The type for which to build the converter.</typeparam>
        /// <returns>An <see cref="CborConverter{T}"/> instance.</returns>
        /// <exception cref="NotSupportedException">No source generated implementation for <typeparamref name="T"/> was found.</exception>
#if NET8_0
        [RequiresDynamicCode("Dynamic resolution of IShapeable<T> interface may require dynamic code generation in .NET 8 Native AOT. It is recommended to switch to statically resolved IShapeable<T> APIs or upgrade your app to .NET 9 or later.")]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that T actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If T is declared in an assembly that does not target .NET, use {nameof(CborSerializer)}.{nameof(CborSerializer.CreateConverter)}({nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamicOrThrow)}<T>()) instead.", error: true)]
#endif
        public static CborConverter<T> CreateConverter<T>()
             => CborSerializer.CreateConverter(TypeShapeResolver.Resolve<T>());
    }
}
