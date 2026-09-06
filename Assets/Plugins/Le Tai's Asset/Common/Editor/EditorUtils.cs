// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace LeTai.Common.Editor
{
public static class EditorUtils
{
    public static List<string> GetDefines(BuildTarget buildTarget = BuildTarget.NoTarget)
    {
        if (buildTarget == BuildTarget.NoTarget)
            buildTarget = EditorUserBuildSettings.activeBuildTarget;

        var    currentGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
        string definesStr;
#if UNITY_2021_2_OR_NEWER
        var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(currentGroup);
        definesStr = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
        definesStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentGroup);
#endif
        return definesStr.Split(';').ToList();
    }

    public static void SetDefines(IEnumerable<string> defines, BuildTarget buildTarget = BuildTarget.NoTarget)
    {
        if (buildTarget == BuildTarget.NoTarget)
            buildTarget = EditorUserBuildSettings.activeBuildTarget;

        var newDefinesStr = string.Join(";", defines);

        var currentGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
#if UNITY_2021_2_OR_NEWER
        var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(currentGroup);
        PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefinesStr);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(currentGroup, newDefinesStr);
#endif
    }

    public static void AddDefines(HashSet<string> toAdd, BuildTarget buildTarget = BuildTarget.NoTarget)
    {
        if (buildTarget == BuildTarget.NoTarget)
            buildTarget = EditorUserBuildSettings.activeBuildTarget;

        var defines = GetDefines(buildTarget);
        var missing = toAdd.Except(defines).ToList();
        defines.AddRange(missing);
        if (missing.Count > 0)
            SetDefines(defines, buildTarget);
    }

    public static void RemoveDefines(HashSet<string> toRemove, BuildTarget buildTarget = BuildTarget.NoTarget)
    {
        if (buildTarget == BuildTarget.NoTarget)
            buildTarget = EditorUserBuildSettings.activeBuildTarget;

        var defines = GetDefines(buildTarget);
        var removed = defines.Except(toRemove).ToList();
        if (removed.Count < defines.Count)
            SetDefines(removed, buildTarget);
    }
}
}
