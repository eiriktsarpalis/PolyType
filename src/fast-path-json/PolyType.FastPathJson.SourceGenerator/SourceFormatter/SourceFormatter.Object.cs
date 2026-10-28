using PolyType.FastPathJson.SourceGenerator.Model;
using PolyType.Roslyn;
using System.Text;

namespace PolyType.FastPathJson.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void WriteObjectConverter(SourceWriter writer, ObjectConverterModel model, bool needsPropertyWriter)
    {
        TypeRef type = model.Type;
        string identifier = GetConverterIdentifier(type);
        string readObject = "ReadObject_" + identifier;
        string writeProperties = "WriteProperties_" + identifier;
        string propertyNames = "__JsonPropertyNames_" + identifier;
        int[] requiredSlots = Enumerable.Range(0, model.Slots.Length).Where(i => model.Slots[i].Required).ToArray();
        bool useBitArray = Enumerable.Range(0, model.Slots.Length)
            .Count(i => model.Slots[i].Required || model.Assignments.Any(a => a.Slot == i)) > 64;
        WritePropertyNamesCache(writer, model, propertyNames);
        writer.WriteLine($"static void {Contract}<{type.Name}>.Write({Writer} writer, {type.NullableName} value)");
        writer.WriteLine('{');
        writer.Indentation++;
        WriteNullGuard(writer, type, reading: false);
        if (model.UnionCases.Length > 0 || model.Properties.Length > 0 && !needsPropertyWriter)
        {
            writer.WriteLine($"bool useCachedPropertyNames = {Shared}.UsesDefaultEncoder(writer);");
            writer.WriteLine();
        }

        foreach (UnionCase unionCase in model.UnionCases)
        {
            string condition = unionCase.IsBaseType ? $"value.GetType() == typeof({type.Name})" : $"value is {unionCase.Type.Name}";
            writer.WriteLine($"if ({condition})");
            writer.WriteLine('{');
            writer.Indentation++;
            writer.WriteLine("writer.WriteStartObject();");
            writer.WriteLine($$"""
                if (useCachedPropertyNames)
                {
                    writer.WriteString({{propertyNames}}.TypeDiscriminator, {{Literal(unionCase.Name)}});
                }
                else
                {
                    writer.WriteString("$type", {{Literal(unionCase.Name)}});
                }

                """);
            writer.WriteLine($"{unionCase.Type.WitnessType}.WriteProperties_{GetConverterIdentifier(unionCase.Type)}(writer, ({unionCase.Type.Name})value);");
            writer.WriteLine("writer.WriteEndObject();");
            writer.WriteLine();
            writer.WriteLine("return;");
            writer.Indentation--;
            writer.WriteLine('}');
            writer.WriteLine();
        }

        writer.WriteLine("writer.WriteStartObject();");
        if (needsPropertyWriter)
        {
            writer.WriteLine($"{writeProperties}(writer, value);");
        }
        else
        {
            WriteObjectProperties(writer, model, propertyNames);
        }

        writer.WriteLine("writer.WriteEndObject();");
        writer.Indentation--;
        writer.WriteLine('}');

        if (needsPropertyWriter)
        {
            writer.WriteLine();
            writer.WriteLine($"internal static void {writeProperties}({Writer} writer, {type.Name} value)");
            writer.WriteLine('{');
            writer.Indentation++;
            if (model.Properties.Length > 0)
            {
                writer.WriteLine($"bool useCachedPropertyNames = {Shared}.UsesDefaultEncoder(writer);");
                writer.WriteLine();
            }

            WriteObjectProperties(writer, model, propertyNames);
            writer.Indentation--;
            writer.WriteLine('}');
        }

        writer.WriteLine();
        writer.WriteLine($"static {type.NullableName} {Contract}<{type.Name}>.Read(ref {Reader} reader)");
        writer.WriteLine('{');
        writer.Indentation++;
        WriteNullGuard(writer, type, reading: true);
        writer.WriteLine($"{Shared}.EnsureToken(ref reader, {Token}StartObject);");
        if (model.UnionCases.Length > 0)
        {
            writer.WriteLine();
            writer.WriteLine($$"""
                var probe = reader;
                while (true)
                {
                    {{Shared}}.EnsureRead(ref probe);
                    if (probe.TokenType is {{Token}}EndObject)
                    {
                        break;
                    }

                    if (probe.TokenType is {{Token}}Comment)
                    {
                        {{Shared}}.EnsureToken(ref probe, {{Token}}PropertyName);
                    }

                    bool isDiscriminator = probe.ValueTextEquals("$type"u8);
                    {{Shared}}.EnsureRead(ref probe);

                    if (isDiscriminator)
                    {
                        {{Shared}}.EnsureToken(ref probe, {{Token}}String);
                        return probe.GetString() switch
                        {
                """);
            writer.Indentation += 3;
            foreach (UnionCase unionCase in model.UnionCases)
            {
                string read = unionCase.IsBaseType ? $"{readObject}(ref reader)" : FormatRead(unionCase.Type);
                writer.WriteLine($"{Literal(unionCase.Name)} => {read},");
            }

            writer.WriteLine("_ => ThrowUnrecognizedTypeDiscriminator(),");
            writer.Indentation -= 3;
            writer.WriteLine("""
                        };
                    }

                    probe.Skip();
                }
                """);
            writer.WriteLine();

            writer.WriteLine($"return {readObject}(ref reader);");
            writer.WriteLine();
            writer.WriteLine($$"""
                [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                static {{type.Name}} ThrowUnrecognizedTypeDiscriminator() =>
                    throw new {{JsonException}}("Unrecognized type discriminator.");
                """);
            writer.Indentation--;
            writer.WriteLine('}');

            writer.WriteLine();
            writer.WriteLine($"private static {type.Name} {readObject}(ref {Reader} reader)");
            writer.WriteLine('{');
            writer.Indentation++;
        }
        else
        {
            writer.WriteLine();
        }

        if (model.Construction is null)
        {
            writer.WriteLine($$"""
                return ThrowMissingTypeDiscriminator();

                [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                static {{type.Name}} ThrowMissingTypeDiscriminator() =>
                    throw new global::System.NotSupportedException({{Literal($"Deserialization for type {type.Name} requires a type discriminator.")}});
                """);
        }
        else
        {
            Dictionary<int, int> trackedSlots = [];
            foreach (int slot in requiredSlots)
            {
                trackedSlots.Add(slot, trackedSlots.Count);
            }

            for (int i = 0; i < model.Slots.Length; i++)
            {
                ReadSlot slot = model.Slots[i];
                writer.WriteLine($"{slot.Type.NullableName} arg{i} = {slot.DefaultValue};");
                if (!slot.Required && model.Assignments.Any(a => a.Slot == i))
                {
                    trackedSlots.Add(i, trackedSlots.Count);
                }
            }

            if (trackedSlots.Count > 0)
            {
                writer.WriteLine(useBitArray
                    ? $"var seen = new global::System.Collections.BitArray({trackedSlots.Count});"
                    : "ulong seen = 0;");
            }

            if (model.Slots.Length > 0)
            {
                writer.WriteLine();
            }

            // Every UTF-16 code unit can be represented by a six-byte \uXXXX escape.
            bool needsRentedPropertyNameBuffer = model.Slots.Any(slot => slot.Name.Length > 256 / 6);
            writer.WriteLine("global::System.Span<byte> propertyNameBuffer = stackalloc byte[256];");
            if (needsRentedPropertyNameBuffer)
            {
                writer.WriteLine("""
                    byte[]? rentedPropertyNameBuffer = null;
                    try
                    {
                    """);
                writer.Indentation++;
            }

            writer.WriteLine($$"""
                while (true)
                {
                    {{Shared}}.EnsureRead(ref reader);
                    if (reader.TokenType is {{Token}}EndObject)
                    {
                        break;
                    }
                """);
            writer.WriteLine();
            writer.WriteLine($$"""
                    if (reader.TokenType is {{Token}}Comment)
                    {
                        {{Shared}}.EnsureToken(ref reader, {{Token}}PropertyName);
                    }
                """);
            writer.WriteLine();
            if (needsRentedPropertyNameBuffer)
            {
                writer.WriteLine($$"""
                        global::System.ReadOnlySpan<byte> name = {{Shared}}.GetUtf8PropertyName(in reader, propertyNameBuffer, ref rentedPropertyNameBuffer);
                    """);
            }
            else
            {
                writer.WriteLine($$"""
                        if (!{{Shared}}.TryGetUtf8PropertyName(in reader, propertyNameBuffer, out global::System.ReadOnlySpan<byte> name))
                        {
                            {{Shared}}.EnsureRead(ref reader);
                            reader.Skip();
                            continue;
                        }
                    """);
            }

            writer.WriteLine($"    {Shared}.EnsureRead(ref reader);");
            writer.WriteLine();
            writer.WriteLine("""
                    switch (name)
                    {
                """);
            writer.Indentation += 2;
            string?[] namePatterns = GetPropertyNamePatterns(model);
            for (int i = 0; i < model.Slots.Length; i++)
            {
                if (namePatterns[i] is not { } pattern)
                {
                    continue;
                }

                if (i > 0)
                {
                    writer.WriteLine();
                }

                ReadSlot slot = model.Slots[i];
                writer.WriteLine($"case {pattern} when global::System.MemoryExtensions.SequenceEqual(name, {Literal(slot.Name)}u8):");
                writer.Indentation++;
                writer.WriteLine($"arg{i} = {FormatRead(slot.Type)};");
                if (slot.DisallowNull)
                {
                    writer.WriteLine($$"""
                        if (arg{{i}} is null)
                        {
                            {{Shared}}.ThrowNullPropertyOnRead({{Literal(slot.Name)}});
                        }
                        """);
                    writer.WriteLine();
                }

                if (trackedSlots.TryGetValue(i, out int bit))
                {
                    writer.WriteLine(useBitArray ? $"seen[{bit}] = true;" : $"seen |= 1UL << {bit};");
                }

                writer.WriteLine("break;");
                writer.Indentation--;
            }

            if (model.Slots.Length > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine("default:");
            writer.Indentation++;
            writer.WriteLine("reader.Skip();");
            writer.WriteLine("break;");
            writer.Indentation--;
            writer.Indentation -= 2;
            writer.WriteLine("""
                    }
                }
                """);
            if (needsRentedPropertyNameBuffer)
            {
                writer.Indentation--;
                writer.WriteLine("""
                    }
                    finally
                    {
                        if (rentedPropertyNameBuffer is not null)
                        {
                            global::System.Buffers.ArrayPool<byte>.Shared.Return(rentedPropertyNameBuffer);
                        }
                    }
                    """);
            }
            if (requiredSlots.Length > 0)
            {
                writer.WriteLine();
                if (useBitArray)
                {
                    string missing = requiredSlots.Length == trackedSlots.Count
                        ? "!seen.HasAllSet()"
                        : $"!{Shared}.HasAllSet(seen, {requiredSlots.Length})";
                    writer.WriteLine($$"""
                        if ({{missing}})
                        {
                            ThrowMissingRequiredParameters(seen);
                        }
                        """);
                }
                else
                {
                    ulong mask = requiredSlots.Length == 64 ? ulong.MaxValue : (1UL << requiredSlots.Length) - 1;
                    string literal = "0x" + mask.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + "UL";
                    writer.WriteLine($$"""
                        if ((seen & {{literal}}) != {{literal}})
                        {
                            ThrowMissingRequiredParameters({{literal}} & ~seen);
                        }
                        """);
                }
            }

            writer.WriteLine();
            writer.WriteLine($"var result = {model.Construction};");
            foreach (MemberAssignment assignment in model.Assignments)
            {
                writer.WriteLine();
                writer.WriteLine($$"""
                    if ({{FormatPresenceCheck(assignment.Slot)}})
                    {
                        {{assignment.Access}} = arg{{assignment.Slot}}!;
                    }
                    """);
            }

            writer.WriteLine();
            writer.WriteLine("return result;");

            string FormatPresenceCheck(int slot)
            {
                int bit = trackedSlots[slot];
                return useBitArray
                    ? $"seen[{bit}]"
                    : $"(seen & (1UL << {bit})) != 0";
            }
        }

        if (requiredSlots.Length > 0)
        {
            writer.WriteLine();
            WriteMissingRequiredParameterHelper(writer, model, requiredSlots, useBitArray);
        }

        writer.Indentation--;
        writer.WriteLine('}');
    }

    private static string?[] GetPropertyNamePatterns(ObjectConverterModel model)
    {
        string?[] patterns = new string?[model.Slots.Length];
        // Names with unpaired surrogates cannot match valid JSON text or appear in UTF-8 literals.
        var names = model.Slots.Select((slot, index) => (Bytes: Encoding.UTF8.GetBytes(slot.Name), Index: index))
            .Where(name => Encoding.UTF8.GetString(name.Bytes) == model.Slots[name.Index].Name);
        foreach (var group in names.GroupBy(name => name.Bytes.Length))
        {
            int length = group.Key;
            if (length == 0 || group.Count() == 1)
            {
                foreach (var name in group)
                {
                    patterns[name.Index] = $"{{ Length: {length} }}";
                }

                continue;
            }

            // Let Roslyn branch on length and a discriminating byte; compare the rest with vectorized UTF-8 equality.
            int position = Enumerable.Range(0, length)
                .OrderByDescending(i => group.Select(name => name.Bytes[i]).Distinct().Count())
                .First();
            foreach (var name in group)
            {
                string[] elements = Enumerable.Repeat("_", length).ToArray();
                elements[position] = $"(byte){Literal((char)name.Bytes[position])}";
                patterns[name.Index] = "[" + string.Join(", ", elements) + "]";
            }
        }

        return patterns;
    }

    private static void WriteMissingRequiredParameterHelper(SourceWriter writer, ObjectConverterModel model, int[] requiredSlots, bool useBitArray)
    {
        string parameter = useBitArray ? "global::System.Collections.BitArray seen" : "ulong missing";
        string loop = useBitArray
            ? $"for (int index = 0; index < {requiredSlots.Length}; index++)"
            : "while (missing != 0)";
        writer.WriteLine($$"""
            [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            static void ThrowMissingRequiredParameters({{parameter}})
            {
                var names = new global::System.Collections.Generic.List<string>();

                {{loop}}
                {
            """);
        writer.Indentation += 2;
        if (useBitArray)
        {
            writer.WriteLine("""
                if (seen[index])
                {
                    continue;
                }

                """);
        }
        else
        {
            writer.WriteLine("int index = global::System.Numerics.BitOperations.TrailingZeroCount(missing);");
        }

        writer.WriteLine("string name = index switch");
        writer.WriteLine('{');
        writer.Indentation++;
        for (int i = 0; i < requiredSlots.Length; i++)
        {
            writer.WriteLine($"{i} => {Literal(model.Slots[requiredSlots[i]].Name)},");
        }

        writer.WriteLine("""_ => throw new global::System.InvalidOperationException("Invalid required constructor parameter index."),""");
        writer.Indentation--;
        writer.WriteLine("};");
        writer.WriteLine();
        writer.WriteLine("names.Add(name);");
        if (!useBitArray)
        {
            writer.WriteLine("missing &= missing - 1;");
        }

        writer.Indentation -= 2;
        writer.WriteLine($$"""
                }

                {{Shared}}.ThrowMissingRequiredParameters(names);
            }
            """);
    }

    private static void WritePropertyNamesCache(SourceWriter writer, ObjectConverterModel model, string name)
    {
        if (model.Properties.Length == 0 && model.UnionCases.Length == 0)
        {
            return;
        }

        const string EncodedText = "global::System.Text.Json.JsonEncodedText";
        writer.WriteLine($"private static class {name}");
        writer.WriteLine('{');
        writer.Indentation++;
        for (int i = 0; i < model.Properties.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($"internal static readonly {EncodedText} Property{i} = {EncodedText}.Encode({Literal(model.Properties[i].Name)});");
        }

        if (model.UnionCases.Length > 0)
        {
            if (model.Properties.Length > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($"internal static readonly {EncodedText} TypeDiscriminator = {EncodedText}.Encode(\"$type\");");
        }

        writer.Indentation--;
        writer.WriteLine('}');
        writer.WriteLine();
    }

    private static void WriteObjectProperties(SourceWriter writer, ObjectConverterModel model, string propertyNames)
    {
        for (int i = 0; i < model.Properties.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            WriteProperty property = model.Properties[i];
            writer.WriteLine($"var property{i} = {property.Access};");
            if (property.DisallowNull)
            {
                writer.WriteLine($$"""
                    if (property{{i}} is null)
                    {
                        {{Shared}}.ThrowNullPropertyOnWrite({{Literal(property.Name)}});
                    }
                    """);
                writer.WriteLine();
            }

            string? fusedWrite = FormatFusedPropertyWrite(property.Type, $"{propertyNames}.Property{i}", $"property{i}!");
            string writeValue = FormatWrite(property.Type, $"property{i}!");
            if (fusedWrite is not null)
            {
                writer.WriteLine($$"""
                    if (useCachedPropertyNames)
                    {
                        {{fusedWrite}};
                    }
                    else
                    {
                        writer.WritePropertyName({{Literal(property.Name)}});
                        {{writeValue}};
                    }
                    """);
                continue;
            }

            writer.WriteLine($$"""
                if (useCachedPropertyNames)
                {
                    writer.WritePropertyName({{propertyNames}}.Property{{i}});
                }
                else
                {
                    writer.WritePropertyName({{Literal(property.Name)}});
                }
                """);
            writer.WriteLine($"{writeValue};");
        }
    }
}
