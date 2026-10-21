#if !NET
namespace System;

internal static class TypePolyfills
{
    extension(Type type)
    {
        public bool IsByRefLike
        {
            get
            {
                if (!type.IsValueType)
                {
                    return false;
                }

                foreach (var attributeData in type.GetCustomAttributesData())
                {
                    if (attributeData.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsGenericMethodParameter => type.IsGenericParameter && type.DeclaringMethod is not null;
    }
}
#endif
