using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine("Usage: dotnet run --file eng/plot-json-benchmarks.cs -- <BDN results directory|results.json> [--output directory] [--date yyyy-MM-dd]");
    return args.Length == 0 ? 1 : 0;
}

string input = args[0];
string output = "docs/images/performance";
string measuredOn = DateTime.Today.ToString("yyyy-MM-dd");
for (int i = 1; i < args.Length; i += 2)
{
    if (i + 1 >= args.Length)
    {
        throw new ArgumentException($"Missing value for {args[i]}.");
    }

    switch (args[i])
    {
        case "--output": output = args[i + 1]; break;
        case "--date": measuredOn = args[i + 1]; break;
        default: throw new ArgumentException($"Unknown option {args[i]}.");
    }
}

JsonObject data = Directory.Exists(input) ? ReadReports(input, measuredOn) : ReadObject(input);
Dictionary<Key, JsonObject> records = Validate(data);
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "results.json"), data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
foreach (Group group in Groups())
{
    foreach (string operation in new[] { "Serialize", "Deserialize" })
    {
        File.WriteAllText(Path.Combine(output, $"{operation.ToLowerInvariant()}-{group.Name}.svg"), Chart(records, operation, group));
    }
}

PrintGaps(records);
return 0;

static Group[] Groups() =>
[
    new("primitives", [new("Int32", "int: 42"), new("String", "string: Hello, PolyType!")]),
    new("collections", [new("Int32[]", "int[]: 256 elements"), new("List<Int32>", "List<int>: 256 elements"), new("Dictionary<String, Int32>", "Dictionary<string, int>: 64 entries")]),
    new("pocos", [new("MyPoco", "Small POCO: 4 properties, parameterized constructor"), new("LargePoco", "Large POCO: 24 properties, nested objects and collections")]),
    new("megabyte", [new("CitmCatalog[]", "CITM event catalogs: 5 catalogs, 2.39 MiB JSON"), new("LargePoco[]", "Large POCO array: 4,096 records, 2.64 MiB JSON")]),
];

static Method[] Methods(string operation) =>
    AllMethods().Where(m => operation == "Serialize" || m.Id != "StjSourceGen_FastPath").ToArray();

static Method[] AllMethods() =>
[
    new("StjReflection", "STJ reflection", "#64748b"),
    new("StjSourceGen", "STJ source-gen metadata", "#2563eb"),
    new("StjSourceGen_FastPath", "STJ source-gen fast path", "#7c3aed"),
    new("PolyTypeReflection", "PolyType reflection", "#b45309"),
    new("PolyTypeSourceGen", "PolyType source-gen", "#047857"),
];

static JsonObject ReadObject(string path) =>
    JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidDataException($"Expected a JSON object in {path}.");

static string Text(JsonNode node, string property) =>
    node[property]?.GetValue<string>() ?? throw new InvalidDataException($"Missing {property}.");

static double Number(JsonNode node, string property) =>
    node[property]?.GetValue<double>() ?? throw new InvalidDataException($"Missing {property}.");

static JsonObject ReadReports(string directory, string measuredOn)
{
    JsonNode? host = null;
    JsonArray benchmarks = [];
    foreach (string path in Directory.EnumerateFiles(directory, "*-report-full-compressed.json").Order(StringComparer.Ordinal))
    {
        JsonObject report = ReadObject(path);
        JsonNode environment = report["HostEnvironmentInfo"] ?? throw new InvalidDataException($"Missing environment in {path}.");
        if (host is not null && !JsonNode.DeepEquals(host, environment))
        {
            throw new InvalidDataException($"Inconsistent benchmark environments: {path}");
        }

        host = environment.DeepClone();
        foreach (JsonNode? entry in report["Benchmarks"]?.AsArray() ?? throw new InvalidDataException($"Missing benchmarks in {path}."))
        {
            JsonNode benchmark = entry ?? throw new InvalidDataException($"Null benchmark in {path}.");
            string method = Text(benchmark, "Method");
            int separator = method.IndexOf('_');
            string type = Text(benchmark, "Type");
            int opening = type.IndexOf('<');
            if (separator < 0 || opening < 0 || !type.EndsWith('>'))
            {
                throw new InvalidDataException($"Invalid benchmark name in {path}.");
            }

            JsonObject? statistics = null;
            if (benchmark["Statistics"] is JsonObject stats)
            {
                statistics = [];
                foreach (string name in new[] { "N", "OriginalValues", "Mean", "StandardError", "StandardDeviation", "ConfidenceInterval" })
                {
                    statistics[name] = stats[name]?.DeepClone();
                }
            }

            JsonObject record = new()
            {
                ["operation"] = method[..separator],
                ["scenario"] = type[(opening + 1)..^1],
                ["method"] = method[(separator + 1)..],
                ["job"] = Text(benchmark, "DisplayInfo"),
                ["statistics"] = statistics,
                ["allocated_bytes"] = benchmark["Memory"]?["BytesAllocatedPerOperation"]?.DeepClone(),
            };
            benchmarks.Add((JsonNode)record);
        }
    }

    return new JsonObject { ["measured_on"] = measuredOn, ["host"] = host, ["benchmarks"] = benchmarks };
}

static Dictionary<Key, JsonObject> Validate(JsonObject data)
{
    HashSet<Key> expected = [];
    foreach (string operation in new[] { "Serialize", "Deserialize" })
    foreach (Scenario scenario in Groups().SelectMany(g => g.Scenarios))
    foreach (Method method in Methods(operation))
    {
        expected.Add(new(operation, scenario.Id, method.Id));
    }

    Dictionary<Key, JsonObject> records = [];
    foreach (JsonNode? node in data["benchmarks"]?.AsArray() ?? throw new InvalidDataException("Missing benchmarks."))
    {
        JsonObject benchmark = node as JsonObject ?? throw new InvalidDataException("Invalid benchmark.");
        Key key = new(Text(benchmark, "operation"), Text(benchmark, "scenario"), Text(benchmark, "method"));
        if (!records.TryAdd(key, benchmark))
        {
            throw new InvalidDataException($"Duplicate benchmark: {key}");
        }

        JsonObject stats = benchmark["statistics"] as JsonObject ?? throw new InvalidDataException($"Missing measurements: {key}");
        double count = Number(stats, "N");
        double mean = Number(stats, "Mean");
        double error = Number(stats, "StandardError");
        double allocated = Number(benchmark, "allocated_bytes");
        if (!double.IsFinite(count) || count < 3 || !double.IsFinite(mean) || mean <= 0 ||
            !double.IsFinite(error) || error < 0 || !double.IsFinite(allocated) || allocated < 0)
        {
            throw new InvalidDataException($"Invalid measurements: {key}");
        }
    }

    if (!expected.SetEquals(records.Keys))
    {
        throw new InvalidDataException("Unexpected benchmark set: requires all nine scenarios and all serializer configurations.");
    }

    if (data["host"] is not JsonObject { Count: > 0 } || string.IsNullOrWhiteSpace(Text(data, "measured_on")))
    {
        throw new InvalidDataException("Missing benchmark environment or measurement date.");
    }

    return records;
}

static double Mean(JsonObject record) => Number(record["statistics"]!, "Mean");
static string Time(double ns) => ns >= 1_000_000 ? $"{ns / 1_000_000:F2} ms" : ns >= 1_000 ? $"{ns / 1_000:F2} us" : $"{ns:F2} ns";
static string Bytes(double value) => value >= 1_048_576 ? $"{value / 1_048_576:F2} MiB" : value >= 1_024 ? $"{value / 1_024:F2} KiB" : $"{value:G} B";
static string Escape(string value) => WebUtility.HtmlEncode(value).Replace("&#39;", "&#x27;");

static string Chart(Dictionary<Key, JsonObject> records, string operation, Group group)
{
    Method[] methods = Methods(operation);
    int groupHeight = 60 + 32 * methods.Length;
    int height = 116 + groupHeight * group.Scenarios.Length;
    const int Left = 258, PlotWidth = 420;
    double upper = group.Scenarios.Max(s => methods.Max(m =>
    {
        JsonObject record = records[new(operation, s.Id, m.Id)];
        return (Mean(record) + Number(record["statistics"]!, "StandardError")) / Mean(records[new(operation, s.Id, "StjReflection")]);
    }));
    double axisMax = Math.Ceiling(Math.Max(1.25, upper + 0.1) * 4) / 4;
    string title = $"{(operation == "Serialize" ? "Serialization" : "Deserialization")}: {group.Name}";
    StringBuilder svg = new();
    void Line(string value) => svg.Append(value).Append('\n');
    void Label(string x, int y, string value, string attributes = "") =>
        Line($"<text x=\"{x}\" y=\"{y}\" {attributes}>{Escape(value)}</text>");

    Line($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1100\" height=\"{height}\" viewBox=\"0 0 1100 {height}\" role=\"img\" aria-labelledby=\"title desc\">");
    Line($"<title id=\"title\">{Escape(title)}</title>");
    Line("<desc id=\"desc\">Mean time relative to STJ reflection for each payload; lower is better. Whiskers show one standard error. Each bar is labelled with its ratio, mean time, and allocated bytes per operation.</desc>");
    Line("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");
    Line("<g font-family=\"Arial, sans-serif\" font-size=\"14\" fill=\"#0f172a\">");
    Label("20", 30, title, "font-size=\"22\" font-weight=\"bold\"");
    Label("20", 54, "Lower is better | normalized to STJ reflection per payload | whiskers: +/-1 standard error");
    Label("740", 79, "Ratio | Mean | Allocated / operation", "font-size=\"13\"");
    for (int tick = 0; tick <= axisMax * 4; tick++)
    {
        double ratio = tick / 4.0;
        string x = (Left + ratio / axisMax * PlotWidth).ToString("F2");
        Line($"<line x1=\"{x}\" y1=\"90\" x2=\"{x}\" y2=\"{height - 35}\" stroke=\"#e2e8f0\"/>");
        Label(x, height - 15, $"{ratio:G}x", "text-anchor=\"middle\" font-size=\"12\"");
    }

    string baselineX = (Left + PlotWidth / axisMax).ToString("F2");
    Line($"<line x1=\"{baselineX}\" y1=\"90\" x2=\"{baselineX}\" y2=\"{height - 35}\" stroke=\"#475569\" stroke-dasharray=\"4 4\"/>");
    for (int i = 0; i < group.Scenarios.Length; i++)
    {
        Scenario scenario = group.Scenarios[i];
        int y = 110 + i * groupHeight;
        Label("20", y, scenario.Label, "font-weight=\"bold\" font-size=\"16\"");
        double baseline = Mean(records[new(operation, scenario.Id, "StjReflection")]);
        for (int j = 0; j < methods.Length; j++)
        {
            Method method = methods[j];
            JsonObject record = records[new(operation, scenario.Id, method.Id)];
            double ratio = Mean(record) / baseline;
            int barY = y + 14 + j * 32;
            double width = ratio / axisMax * PlotWidth;
            double error = Number(record["statistics"]!, "StandardError") / baseline / axisMax * PlotWidth;
            double endpoint = Left + width;
            Label((Left - 12).ToString(), barY + 16, method.Label, "text-anchor=\"end\"");
            Line($"<rect x=\"{Left}\" y=\"{barY}\" width=\"{width:F2}\" height=\"22\" fill=\"{method.Color}\"/>");
            Line($"<path d=\"M {endpoint - error:F2} {barY + 11} H {endpoint + error:F2} M {endpoint - error:F2} {barY + 6} V {barY + 16} M {endpoint + error:F2} {barY + 6} V {barY + 16}\" stroke=\"#0f172a\" fill=\"none\"/>");
            Label("740", barY + 16, $"{ratio:F2}x | {Time(Mean(record))} | {Bytes(Number(record, "allocated_bytes"))}");
        }
    }

    Line("</g>");
    Line("</svg>");
    return svg.ToString();
}

static void PrintGaps(Dictionary<Key, JsonObject> records)
{
    foreach (string operation in new[] { "Serialize", "Deserialize" })
    foreach (Scenario scenario in Groups().SelectMany(g => g.Scenarios))
    {
        Method fastest = Methods(operation).Where(m => m.Id.StartsWith("Stj", StringComparison.Ordinal))
            .MinBy(m => Mean(records[new(operation, scenario.Id, m.Id)]))!;
        double fastestMean = Mean(records[new(operation, scenario.Id, fastest.Id)]);
        foreach (string method in new[] { "PolyTypeReflection", "PolyTypeSourceGen" })
        {
            string matched = method == "PolyTypeReflection" ? "StjReflection" : "StjSourceGen";
            double mean = Mean(records[new(operation, scenario.Id, method)]);
            double matchedMean = Mean(records[new(operation, scenario.Id, matched)]);
            if (mean > Math.Min(matchedMean, fastestMean))
            {
                Console.WriteLine($"{operation} {scenario.Id} {method}: {(mean / matchedMean - 1) * 100:+0.0;-0.0;+0.0}% time vs {matched}, {(mean / fastestMean - 1) * 100:+0.0;-0.0;+0.0}% vs fastest STJ ({fastest.Id})");
            }
        }
    }
}

readonly record struct Key(string Operation, string Scenario, string Method);
sealed record Scenario(string Id, string Label);
sealed record Group(string Name, Scenario[] Scenarios);
sealed record Method(string Id, string Label, string Color);
