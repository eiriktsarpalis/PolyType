using PolyType.FastPathJson.SourceGenerator.Model;
using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator;

internal static partial class SourceFormatter
{
    private static void WriteArrayConverter(SourceWriter writer, ArrayConverterModel model)
    {
        string identifier = GetConverterIdentifier(model.Type);
        writer.WriteLine($$"""
            static void {{Contract}}<{{model.Type.Name}}>.Write({{Writer}} writer, {{model.Type.NullableName}} value)
            {
            """);
        writer.Indentation++;
        WriteNullGuard(writer, model.Type, reading: false);
        writer.WriteLine("""
            if (value.Length == 0)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
                return;
            }
            """);
        writer.WriteLine();
        for (int d = 0; d < model.Rank; d++)
        {
            writer.WriteLine($$"""
                writer.WriteStartArray();

                for (int i{{d}} = 0; i{{d}} < value.GetLength({{d}}); i{{d}}++)
                {
                """);
            writer.Indentation++;
        }

        string indices = string.Join(", ", Enumerable.Range(0, model.Rank).Select(d => "i" + d));
        writer.WriteLine($"{FormatWrite(model.Element, $"value[{indices}]")};");
        for (int d = 0; d < model.Rank; d++)
        {
            writer.Indentation--;
            writer.WriteLine("""
                }

                writer.WriteEndArray();
                """);
        }

        writer.Indentation--;
        writer.WriteLine('}');
        writer.WriteLine();

        writer.WriteLine($$"""
            static {{model.Type.NullableName}} {{Contract}}<{{model.Type.Name}}>.Read(ref {{Reader}} reader)
            {
            """);
        writer.Indentation++;
        WriteNullGuard(writer, model.Type, reading: true);
        writer.WriteLine($"var items = ReadDimension0_{identifier}(ref reader);");
        for (int d = 0; d < model.Rank; d++)
        {
            string prefix = "items" + string.Concat(Enumerable.Repeat("[0]", d));
            string condition = string.Join(" && ", Enumerable.Range(0, d).Select(i => "n" + i + " > 0"));
            writer.WriteLine($"int n{d} = {(d == 0 ? prefix + ".Count" : condition + " ? " + prefix + ".Count : 0")};");
        }

        writer.WriteLine();
        writer.WriteLine($"var result = {model.Construction};");
        writer.WriteLine();
        for (int d = 0; d < model.Rank; d++)
        {
            string prefix = "items" + string.Concat(Enumerable.Range(0, d).Select(i => "[i" + i + "]"));
            writer.WriteLine($$"""
                if ({{prefix}}.Count != n{{d}})
                {
                    ThrowNonRectangularArray();
                }

                for (int i{{d}} = 0; i{{d}} < n{{d}}; i{{d}}++)
                {
                """);
            writer.Indentation++;
        }

        writer.WriteLine($"result[{indices}] = items{string.Concat(Enumerable.Range(0, model.Rank).Select(d => "[i" + d + "]"))};");
        for (int d = 0; d < model.Rank; d++)
        {
            writer.Indentation--;
            writer.WriteLine('}');
        }

        writer.WriteLine();
        writer.WriteLine("return result;");
        writer.WriteLine();
        writer.WriteLine($$"""
            [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            static void ThrowNonRectangularArray() => throw new {{JsonException}}("Expected a rectangular array.");
            """);
        writer.Indentation--;
        writer.WriteLine('}');

        for (int d = 0; d < model.Rank; d++)
        {
            string listType = model.Element.Name;
            for (int i = d; i < model.Rank; i++)
            {
                listType = "global::System.Collections.Generic.List<" + listType + ">";
            }

            string readElement = d == model.Rank - 1
                ? $"{FormatRead(model.Element)}!"
                : $"ReadDimension{d + 1}_{identifier}(ref reader)";
            writer.WriteLine();
            writer.WriteLine($$"""
                private static {{listType}} ReadDimension{{d}}_{{identifier}}(ref {{Reader}} reader)
                {
                    {{Shared}}.EnsureToken(ref reader, {{Token}}StartArray);
                    {{listType}} result = new();

                    while (true)
                    {
                        {{Shared}}.EnsureRead(ref reader);
                        if (reader.TokenType is {{Token}}EndArray)
                        {
                            return result;
                        }

                        result.Add({{readElement}});
                    }
                }
                """);
        }
    }
}
