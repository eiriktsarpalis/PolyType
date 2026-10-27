using PolyType.FastPathJson.SourceGenerator.Model;
using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void WriteBuiltInCollectionConverter(SourceWriter writer, BuiltInCollectionConverterModel model)
    {
        TypeRef type = model.Type;
        bool isDictionary = model.Key is not null;
        writer.WriteLine($"static void {Contract}<{type.Name}>.Write({Writer} writer, {type.NullableName} value)");
        writer.WriteLine('{');
        writer.Indentation++;
        WriteNullGuard(writer, type, reading: false);
        writer.WriteLine(isDictionary ? "writer.WriteStartObject();" : "writer.WriteStartArray();");
        writer.WriteLine();
        writer.WriteLine($"foreach (var {(isDictionary ? "entry" : "element")} in value)");
        writer.WriteLine('{');
        writer.Indentation++;
        string? fusedEntry = model.Key is { } key ? FormatFusedDictionaryEntry(key, model.Element) : null;
        if (fusedEntry is not null)
        {
            writer.WriteLine($"{fusedEntry};");
        }
        else
        {
            if (model.Key is { } dictionaryKey)
            {
                writer.WriteLine($"{FormatBuiltInKeyWrite(dictionaryKey, "entry.Key")};");
            }

            writer.WriteLine($"{FormatWrite(model.Element, isDictionary ? "entry.Value" : "element")};");
        }
        writer.Indentation--;
        writer.WriteLine('}');
        writer.WriteLine();
        writer.WriteLine(isDictionary ? "writer.WriteEndObject();" : "writer.WriteEndArray();");
        writer.Indentation--;
        writer.WriteLine('}');
        writer.WriteLine();
        writer.WriteLine($"static {type.NullableName} {Contract}<{type.Name}>.Read(ref {Reader} reader)");
        writer.WriteLine('{');
        writer.Indentation++;
        writer.WriteLine($"return {FormatRead(type with { Witness = null })};");
        writer.Indentation--;
        writer.WriteLine('}');
    }
}
