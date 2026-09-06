// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using LeTai.TrueShadow.PluginInterfaces;
using UnityEngine;

namespace LeTai.TrueShadow
{
[AddComponentMenu("UI/True Shadow/True Shadow Custom Material")]
[ExecuteAlways]
public class TrueShadowCustomMaterial : MonoBehaviour, ITrueShadowRendererMaterialProvider
{
    public Material material;

    public event Action materialReplaced;
    public event Action materialModified;

    public Material GetTrueShadowRendererMaterial()
    {
        if (!isActiveAndEnabled) // Component Destroyed
            return null;

        return material;
    }

    void OnEnable()
    {
        var ts = GetComponent<TrueShadow>();
        if (ts)
        {
            ts.RefreshPlugins();
        }

        materialReplaced?.Invoke();
    }

    void OnDisable()
    {
        var ts = GetComponent<TrueShadow>();
        if (ts)
        {
            ts.RefreshPlugins();
        }
        materialReplaced?.Invoke();
    }

    void OnValidate()
    {
        materialReplaced?.Invoke();
    }

    public void OnMaterialModified()
    {
        materialModified?.Invoke();
    }
}
}
