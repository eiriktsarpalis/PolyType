#if POLYFILL_TUNIT_CORE
global using TUnit.Core;
global using TUnit.Assertions;

namespace TUnit.Core;
public sealed class TestAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ArgumentsAttribute(params object?[] arguments) : Attribute
{
    public object?[] Arguments { get; } = arguments;
}
#endif