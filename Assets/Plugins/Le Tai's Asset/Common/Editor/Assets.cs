// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEditor;
using UnityEngine;

namespace LeTai.Common.Editor
{
public static class Assets
{
    public static T Find<T>(string assetName, string label = "TranslucentImageEditorResources") where T : Object
    {
        var guids = AssetDatabase.FindAssets($"l:{label} {assetName}");
        if (guids.Length == 0)
        {
            Debug.LogError($"Asset \"{assetName}\" not found. " +
                           $"Make sure it have the label \"TranslucentImageEditorResources\"");
            return null;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
}
