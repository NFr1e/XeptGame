// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEngine;

namespace LeTai.Common
{
public static class SpanUtils
{
    public static Vector4 ToVector4(ReadOnlySpan<float> span)
    {
        return new Vector4(span[0], span[1], span[2], span[3]);
    }

    public static SpanWriter<T> WriterFor<T>(Span<T> span) => new(span);
}

public ref struct SpanWriter<T>
{
    readonly Span<T> _span;
    int              _nextIndex;

    public SpanWriter(Span<T> span)
    {
        _span      = span;
        _nextIndex = 0;
    }

    public void Reset() => _nextIndex = 0;

    public void Write(T value)
    {
        _span[_nextIndex++] = value;
    }

    public void FillRest(T value = default)
    {
        if (_nextIndex == _span.Length)
            return;

        _span[_nextIndex..].Fill(value);
        _nextIndex = _span.Length;
    }
}
}
