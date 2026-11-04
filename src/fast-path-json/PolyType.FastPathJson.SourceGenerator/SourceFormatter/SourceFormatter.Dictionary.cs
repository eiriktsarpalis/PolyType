using PolyType.FastPathJson.SourceGenerator.Model;
using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void WriteDictionaryConverter(SourceWriter writer, DictionaryConverterModel model)
    {
        string enumerable = model.RequiresInterfaceEnumeration
            ? $"(global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<{model.Key.Name}, {model.Value.Name}>>)value"
            : "value";
        writer.WriteLine($$"""
            static void {{Contract}}<{{model.Type.Name}}>.Write({{Writer}} writer, {{model.Type.NullableName}} value)
            {
            """);
        writer.Indentation++;
        WriteNullGuard(writer, model.Type, reading: false);
        writer.WriteLine($$"""
            writer.WriteStartObject();

            foreach (var entry in {{enumerable}})
            {
            """);
        writer.Indentation++;
        if (FormatFusedDictionaryEntry(model.Key, model.Value) is { } fusedEntry)
        {
            writer.WriteLine($"{fusedEntry};");
        }
        else
        {
            writer.WriteLine($"{Shared}.WriteKey<{model.Key.Name}, {model.Key.WitnessType}>(writer, entry.Key);");
            writer.WriteLine($"{FormatWrite(model.Value, "entry.Value")};");
        }

        writer.Indentation--;
        writer.WriteLine("""
            }

            writer.WriteEndObject();
            """);
        writer.Indentation--;
        writer.WriteLine('}');
        writer.WriteLine();

        string initialization = model.Insert is null
            ? $"var items = new global::System.Collections.Generic.Dictionary<{model.Key.Name}, {model.Value.Name}>();"
            : $"var result = {model.Construction};";
        string returnStatement = model.Insert is null ? $"return {model.Construction};" : "return result;";
        writer.WriteLine($$"""
            static {{model.Type.NullableName}} {{Contract}}<{{model.Type.Name}}>.Read(ref {{Reader}} reader)
            {
            """);
        writer.Indentation++;
        WriteNullGuard(writer, model.Type, reading: true);
        writer.WriteLine($$"""
            {{Shared}}.EnsureToken(ref reader, {{Token}}StartObject);
            {{initialization}}

            while (true)
            {
                {{Shared}}.EnsureRead(ref reader);
                if (reader.TokenType is {{Token}}EndObject)
                {
                    break;
                }

                if (reader.TokenType is {{Token}}Comment)
                {
                    {{Shared}}.EnsureToken(ref reader, {{Token}}PropertyName);
                }

                if (reader.ValueTextEquals("$type"u8))
                {
                    {{Shared}}.EnsureRead(ref reader);
                    reader.Skip();
                    continue;
                }

                var key = {{Shared}}.ReadKey<{{model.Key.Name}}, {{model.Key.WitnessType}}>(ref reader);
                {{Shared}}.EnsureRead(ref reader);
                var element = {{FormatRead(model.Value)}}!;
                {{model.Insert ?? "items[key] = element;"}}
            }

            {{returnStatement}}
            """);
        writer.Indentation--;
        writer.WriteLine('}');
    }
}
