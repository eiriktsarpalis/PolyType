#if !NET
using System.Runtime.Serialization;

namespace System.Runtime.CompilerServices;

internal static class RuntimeHelpersPolyfills
{
    extension(RuntimeHelpers)
    {
        public static object GetUninitializedObject(Type type)
            => FormatterServices.GetUninitializedObject(type);
    }
}
#endif
