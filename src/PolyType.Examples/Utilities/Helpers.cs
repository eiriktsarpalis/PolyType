﻿using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PolyType.Abstractions;

namespace PolyType.Examples.Utilities;

internal static class Helpers
{
    public static void ThrowMissingRequiredArguments<TArgumentState>(ref TArgumentState argumentState, IReadOnlyList<IParameterShape> parameters)
        where TArgumentState : IArgumentState
    {
        Debug.Assert(!argumentState.AreRequiredArgumentsSet);
        List<string> missingRequiredParams = new();
        foreach (IParameterShape parameter in parameters)
        {
            if (parameter.IsRequired && !argumentState.IsArgumentSet(parameter.Position))
            {
                missingRequiredParams.Add($"'{parameter.Name}'");
            }
        }

        throw new KeyNotFoundException($"Missing required parameters: {string.Join(", ", missingRequiredParams)}");
    }

    public static string ConvertBytesToHexString(byte[] bytes)
    {
#if NET
        return Convert.ToHexString(bytes);
#else
        StringBuilder sb = new(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
#endif
    }

    public static byte[] ConvertHexStringToBytes(string hex)
    {
#if NET
        return Convert.FromHexString(hex);
#else
        if (hex.Length % 2 != 0)
        {
            throw new FormatException("The hex string must have an even number of characters.");
        }

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
#endif
    }

#if !NET
    public static unsafe int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
    {
        fixed (byte* pBytes = bytes)
        fixed (char* pChars = chars)
        {
            return encoding.GetChars(pBytes, bytes.Length, pChars, chars.Length);
        }
    }

    public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        fixed (byte* pBytes = bytes)
        {
            return encoding.GetString(pBytes, bytes.Length);
        }
    }
#endif

#if !NET
    public static void AddBytes(this HashCode hashCode, ReadOnlySpan<byte> bytes)
    {
        int length = bytes.Length;
        int i = 0;
        for (; i < length - 3; i += 4)
        {
            hashCode.Add(bytes[i] | (bytes[i + 1] << 8) | (bytes[i + 2] << 16) | (bytes[i + 3] << 24));
        }

        for (; i < length; i++)
        {
            hashCode.Add(bytes[i]);
        }
    }
#endif

    public readonly struct UnsafeArraySpan<TElement>(Array array) : IDisposable
    {
        private readonly GCHandle _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        public readonly unsafe Span<TElement> Span => new(_handle.AddrOfPinnedObject().ToPointer(), array.Length);
        public void Dispose() => _handle.Free();
    }

#if !NET
    public static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> values, out int count)
    {
        if (values is ICollection<T> collection)
        {
            count = collection.Count;
            return true;
        }

        if (values is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        count = -1;
        return false;
    }

    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> values,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        return values.ToDictionary(pair => pair.Key, pair => pair.Value, keyComparer);
    }

    public static void Replace<T>(this Span<T> values, T oldValue, T newValue)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (EqualityComparer<T>.Default.Equals(values[i], oldValue))
            {
                values[i] = newValue;
            }
        }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrContainsReferences<T>()
    {
#if NET
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
        HashSet<Type>? visited = null;
        return IsReferenceOrContainsReferencesCore(typeof(T));
        bool IsReferenceOrContainsReferencesCore(Type type)
        {
            if (!type.IsValueType)
            {
                return true;
            }

            if (!(visited ??= new()).Add(type))
            {
                return false;
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsReferenceOrContainsReferencesCore(field.FieldType))
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }

    /// <summary>
    /// Gets an enumerable view that is potentially blocking if the source collection is an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    public static Func<TEnumerable, IEnumerable<TElement>> GetGetPotentiallyBlockingEnumerable<TEnumerable, TElement>(this IEnumerableTypeShape<TEnumerable, TElement> shape)
    {
        Debug.Assert(!shape.IsAsyncEnumerable || typeof(IAsyncEnumerable<TElement>).IsAssignableFrom(typeof(TEnumerable)));
        return shape.IsAsyncEnumerable
            ? (Func<TEnumerable, IEnumerable<TElement>>)(object)new Func<IAsyncEnumerable<TElement>, IEnumerable<TElement>>(iae => iae.ToBlockingEnumerable())
            : shape.GetGetEnumerable();
    }
    
#if !NET
    private static IEnumerable<T> ToBlockingEnumerable<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<T> enumerator = source.GetAsyncEnumerator(cancellationToken);
        // A ManualResetEventSlim variant that lets us reuse the same
        // awaiter callback allocation across the entire enumeration.
        ManualResetEventWithAwaiterSupport? mres = null;

        try
        {
            while (true)
            {
#pragma warning disable CA2012 // Use ValueTasks correctly
                ValueTask<bool> moveNextTask = enumerator.MoveNextAsync();
#pragma warning restore CA2012 // Use ValueTasks correctly

                if (!moveNextTask.IsCompleted)
                {
                    (mres ??= new()).Wait(moveNextTask.ConfigureAwait(false).GetAwaiter());
                    Debug.Assert(moveNextTask.IsCompleted);
                }

                if (!moveNextTask.Result)
                {
                    yield break;
                }

                yield return enumerator.Current;
            }
        }
        finally
        {
            ValueTask disposeTask = enumerator.DisposeAsync();

            if (!disposeTask.IsCompleted)
            {
                (mres ?? new()).Wait(disposeTask.ConfigureAwait(false).GetAwaiter());
                Debug.Assert(disposeTask.IsCompleted);
            }

            disposeTask.GetAwaiter().GetResult();
        }
    }

    private sealed class ManualResetEventWithAwaiterSupport : ManualResetEventSlim
    {
        private readonly Action _onCompleted;

        public ManualResetEventWithAwaiterSupport()
        {
            _onCompleted = Set;
        }
        
        public void Wait<TAwaiter>(TAwaiter awaiter) where TAwaiter : ICriticalNotifyCompletion
        {
            awaiter.UnsafeOnCompleted(_onCompleted);
            Wait();
            Reset();
        }
    }
#endif
}