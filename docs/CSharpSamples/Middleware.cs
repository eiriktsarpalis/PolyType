using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PolyType.Abstractions;

#if NET
#region MyFancyParser
public static class MyFancyParser
{
    public static T? Parse<T>(string myFancyFormat) where T : IShapeable<T> => throw new NotImplementedException();
}
#endregion

namespace MyFancyParserUser
{
    #region Person
    [GenerateShape] // Generate an IShapeable<Person> implementation
    partial record Person(string name, int age, List<Person> children);
    #endregion

    class User
    {
        static void Container()
        {
            #region MyFancyParserUser
            string myFancyFormat = "..."; // Some format string
            Person? person = MyFancyParser.Parse<Person>(myFancyFormat); // Compiles
            #endregion
        }
    }
}
#endif

#region IdealMultitargetingAPI
internal class ShapeProcessor
{
    internal const string ResolveDynamicMessage =
        "Dynamic resolution of IShapeable<T> interface may require dynamic code generation in .NET 8 Native AOT. " +
        "It is recommended to switch to statically resolved IShapeable<T> APIs or upgrade your app to .NET 9 or later.";

    /// <summary>
    /// Processes a shape of type T.
    /// </summary>
    /// <typeparam name="T">The type to operate on.</typeparam>
    /// <param name="shape">The shape to process.</param>
    public static void ConsumeShape<T>(ITypeShape<T> shape)
    {
        // Your interesting code goes here.
    }

#if NET
    /// <inheritdoc cref="ShapeProcessorExtensions.ConsumeShape{T}()"/>
    public static void ConsumeShape<T>() where T : IShapeable<T> => ConsumeShape(T.GetTypeShape());

    /// <inheritdoc cref="ShapeProcessorExtensions.ConsumeShape{T, TProvider}()"/>
    public static void ConsumeShape<T, TProvider>() where TProvider : IShapeable<T> => ConsumeShape(TProvider.GetTypeShape());
#endif
}

internal static class ShapeProcessorExtensions
{
    extension(ShapeProcessor)
    {
        /// <inheritdoc cref="ShapeProcessor.ConsumeShape{T}(ITypeShape{T})"/>
#if NET8_0
        [RequiresDynamicCode(ShapeProcessor.ResolveDynamicMessage)]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that T actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If T is declared in an assembly that does not target .NET, use {nameof(ShapeProcessor)}.{nameof(ShapeProcessor.ConsumeShape)}({nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamic)}<T>()) instead.", error: true)]
#endif
        public static void ConsumeShape<T>() => ShapeProcessor.ConsumeShape(TypeShapeResolver.Resolve<T>());

        /// <inheritdoc cref="ShapeProcessor.ConsumeShape{T}(ITypeShape{T})"/>
        /// <typeparam name="TProvider">The type that provides the shape for T.</typeparam>
#if NET8_0
        [RequiresDynamicCode(ShapeProcessor.ResolveDynamicMessage)]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that T actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If T is declared in an assembly that does not target .NET, use {nameof(ShapeProcessor)}.{nameof(ShapeProcessor.ConsumeShape)}({nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamic)}<T, TProvider>()) instead.", error: true)]
#endif
        public static void ConsumeShape<T, TProvider>() => ShapeProcessor.ConsumeShape(TypeShapeResolver.Resolve<T, TProvider>());
    }
}
#endregion
