using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string root = Directory.GetCurrentDirectory();
string script = Path.Combine(root, "eng", "plot-json-benchmarks.cs");
JsonObject original = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "images", "performance", "results.json")))!.AsObject();
string scratch = Path.Combine(Path.GetTempPath(), $"polytype-chart-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(scratch);
try
{
    string input = Path.Combine(scratch, "input.json");
    string output = Path.Combine(scratch, "output");
    Run(original, expectSuccess: true);
    JsonNode regenerated = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "results.json")))!;
    Check(JsonNode.DeepEquals(original, regenerated), "Regeneration changed measurement data.");
    Check(Directory.GetFiles(output, "*.svg").Length == 8, "Expected eight charts.");
    foreach (string file in Directory.GetFiles(output, "*.svg"))
    {
        XElement chart = XElement.Load(file);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Check((string?)chart.Attribute("role") == "img", "Missing accessible image role.");
        XElement[] bars = chart.Element(ns + "g")!.Elements(ns + "rect").ToArray();
        int scenarios = file.Contains("collections", StringComparison.Ordinal) ? 3 : 2;
        int methods = Path.GetFileName(file).StartsWith("serialize", StringComparison.Ordinal) ? 5 : 4;
        Check(bars.Length == scenarios * methods, "Incorrect bar count.");
        Check(bars.All(b => double.Parse(b.Attribute("width")!.Value, CultureInfo.InvariantCulture) > 0), "Invalid bar width.");
        string committed = Path.Combine(root, "docs", "images", "performance", Path.GetFileName(file));
        Check(File.ReadAllText(file) == File.ReadAllText(committed), $"Chart differs: {file}");
    }
    Console.WriteLine("PASS: complete dataset, preserved measurements, and eight reproducible SVG charts");

    JsonObject synthetic = original.DeepClone().AsObject();
    foreach (JsonNode? node in synthetic["benchmarks"]!.AsArray())
    {
        node!["statistics"] = new JsonObject
        {
            ["N"] = 3,
            ["OriginalValues"] = new JsonArray(99, 100, 101),
            ["Mean"] = 100,
            ["StandardError"] = 1,
            ["StandardDeviation"] = 1,
            ["ConfidenceInterval"] = new JsonObject { ["Lower"] = 90, ["Upper"] = 110 },
        };
        node["allocated_bytes"] = 0;
    }

    Run(synthetic, expectSuccess: true);
    foreach (string file in Directory.GetFiles(output, "*.svg"))
    {
        Check(File.ReadAllText(file).Contains("1.00x | 100.00 ns | 0 B", StringComparison.Ordinal), "Missing synthetic measurement label.");
    }
    Console.WriteLine("PASS: synthetic measurements produce expected labels");

    foreach (string kind in new[] { "missing", "duplicate", "unknown", "null-statistics", "few-iterations", "zero-mean", "negative-error", "infinite-mean", "infinite-error", "negative-allocation", "missing-host", "missing-date" })
    {
        JsonObject data = original.DeepClone().AsObject();
        JsonArray benchmarks = data["benchmarks"]!.AsArray();
        switch (kind)
        {
            case "missing": benchmarks.RemoveAt(benchmarks.Count - 1); break;
            case "duplicate": benchmarks.Add(benchmarks[0]!.DeepClone()); break;
            case "unknown": benchmarks[0]!["scenario"] = "Unknown"; break;
            case "null-statistics": benchmarks[0]!["statistics"] = null; break;
            case "few-iterations": benchmarks[0]!["statistics"]!["N"] = 2; break;
            case "zero-mean": benchmarks[0]!["statistics"]!["Mean"] = 0; break;
            case "negative-error": benchmarks[0]!["statistics"]!["StandardError"] = -1; break;
            case "infinite-mean": benchmarks[0]!["statistics"]!["Mean"] = JsonNode.Parse("1e999"); break;
            case "infinite-error": benchmarks[0]!["statistics"]!["StandardError"] = JsonNode.Parse("1e999"); break;
            case "negative-allocation": benchmarks[0]!["allocated_bytes"] = -1; break;
            case "missing-host": data["host"] = null; break;
            case "missing-date": data["measured_on"] = ""; break;
        }

        Run(data, expectSuccess: false);
        Console.WriteLine($"PASS: rejects {kind}");
    }

    string reports = Path.Combine(scratch, "reports");
    Directory.CreateDirectory(reports);
    JsonArray exported = [];
    foreach (JsonNode? node in original["benchmarks"]!.AsArray())
    {
        JsonNode record = node!;
        JsonObject benchmark = new()
        {
            ["Type"] = $"Json{record["operation"]!.GetValue<string>()}Benchmark<{record["scenario"]!.GetValue<string>()}>",
            ["Method"] = $"{record["operation"]!.GetValue<string>()}_{record["method"]!.GetValue<string>()}",
            ["DisplayInfo"] = record["job"]!.DeepClone(),
            ["Statistics"] = record["statistics"]!.DeepClone(),
            ["Memory"] = new JsonObject { ["BytesAllocatedPerOperation"] = record["allocated_bytes"]!.DeepClone() },
        };
        exported.Add((JsonNode)benchmark);
    }

    JsonObject report = new() { ["HostEnvironmentInfo"] = original["host"]!.DeepClone(), ["Benchmarks"] = exported };
    File.WriteAllText(Path.Combine(reports, "test-report-full-compressed.json"), report.ToJsonString());
    Execute(reports, expectSuccess: true);
    Check(JsonNode.DeepEquals(original, JsonNode.Parse(File.ReadAllText(Path.Combine(output, "results.json")))), "BDN import changed measurements or environment.");
    report["HostEnvironmentInfo"] = new JsonObject { ["ProcessorName"] = "different" };
    File.WriteAllText(Path.Combine(reports, "other-report-full-compressed.json"), report.ToJsonString());
    Execute(reports, expectSuccess: false);
    Console.WriteLine("PASS: BDN import preserves samples and rejects inconsistent environments");

    void Run(JsonObject data, bool expectSuccess)
    {
        File.WriteAllText(input, data.ToJsonString());
        Execute(input, expectSuccess);
    }

    void Execute(string source, bool expectSuccess)
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "run", "--file", script, "--", source, "--output", output, "--date", original["measured_on"]!.GetValue<string>() })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start chart app.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string diagnostics = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        Check((process.ExitCode == 0) == expectSuccess, $"Unexpected exit code {process.ExitCode}: {diagnostics}");
        if (!expectSuccess)
        {
            Check(diagnostics.Contains("InvalidDataException", StringComparison.Ordinal), $"Expected validation error, not another failure: {diagnostics}");
        }
    }
}
finally
{
    Directory.Delete(scratch, recursive: true);
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
