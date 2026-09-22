#if NET
using System.Globalization;

namespace PolyType.Tests;

internal static class TestHelpers
{
    // The CodeCoverage profiler can retain collectible ALC references on Unix runs; check both
    // CoreCLR and CLR profiling flags because test hosts may set either spelling. macOS CI also
    // exhibits unreliable unloads without exposing profiler flags to the test process.
    public static bool IsUnloadUnreliableEnvironment() => OperatingSystem.IsMacOS() || (!OperatingSystem.IsWindows() && IsProfilingEnabled());

    private static bool IsProfilingEnabled() =>
        IsProfilerFlagSet(Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING"))
        || IsProfilerFlagSet(Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING"));

    private static bool IsProfilerFlagSet(string? value)
    {
        string? trimmedValue = value?.Trim();
        if (string.IsNullOrEmpty(trimmedValue))
        {
            return false;
        }

        if (string.Equals(trimmedValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Profiler flags are commonly encoded as decimal, or as 0x-prefixed hex, with any non-zero value enabled.
        if (trimmedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(trimmedValue[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hexValue)
                && hexValue != 0;
        }

        return ulong.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong numericValue)
            && numericValue != 0;
    }
}
#endif
