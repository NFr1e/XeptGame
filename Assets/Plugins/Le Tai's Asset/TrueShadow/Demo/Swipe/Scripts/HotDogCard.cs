// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.SwipeView;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.TrueShadow.Demo
{
public class HotDogCard : Swipable<HotDogSprite>
{
    public Image content;

    public override void SetData(HotDogSprite data)
    {
        Data = data;

        content.sprite = Data.sprite;
    }
}
}
