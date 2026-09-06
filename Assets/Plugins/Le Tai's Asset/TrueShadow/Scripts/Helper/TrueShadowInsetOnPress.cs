// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;

namespace LeTai.TrueShadow
{
[AddComponentMenu("UI/True Shadow/True Shadow Inset On Press")]
[RequireComponent(typeof(TrueShadow))]
public class TrueShadowInsetOnPress : AnimatedBiStateButton
{
    TrueShadow[] _shadows;
    float[]      _normalOpacity;
    bool         _wasInset;

    void OnEnable()
    {
        _shadows       = GetComponents<TrueShadow>();
        _normalOpacity = new float[_shadows.Length];
    }

    protected override void Animate(float visualPressAmount)
    {
        void SetAllOpacity(float lerpProgress)
        {
            for (var i = 0; i < _shadows.Length; i++)
            {
                var color = _shadows[i].Color;
                color.a           = Mathf.Lerp(0, _normalOpacity[i], lerpProgress);
                _shadows[i].Color = color;
            }
        }

        bool shouldInset = visualPressAmount > .5f;

        if (shouldInset != _wasInset)
        {
            for (var i = 0; i < _shadows.Length; i++)
            {
                _shadows[i].Inset = shouldInset;
            }

            _wasInset = shouldInset;
        }

        if (shouldInset)
        {
            SetAllOpacity(visualPressAmount * 2f - 1f);
        }
        else
        {
            SetAllOpacity(1 - visualPressAmount * 2f);
        }
    }

    void MemorizeOpacity()
    {
        if (IsAnimating) return;

        for (var i = 0; i < _shadows.Length; i++)
        {
            _normalOpacity[i] = _shadows[i].Color.a;
        }
    }

    protected override void OnWillPress()
    {
        _wasInset = _shadows[0].Inset;
        MemorizeOpacity();
        base.OnWillPress();
    }
}
}
