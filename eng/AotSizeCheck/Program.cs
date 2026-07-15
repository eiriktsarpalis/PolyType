// AOT size regression check tool. Run via `make test-aot-size` (which
// publishes tests/SizeTrackingApp.AOT and then invokes `check`) or via
// `make update-aot-size-baseline` (which publishes and then invokes
// `update`).
//
// All commands print an agent-parseable AOT-SIZE-RESULT block to stdout
// that contains the RID and measured size_bytes, so a contributor can
// merge per-leg results from a CI run into aot-size-baselines.json by
// reading each leg's log.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

return args switch
{
    [] or ["-h"] or ["--help"] or ["help"] => PrintUsage(Console.Out),
    ["check", .. var rest] => SafeRun(() => RunCheck(rest)),
    ["update", .. var rest] => SafeRun(() => RunUpdate(rest)),
    [var unknown, ..] => Fail($"Unknown command: '{unknown}'"),
};

static int SafeRun(Func<int> action)
{
    try
    {
        return action();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"AotSizeCheck: error: {ex.Message}");
        return 1;
    }
}

static int PrintUsage(TextWriter writer)
{
    writer.WriteLine("Usage: AotSizeCheck <command> [options]");
    writer.WriteLine();
    writer.WriteLine("Commands:");
    writer.WriteLine("  check   Measure the published AOT binary and compare to the committed baseline.");
    writer.WriteLine("  update  Measure the published AOT binary and overwrite the baseline entry for the current RID.");
    writer.WriteLine();
    writer.WriteLine("Options (both commands):");
    writer.WriteLine("  --baselines <path>     Path to aot-size-baselines.json (required).");
    writer.WriteLine("  --publish-dir <dir>    Directory containing the published AOT output (required).");
    writer.WriteLine("  --app-name <name>      Published binary name without extension (required).");
    writer.WriteLine("  --rid <rid>            RID for this measurement (defaults to RuntimeInformation.RuntimeIdentifier).");
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"AotSizeCheck: {message}");
    return 1;
}

static MeasureOptions ParseOptions(string[] args)
{
    string? baselines = null, publishDir = null, appName = null, rid = null;
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--baselines":   baselines  = RequireValue(args, ref i); break;
            case "--publish-dir": publishDir = RequireValue(args, ref i); break;
            case "--app-name":    appName    = RequireValue(args, ref i); break;
            case "--rid":         rid        = RequireValue(args, ref i); break;
            default: throw new ArgumentException($"Unknown option: {args[i]}");
        }
    }

    if (baselines is null)  throw new ArgumentException("--baselines is required");
    if (publishDir is null) throw new ArgumentException("--publish-dir is required");
    if (appName is null)    throw new ArgumentException("--app-name is required");
    rid ??= RuntimeInformation.RuntimeIdentifier;

    return new MeasureOptions(baselines, publishDir, appName, rid);
}

static string RequireValue(string[] args, ref int i)
{
    if (i + 1 >= args.Length)
    {
        throw new ArgumentException($"Missing value for {args[i]}");
    }

    return args[++i];
}

static long MeasureBinary(string publishDir, string appName, out string measuredPath)
{
    string exe = Path.Combine(publishDir, appName + ".exe");
    string noext = Path.Combine(publishDir, appName);
    measuredPath = File.Exists(exe) ? exe : noext;

    if (!File.Exists(measuredPath))
    {
        throw new FileNotFoundException(
            $"Published AOT binary not found. Looked for '{exe}' and '{noext}'. " +
            "Run 'dotnet publish' on the size-tracking app first.");
    }

    return new FileInfo(measuredPath).Length;
}

static int RunCheck(string[] args)
{
    MeasureOptions opts = ParseOptions(args);
    Baselines baselines = Baselines.Load(opts.BaselinesPath);
    long actual = MeasureBinary(opts.PublishDir, opts.AppName, out string measuredPath);

    bool hasBaseline = baselines.Platforms.TryGetValue(opts.Rid, out PlatformBaseline? platformBaseline);
    long? expected = platformBaseline?.SizeBytes;
    long tolerance = ComputeTolerance(baselines, expected);

    string verdict;
    long delta = 0;
    bool fail = false;

    if (!hasBaseline || expected is null)
    {
        verdict = "SKIP";
    }
    else
    {
        delta = actual - expected.Value;
        if (Math.Abs(delta) <= tolerance)
        {
            verdict = "PASS";
        }
        else
        {
            verdict = "FAIL";
            fail = true;
        }
    }

    PrintCheckSummary(opts, baselines, measuredPath, actual, expected, tolerance, delta, verdict);

    ResultRecord result = new(
        Rid: opts.Rid,
        AppName: opts.AppName,
        MeasuredPath: measuredPath,
        SizeBytes: actual,
        ExpectedBytes: expected,
        DeltaBytes: hasBaseline ? delta : null,
        ToleranceBytes: hasBaseline ? tolerance : null,
        Verdict: verdict);

    PrintResultBlock(result);

    return fail ? 1 : 0;
}

static int RunUpdate(string[] args)
{
    MeasureOptions opts = ParseOptions(args);
    Baselines baselines = Baselines.Load(opts.BaselinesPath);
    long actual = MeasureBinary(opts.PublishDir, opts.AppName, out string measuredPath);

    long? previous = baselines.Platforms.TryGetValue(opts.Rid, out PlatformBaseline? existing)
        ? existing?.SizeBytes
        : null;

    Console.WriteLine($"AotSizeCheck: updating baseline for rid={opts.Rid}");
    Console.WriteLine($"  measured: {measuredPath}");
    Console.WriteLine($"  previous: {(previous is null ? "<unset>" : FormatSize(previous.Value))}");
    Console.WriteLine($"  new:      {FormatSize(actual)}");

    baselines.Platforms[opts.Rid] = new PlatformBaseline(actual);
    baselines.Save(opts.BaselinesPath);

    Console.WriteLine($"  wrote:    {opts.BaselinesPath}");

    ResultRecord result = new(
        Rid: opts.Rid,
        AppName: opts.AppName,
        MeasuredPath: measuredPath,
        SizeBytes: actual,
        ExpectedBytes: actual,
        DeltaBytes: 0,
        ToleranceBytes: null,
        Verdict: "UPDATED");

    PrintResultBlock(result);
    return 0;
}

static long ComputeTolerance(Baselines baselines, long? expected)
{
    long absolute = baselines.ToleranceBytes;
    long percent = expected is { } e
        ? (long)Math.Ceiling(e * baselines.TolerancePercent / 100.0)
        : 0;
    return Math.Max(absolute, percent);
}

static void PrintCheckSummary(
    MeasureOptions opts,
    Baselines baselines,
    string measuredPath,
    long actual,
    long? expected,
    long tolerance,
    long delta,
    string verdict)
{
    Console.WriteLine($"AotSizeCheck rid={opts.Rid} app={opts.AppName}");
    Console.WriteLine($"  measured path : {measuredPath}");
    Console.WriteLine($"  measured size : {FormatSize(actual)} ({actual:N0} bytes)");
    if (expected is { } e)
    {
        Console.WriteLine($"  baseline size : {FormatSize(e)} ({e:N0} bytes)");
        Console.WriteLine($"  delta         : {FormatSignedSize(delta)} ({delta:+#,0;-#,0;0} bytes)");
        Console.WriteLine($"  tolerance     : +/-{FormatSize(tolerance)} (+/-{tolerance:N0} bytes; max of {baselines.ToleranceBytes:N0} B and {baselines.TolerancePercent}% of baseline)");
    }
    else
    {
        Console.WriteLine("  baseline size : <not set for this RID>");
        Console.WriteLine($"  tolerance     : +/-{FormatSize(tolerance)} (using configured floor of {baselines.ToleranceBytes:N0} B)");
    }
    Console.WriteLine($"  verdict       : {verdict}");

    if (verdict == "SKIP")
    {
        Console.WriteLine();
        Console.WriteLine($"::warning::No baseline committed for RID '{opts.Rid}'. Skipping regression check.");
        Console.WriteLine("To populate this RID, copy the size_bytes value from the AOT-SIZE-RESULT block");
        Console.WriteLine("below into aot-size-baselines.json under platforms.<rid>.size_bytes,");
        Console.WriteLine("or run `make update-aot-size-baseline` locally on this platform.");
    }
    else if (verdict == "FAIL")
    {
        Console.WriteLine();
        Console.WriteLine($"::error::AOT size for '{opts.Rid}' drifted by {FormatSignedSize(delta)} (outside +/-{FormatSize(tolerance)}). Investigate or update the baseline.");
    }
}

static void PrintResultBlock(ResultRecord record)
{
    Console.WriteLine();
    Console.WriteLine("=== AOT-SIZE-RESULT-BEGIN ===");
    Console.WriteLine(JsonSerializer.Serialize(record, ResultSerializerOptionsHolder.Value));
    Console.WriteLine("=== AOT-SIZE-RESULT-END ===");
}

static string FormatSize(long bytes)
{
    const double KB = 1024.0;
    const double MB = KB * 1024.0;
    double abs = Math.Abs(bytes);
    if (abs < KB)
    {
        return $"{bytes} B";
    }

    if (abs < MB)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} KB", bytes / KB);
    }

    return string.Format(CultureInfo.InvariantCulture, "{0:0.00} MB", bytes / MB);
}

static string FormatSignedSize(long bytes)
    => (bytes >= 0 ? "+" : "-") + FormatSize(Math.Abs(bytes));

internal static class ResultSerializerOptionsHolder
{
    public static readonly JsonSerializerOptions Value = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

internal sealed record MeasureOptions(string BaselinesPath, string PublishDir, string AppName, string Rid);

internal sealed record PlatformBaseline(
    [property: JsonPropertyName("size_bytes")] long SizeBytes);

internal sealed record ResultRecord(
    [property: JsonPropertyName("rid")] string Rid,
    [property: JsonPropertyName("app_name")] string AppName,
    [property: JsonPropertyName("measured_path")] string MeasuredPath,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("expected_bytes")] long? ExpectedBytes,
    [property: JsonPropertyName("delta_bytes")] long? DeltaBytes,
    [property: JsonPropertyName("tolerance_bytes")] long? ToleranceBytes,
    [property: JsonPropertyName("verdict")] string Verdict);

internal sealed class Baselines
{
    private JsonObject _raw = new();

    public long ToleranceBytes { get; set; }

    public double TolerancePercent { get; set; }

    public SortedDictionary<string, PlatformBaseline?> Platforms { get; } = new(StringComparer.Ordinal);

    public static Baselines Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Baselines file not found: '{path}'.", path);
        }

        string text = File.ReadAllText(path);
        JsonNode? node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (node is not JsonObject obj)
        {
            throw new InvalidDataException($"Baselines file '{path}' is not a JSON object.");
        }

        Baselines b = new()
        {
            _raw = obj,
            ToleranceBytes = obj["tolerance_bytes"]?.GetValue<long>()
                ?? throw new InvalidDataException("Baselines file is missing required field 'tolerance_bytes'."),
            TolerancePercent = obj["tolerance_percent"]?.GetValue<double>()
                ?? throw new InvalidDataException("Baselines file is missing required field 'tolerance_percent'."),
        };

        if (obj["platforms"] is JsonObject platforms)
        {
            foreach (var kvp in platforms)
            {
                if (kvp.Value is JsonObject p && p["size_bytes"]?.GetValue<long>() is long size)
                {
                    b.Platforms[kvp.Key] = new PlatformBaseline(size);
                }
                else
                {
                    b.Platforms[kvp.Key] = null;
                }
            }
        }

        return b;
    }

    public void Save(string path)
    {
        // Preserve any unknown properties / comments-as-fields in the
        // original file by writing back through the raw JsonObject.
        _raw["tolerance_bytes"] = ToleranceBytes;
        _raw["tolerance_percent"] = TolerancePercent;

        JsonObject platforms = new();
        foreach (var kvp in Platforms)
        {
            platforms[kvp.Key] = kvp.Value is { } p
                ? new JsonObject { ["size_bytes"] = p.SizeBytes }
                : null;
        }
        _raw["platforms"] = platforms;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        File.WriteAllText(path, _raw.ToJsonString(options) + Environment.NewLine);
    }
}
