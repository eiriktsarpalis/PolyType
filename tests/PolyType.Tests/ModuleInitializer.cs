using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PolyType.Tests;

internal static class AvoidCrashingOnDebugAsserts
{
    [ModuleInitializer]
    internal static void Initializer()
    {
        if (Trace.Listeners.OfType<DefaultTraceListener>().FirstOrDefault() is { AssertUiEnabled: true } defaultListener)
        {
            // Avoid crashing the test process.
            defaultListener.AssertUiEnabled = false;
        }

        // But _do_ throw an exception so the scenario fails.
        Trace.Listeners.Add(new ThrowListener());
    }

    [ExcludeFromCodeCoverage]
    private class ThrowListener : TraceListener
    {
        public override void Fail(string? message)
        {
            Assert.Fail($"Assertion failed: {message}.");
        }

        public override void Fail(string? message, string? detailMessage)
        {
            Assert.Fail($"Assertion failed: {message}. {detailMessage}");
        }

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }
}
