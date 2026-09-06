// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;
using UnityEngine.UI;

namespace LeTai.TrueShadow.Demo
{
[RequireComponent(typeof(Slider))]
public class BarGraphBar : MonoBehaviour
{
    Slider slider;

    public void Init(int max)
    {
        slider          = GetComponent<Slider>();
        slider.maxValue = max;
        slider.value    = 0;
    }

    public void SetValue(float value)
    {
        slider.value = value * slider.maxValue;
    }
}
}
