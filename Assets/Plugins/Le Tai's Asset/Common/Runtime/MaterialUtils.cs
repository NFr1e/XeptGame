// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Runtime.CompilerServices;
using UnityEngine;

namespace LeTai.Common
{
public static class MaterialUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetKeyword(Material mat, string keyword, bool isOn)
    {
        if (isOn)
            mat.EnableKeyword(keyword);
        else
            mat.DisableKeyword(keyword);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyKeyword(Material src, Material dst, string keyword)
    {
        SetKeyword(dst, keyword, src.IsKeywordEnabled(keyword));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyFloat(Material src, Material dst, int id)
    {
        dst.SetFloat(id, src.GetFloat(id));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyVector(Material src, Material dst, int id)
    {
        dst.SetVector(id, src.GetVector(id));
    }
}
}
