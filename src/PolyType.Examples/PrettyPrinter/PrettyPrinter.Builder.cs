using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Utilities;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace PolyType.Examples.PrettyPrinter;

public static partial class PrettyPrinter
{
    private sealed class Builder(ITypeShapeFunc self) : TypeShapeVisitor, ITypeShapeFunc
    {
        private static readonly Dictionary<Type, object> s_defaultPrinters = CreateDefaultPrinters().ToDictionary();

        /// <summary>Recursively looks up or creates a printer for the specified shape.</summary>
        public PrettyPrinter<T> GetOrAddPrettyPrinter<T>(ITypeShape<T> typeShape) =>
            (PrettyPrinter<T>)self.Invoke(typeShape)!;

        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? _)
        {
            if (s_defaultPrinters.TryGetValue(typeShape.Type, out object? defaultPrinter))
            {
                return defaultPrinter;
            }

            return typeShape.Accept(this);
        }

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            string typeName = FormatTypeName(typeof(T));
            PrettyPrinter<T>[] propertyPrinters = type.Properties
                .Where(prop => prop.HasGetter)
                .Select(prop => (PrettyPrinter<T>?)prop.Accept(this)!)
                .Where(prop => prop != null)
                .ToArray();

            return new PrettyPrinter<T>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Write("null");
                    return;
                }

                sb.Write("new ");
                sb.Write(typeName);

                if (propertyPrinters.Length == 0)
                {
                    sb.Write("()");
                    return;
                }

                WriteLine(sb, indentation);
                sb.Write('{');
                for (int i = 0; i < propertyPrinters.Length; i++)
                {
                    WriteLine(sb, indentation + 1);
                    propertyPrinters[i](sb, indentation + 1, value);
                    if (i < propertyPrinters.Length - 1)
                    {
                        sb.Write(',');
                    }
                }

                WriteLine(sb, indentation);
                sb.Write('}');
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            PrettyPrinter<TPropertyType> propertyTypePrinter = GetOrAddPrettyPrinter(property.PropertyType);
            return new PrettyPrinter<TDeclaringType>((sb, indentation, obj) =>
            {
                DebugExt.Assert(obj != null);
                sb.Write(property.Name);
                sb.Write(" = ");
                propertyTypePrinter(sb, indentation, getter(ref obj));
            });
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableShape.GetGetPotentiallyBlockingEnumerable();
            PrettyPrinter<TElement> elementPrinter = GetOrAddPrettyPrinter(enumerableShape.ElementType);
            bool valuesArePrimitives = s_defaultPrinters.ContainsKey(typeof(TElement));

            return new PrettyPrinter<TEnumerable>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Write("null");
                    return;
                }

                sb.Write('[');

                bool containsElements = false;
                if (valuesArePrimitives)
                {
                    foreach (TElement element in enumerableGetter(value))
                    {
                        if (containsElements)
                        {
                            sb.Write(", ");
                        }

                        elementPrinter(sb, indentation, element);
                        containsElements = true;
                    }
                }
                else
                {
                    foreach (TElement element in enumerableGetter(value))
                    {
                        if (containsElements)
                        {
                            sb.Write(',');
                        }

                        WriteLine(sb, indentation + 1);
                        elementPrinter(sb, indentation + 1, element);
                        containsElements = true;
                    }

                    WriteLine(sb, indentation);
                }

                sb.Write(']');
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            string typeName = FormatTypeName(typeof(TDictionary));
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryShape.GetGetDictionary();
            PrettyPrinter<TKey> keyPrinter = GetOrAddPrettyPrinter(dictionaryShape.KeyType);
            PrettyPrinter<TValue> valuePrinter = GetOrAddPrettyPrinter(dictionaryShape.ValueType);

            return new PrettyPrinter<TDictionary>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Write("null");
                    return;
                }

                sb.Write("new ");
                sb.Write(typeName);

                IReadOnlyDictionary<TKey, TValue> dictionary = dictionaryGetter(value);

                if (dictionary.Count == 0)
                {
                    sb.Write("()");
                    return;
                }

                WriteLine(sb, indentation);
                sb.Write('{');
                bool first = true;
                foreach (KeyValuePair<TKey, TValue> kvp in dictionaryGetter(value))
                {
                    if (!first)
                    {
                        sb.Write(',');
                    }

                    WriteLine(sb, indentation + 1);
                    sb.Write('[');
                    keyPrinter(sb, indentation + 1, kvp.Key); // TODO non-primitive key indentation
                    sb.Write("] = ");
                    valuePrinter(sb, indentation + 1, kvp.Value);
                    first = false;
                }

                WriteLine(sb, indentation);
                sb.Write('}');
            });
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return new PrettyPrinter<TEnum>((sb, _, e) =>
            {
                sb.Write(typeof(TEnum).Name);
                sb.Write('.');
                sb.Write(e);
            });
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            PrettyPrinter<TElement> elementPrinter = GetOrAddPrettyPrinter(optionalShape.ElementType);
            var deconstructor = optionalShape.GetDeconstructor();
            return new PrettyPrinter<TOptional>((sb, indentation, value) =>
            {
                if (!deconstructor(value, out TElement? element))
                {
                    sb.Write("null");
                }
                else
                {
                    elementPrinter(sb, indentation, element);
                }
            });
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        {
            PrettyPrinter<TSurrogate> surrogatePrinter = GetOrAddPrettyPrinter(surrogateShape.SurrogateType);
            var marshaler = surrogateShape.Marshaler;
            return new PrettyPrinter<T>((sb, indentation, t) => surrogatePrinter(sb, indentation, marshaler.Marshal(t)));
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseCasePrinter = (PrettyPrinter<TUnion>)unionShape.BaseType.Accept(this)!;
            var unionCasePrinters = unionShape.UnionCases
                .Select(unionCase => (PrettyPrinter<TUnion>)unionCase.Accept(this)!)
                .ToArray();

            return new PrettyPrinter<TUnion>((sb, indentation, value) =>
            {
                if (value is null)
                {
                    sb.Write("null");
                    return;
                }

                int index = getUnionCaseIndex(ref value);
                var derivedPrinter = index < 0 ? baseCasePrinter : unionCasePrinters[index];
                derivedPrinter(sb, indentation, value);
            });
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
        {
            var underlying = (PrettyPrinter<TUnionCase>)unionCaseShape.UnionCaseType.Accept(this)!;
            var marshaler = unionCaseShape.Marshaler;
            return new PrettyPrinter<TUnion>((sb, indentation, value) => underlying(sb, indentation, marshaler.Unmarshal(value)));
        }

        private static void WriteLine(TextWriter builder, int indentation)
        {
            builder.WriteLine();
            builder.Write(new string(' ', 2 * indentation));
        }

        private static void WriteStringLiteral(TextWriter builder, string value)
        {
            builder.Write('\"');
            builder.Write(value);
            builder.Write('\"');
        }

        private static void WriteStringLiteral(TextWriter builder, object value)
        {
            builder.Write('\"');
            builder.Write(value);
            builder.Write('\"');
        }

        private static string FormatTypeName(Type type)
        {
            Debug.Assert(!type.IsArray || type.IsPointer || type.IsGenericTypeDefinition);
            if (type.IsGenericType)
            {
                string paramNames = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
                return $"{type.Name.Split('`')[0]}<{paramNames}>";
            }

            return type.Name;
        }

        private static IEnumerable<KeyValuePair<Type, object>> CreateDefaultPrinters()
        {
            yield return Create<bool>((builder, _, b) => builder.Write(b ? "true" : "false"));

            yield return Create<byte>((builder, _, i) => builder.Write(i));
            yield return Create<ushort>((builder, _, i) => builder.Write(i));
            yield return Create<uint>((builder, _, i) => builder.Write(i));
            yield return Create<ulong>((builder, _, i) => builder.Write(i));

            yield return Create<sbyte>((builder, _, i) => builder.Write(i));
            yield return Create<short>((builder, _, i) => builder.Write(i));
            yield return Create<int>((builder, _, i) => builder.Write(i));
            yield return Create<long>((builder, _, i) => builder.Write(i));

            yield return Create<float>((builder, _, i) => builder.Write(i));
            yield return Create<double>((builder, _, i) => builder.Write(i));
            yield return Create<decimal>((builder, _, i) => builder.Write(i));
            yield return Create<BigInteger>((builder, _, i) => builder.Write(i));

            yield return Create<char>((builder, _, c) =>
            {
                builder.Write('\'');
                builder.Write(c);
                builder.Write('\'');
            });
            yield return Create<string>((builder, _, s) =>
            {
                if (s is null)
                {
                    builder.Write("null");
                }
                else
                {
                    WriteStringLiteral(builder, s);
                }
            });

            yield return Create<DateTime>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<DateTimeOffset>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<TimeSpan>((builder, _, t) => WriteStringLiteral(builder, t));
#if NET
            yield return Create<DateOnly>((builder, _, d) => WriteStringLiteral(builder, d));
            yield return Create<TimeOnly>((builder, _, d) => WriteStringLiteral(builder, d));
#endif
            yield return Create<Guid>((builder, _, g) => WriteStringLiteral(builder, g));

            static KeyValuePair<Type, object> Create<T>(PrettyPrinter<T> printer)
                => new(typeof(T), printer);
        }
    }

    private sealed class DelayedPrettyPrinterFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<PrettyPrinter<T>>(self => (sb, i, t) => self.Result(sb, i, t));
    }
}
