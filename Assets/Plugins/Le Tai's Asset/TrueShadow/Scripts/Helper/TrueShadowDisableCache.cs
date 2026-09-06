// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Collections.Generic;
using LeTai.TrueShadow.PluginInterfaces;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace LeTai.TrueShadow
{
[AddComponentMenu("UI/True Shadow/True Shadow Disable Cache")]
[ExecuteAlways]
[RequireComponent(typeof(TrueShadow))]
public class TrueShadowDisableCache : MonoBehaviour, ITrueShadowCustomHashProviderV2
{
    public event Action<int> trueShadowCustomHashChanged;

    [Tooltip("Force shadow to refresh every frame. Useful for animated effects")]
    public bool everyFrame;

    [Tooltip("Some 3rd party effects doesn't mark the Graphic as dirty when modifying them. Try this if the above doesn't work")]
    public bool markGraphicDirty;

    Graphic _graphic;

    void OnEnable()
    {
        _graphic = GetComponent<Graphic>();

        Canvas.preWillRenderCanvases += PreWillRenderCanvases;
    }

#if UNITY_EDITOR
    static readonly List<Component> COMPONENT_LIST = new List<Component>();

    void Reset()
    {
        GetComponents(COMPONENT_LIST);
        foreach (var component in COMPONENT_LIST)
        {
            if (component.GetType().FullName == "Febucci.UI.TextAnimator_TMP")
            {
                markGraphicDirty = true;
                break;
            }
        }
    }
#endif

    void PreWillRenderCanvases()
    {
        Canvas.preWillRenderCanvases -= PreWillRenderCanvases;

        Dirty();
    }

    void Update()
    {
        if (everyFrame)
            Dirty();
    }

    void Dirty()
    {
        if (markGraphicDirty) _graphic.SetAllDirty();

        trueShadowCustomHashChanged?.Invoke(Random.Range(int.MinValue, int.MaxValue));
    }

    void OnDisable()
    {
        trueShadowCustomHashChanged?.Invoke(0);
    }
}
}
