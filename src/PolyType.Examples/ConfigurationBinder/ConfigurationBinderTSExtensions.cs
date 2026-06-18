using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Configuration;
using PolyType.Abstractions;

namespace PolyType.Examples.ConfigurationBinder;

/// <summary>
/// Extensions for the <see cref="ConfigurationBinderTS"/> type.
/// </summary>
public static class ConfigurationBinderTSExtensions
{
    extension(ConfigurationBinderTS)
    {
        /// <summary>
        /// Builds a configuration binder delegate instance from the specified shape provider.
        /// </summary>
        /// <typeparam name="T">The type for which to build the binder.</typeparam>
        /// <returns>A configuration binder delegate.</returns>
        /// <exception cref="NotSupportedException">No source generated implementation for <typeparamref name="T"/> was found.</exception>
#if NET8_0
        [RequiresDynamicCode("Dynamic resolution of IShapeable<T> interface may require dynamic code generation in .NET 8 Native AOT. It is recommended to switch to statically resolved IShapeable<T> APIs or upgrade your app to .NET 9 or later.")]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that T actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If T is declared in an assembly that does not target .NET, use {nameof(ConfigurationBinderTS)}.{nameof(ConfigurationBinderTS.Create)}({nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamicOrThrow)}<T>()) instead.", error: true)]
#endif
        public static Func<IConfiguration, T?> Create<T>() => ConfigurationBinderTS.Create(TypeShapeResolver.Resolve<T>());
    }
}
