#if !NET
namespace System;

internal static class EnumPolyfills
{
    extension(Enum)
    {
        public static bool IsDefined<TEnum>(TEnum value)
            where TEnum : struct, Enum
            => Enum.IsDefined(typeof(TEnum), value);

        public static TEnum Parse<TEnum>(string value)
            where TEnum : struct, Enum
            => (TEnum)Enum.Parse(typeof(TEnum), value);

        public static TEnum[] GetValues<TEnum>()
            where TEnum : struct, Enum
            => (TEnum[])Enum.GetValues(typeof(TEnum));
    }
}
#endif
