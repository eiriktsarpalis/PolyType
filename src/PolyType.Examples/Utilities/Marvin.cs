// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Adapted from https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Marvin.cs

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PolyType.Examples.Utilities;

[ExcludeFromCodeCoverage]
internal static class Marvin
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeHash32(ReadOnlySpan<byte> data, ulong seed)
    {
        unsafe
        {
            return ComputeHash32(ref MemoryMarshal.GetReference(data), (uint)data.Length, (uint)seed, (uint)(seed >> 32));
        }
    }

    private static int ComputeHash32(ref byte data, uint count, uint p0, uint p1)
    {
        unsafe
        {
            if (count < 8)
            {
                if (count >= 4)
                {
                    goto Between4And7BytesRemain;
                }
                else
                {
                    goto InputTooSmallToEnterMainLoop;
                }
            }

            uint loopCount = count / 8;
            Debug.Assert(loopCount > 0);

            do
            {
                p0 += Unsafe.ReadUnaligned<uint>(ref data);
                uint nextUInt32 = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref data, 4));

                Block(ref p0, ref p1);
                p0 += nextUInt32;
                Block(ref p0, ref p1);

                data = ref Unsafe.Add(ref data, 8);
            } while (--loopCount > 0);

            // The low three bits of the original count give the remaining byte count.
            if ((count & 4) == 0)
            {
                goto DoFinalPartialRead;
            }

        Between4And7BytesRemain:
            Debug.Assert(count >= 4);
            p0 += Unsafe.ReadUnaligned<uint>(ref data);
            Block(ref p0, ref p1);

        DoFinalPartialRead:
            Debug.Assert(count >= 4);

            // This read overlaps already-consumed bytes, which are shifted out below.
            // Keep the subtraction native-sized so Mono preserves negative offsets.
            uint partialResult = Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref data, (nint)(count & 7) - 4));
            count = ~count << 3;

            if (BitConverter.IsLittleEndian)
            {
                partialResult >>= 8;
                partialResult |= 0x8000_0000u;
                partialResult >>= (int)count & 0x1F;
            }
            else
            {
                partialResult <<= 8;
                partialResult |= 0x80u;
                partialResult <<= (int)count & 0x1F;
            }

        DoFinalRoundsAndReturn:
            p0 += partialResult;
            Block(ref p0, ref p1);
            Block(ref p0, ref p1);
            return (int)(p1 ^ p0);

        InputTooSmallToEnterMainLoop:
            partialResult = BitConverter.IsLittleEndian ? 0x80u : 0x8000_0000u;

            if ((count & 1) != 0)
            {
                partialResult = Unsafe.Add(ref data, (int)(count & 2));
                if (BitConverter.IsLittleEndian)
                {
                    partialResult |= 0x8000;
                }
                else
                {
                    partialResult <<= 24;
                    partialResult |= 0x800000u;
                }
            }

            if ((count & 2) != 0)
            {
                if (BitConverter.IsLittleEndian)
                {
                    partialResult <<= 16;
                    partialResult |= Unsafe.ReadUnaligned<ushort>(ref data);
                }
                else
                {
                    partialResult |= Unsafe.ReadUnaligned<ushort>(ref data);
                    partialResult = RotateLeft(partialResult, 16);
                }
            }

            goto DoFinalRoundsAndReturn;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Block(ref uint rp0, ref uint rp1)
    {
        uint p0 = rp0;
        uint p1 = rp1;

        p1 ^= p0;
        p0 = RotateLeft(p0, 20);
        p0 += p1;
        p1 = RotateLeft(p1, 9);
        p1 ^= p0;
        p0 = RotateLeft(p0, 27);
        p0 += p1;
        p1 = RotateLeft(p1, 19);

        rp0 = p0;
        rp1 = p1;
    }

    public static ulong DefaultSeed { get; } = GenerateSeed();

    private static ulong GenerateSeed()
    {
        byte[] seed = new byte[sizeof(ulong)];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(seed);
        unsafe
        {
            return MemoryMarshal.Read<ulong>(seed);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int shift) => (value << shift) | (value >> (32 - shift));
}
