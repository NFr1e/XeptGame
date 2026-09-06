// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEditor;

namespace LeTai.Common.Editor
{
public class EditorPrefValue<T>
{
    readonly string key;
    readonly T      initialValue;

    public T Value
    {
        get
        {
            if (!EditorPrefs.HasKey(key))
            {
                Value = initialValue;
                return initialValue;
            }

            return typeof(T) switch {
                { } t when t == typeof(bool)  => (T)(object)EditorPrefs.GetBool(key),
                { } t when t == typeof(float) => (T)(object)EditorPrefs.GetFloat(key),

                _ => throw new ArgumentException("Type " + typeof(T) + " is not supported.")
            };
        }
        set
        {
            switch (value)
            {
            case bool v:
                EditorPrefs.SetBool(key, v);
                break;
            case float v:
                EditorPrefs.SetFloat(key, v);
                break;
            default:
                throw new ArgumentException("Type " + typeof(T) + " is not supported.");
            }
        }
    }

    public EditorPrefValue(string key, T initialValue = default)
    {
        this.key          = key;
        this.initialValue = initialValue;
    }

    public static implicit operator T(EditorPrefValue<T> value)
    {
        return value.Value;
    }
}
}
