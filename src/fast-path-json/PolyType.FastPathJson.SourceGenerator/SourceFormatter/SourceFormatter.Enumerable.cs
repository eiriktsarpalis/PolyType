using PolyType.FastPathJson.SourceGenerator.Model;
using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void WriteEnumerableConverter(SourceWriter writer, EnumerableConverterModel model)
    {
        string enumerable = model.RequiresInterfaceEnumeration
            ? $"(global::System.Collections.Generic.IEnumerable<{model.Element.Name}>)value"
            : "value";
        writer.WriteLine($$"""
            static void {{Contract}}<{{model.Type.Name}}>.Write({{Writer}} writer, {{model.Type.NullableName}} value)
            {
            """);
        writer.Indentation++;
        WriteNullGuard(writer, model.Type, reading: false);
        writer.WriteLine($$"""
            writer.WriteStartArray();

            foreach (var element in {{enumerable}})
            {
                {{FormatWrite(model.Element, "element")}};
            }

            writer.WriteEndArray();
            """);
        writer.Indentation--;
        writer.WriteLine('}');
        writer.WriteLine();

        string initialization = model.Append is null
            ? $"var items = new global::System.Collections.Generic.List<{model.Element.Name}>();"
            : $"var result = {model.Construction};";
        string returnStatement = model.Append is null ? $"return {model.Construction};" : "return result;";
        writer.WriteLine($$"""
            static {{model.Type.NullableName}} {{Contract}}<{{model.Type.Name}}>.Read(ref {{Reader}} reader)
            {
            """);
        writer.Indentation++;
        WriteNullGuard(writer, model.Type, reading: true);
        writer.WriteLine($$"""
            {{Shared}}.EnsureToken(ref reader, {{Token}}StartArray);
            {{initialization}}

            while (true)
            {
                {{Shared}}.EnsureRead(ref reader);
                if (reader.TokenType is {{Token}}EndArray)
                {
                    break;
                }

                var element = {{FormatRead(model.Element)}}!;
                {{model.Append ?? "items.Add(element);"}}
            }

            {{returnStatement}}
            """);
        writer.Indentation--;
        writer.WriteLine('}');
    }
}
