#if !NET6_0_OR_GREATER
namespace System;

internal static class ArrayExtensions
{
    extension(Array)
    {
        public static int MaxLength => 0x7FFFFFC7;
    }
}
#endif
