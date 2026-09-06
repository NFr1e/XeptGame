// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace LeTai.Common
{
public static class Packing
{
    /// <summary>
    /// Packs floats into a single float, using up to 30 bits.
    /// The mapping guarantees the resulting float is not NaN, Inf, or subnormal.
    /// </summary>
    public readonly struct FloatPacker
    {
        private readonly int _bitsPerFloat;

        private readonly uint _payload;
        private readonly int  _nBitsUsed;

        private FloatPacker(uint payload, int nBitsUsed, int bitsPerFloat)
        {
            _payload      = payload;
            _nBitsUsed    = nBitsUsed;
            _bitsPerFloat = bitsPerFloat;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloatPacker Varying() => new FloatPacker(0u, 0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloatPacker Uniform(int bitsPerFloat) => new FloatPacker(0u, 0, bitsPerFloat);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FloatPacker Enqueue(float value, float min, float max, int nBits)
        {
            if (nBits == 0)
                throw new ArgumentException($"Use {nameof(Uniform)} or specify {nameof(nBits)}");

            uint q = Quantize(value, min, max, nBits);
            return new FloatPacker((q << _nBitsUsed) | _payload, _nBitsUsed + nBits, _bitsPerFloat);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FloatPacker Enqueue(float value, float min, float max) => Enqueue(value, min, max, _bitsPerFloat);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FloatPacker Enqueue(float value, float max) => Enqueue(value, 0, max, _bitsPerFloat);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Finish()
        {
            if (_nBitsUsed > 30)
                throw new ArgumentOutOfRangeException(nameof(_nBitsUsed), $"Must use <= 30 bits. Requesting {_nBitsUsed} bits");

            uint bits = EnsureNormalFloatExponent(_payload);
            return UintToFloatBits(bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float(FloatPacker packer) => packer.Finish();
    }


    /// <summary>
    /// Packs floats into a single float, using up to 30 bits.
    /// The mapping guarantees the resulting float is not NaN, Inf, or subnormal.
    /// </summary>
    public static float PackFloatsSafe(
        float a, float minA, float maxA, int nBitsA,
        float b, float minB, float maxB, int nBitsB
    )
    {
        if (nBitsA + nBitsB > 30)
            throw new ArgumentOutOfRangeException(nameof(nBitsA), $"{nameof(nBitsA)} + {nameof(nBitsB)} must be <= 30");

        uint aQuantized = Quantize(a, minA, maxA, nBitsA);
        uint bQuantized = Quantize(b, minB, maxB, nBitsB);

        uint payload = (aQuantized << nBitsB) | bQuantized; // [0, (1u << 30) - 1]
        uint bits    = EnsureNormalFloatExponent(payload);

        return UintToFloatBits(bits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Quantize(float x, float min, float max, int nBits)
    {
        // Not checking positive range for speed.
        float normalized   = Mathf.Clamp((x - min) / (max - min), 0f, 1f);
        uint  maxQuantized = (1u << nBits) - 1u;
        return (uint)(normalized * maxQuantized + 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint EnsureNormalFloatExponent(uint bits)
    {
        uint exponent = (bits >> 23) + 1;
        uint mantissa = bits & 0x7FFFFFu;
        return (exponent << 23) | mantissa;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float UintToFloatBits(uint bits)
    {
        byte[] bytes = BitConverter.GetBytes(bits);
        return BitConverter.ToSingle(bytes, 0);
    }
}
}
