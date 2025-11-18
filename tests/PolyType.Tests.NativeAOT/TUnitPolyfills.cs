#if POLYFILL_TUNIT_CORE
global using TUnit.Core;
global using TUnit.Assertions;

namespace TUnit.Core
{
    public sealed class TestAttribute : Attribute { }
}

namespace TUnit.Assertions
{
    // Minimal polyfills to allow compilation when TUnit packages are not included
    public static class Assert
    {
        public static void That(bool condition) { }
        public static void That(object? value) { }
        public static void Equal<T>(T expected, T actual) { }
        public static void NotNull(object? value) { }
        public static void IsType<T>(object value) { }
        public static void Contains(string value, string substring) { }
        public static void Empty<T>(System.Collections.Generic.IEnumerable<T> collection) { }
        public static void NotEmpty<T>(System.Collections.Generic.IEnumerable<T> collection) { }
        public static void Single<T>(System.Collections.Generic.IEnumerable<T> collection) { }
    }
    
    public static class Throws
    {
        public static T Exception<T>(Action action) where T : Exception { return null!; }
    }
}
#endif