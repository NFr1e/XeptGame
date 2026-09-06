// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEditor;

namespace LeTai.TrueShadow.Editor
{
[InitializeOnLoad]
class PrefabEventHandler
{
    static PrefabEventHandler()
    {
        PrefabUtility.prefabInstanceUpdated += go =>
        {
            var shadows = go.GetComponentsInChildren<TrueShadow>();

            foreach (var shadow in shadows)
                shadow.ApplySerializedData();
        };
    }
}
}
