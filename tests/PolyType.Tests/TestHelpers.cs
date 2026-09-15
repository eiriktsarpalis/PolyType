#if NET
using System.Globalization;

namespace PolyType.Tests;

internal static class TestHelpers
{
    // macOS and non-Windows profiler instrumentation can hold references that prevent collectible unload.
    public static bool IsUnloadUnreliableUnderProfiler() => OperatingSystem.IsMacOS() || (!OperatingSystem.IsWindows() && IsProfilingEnabled());

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
