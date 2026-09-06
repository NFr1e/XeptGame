// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;

namespace LeTai.TrueShadow.Demo
{
public struct HotDogSprite
{
    public Sprite sprite;
    public bool   isHotDog;

    public override string ToString()
    {
        var prefix = isHotDog ? "(Hot Dog) " : "";
        return prefix + sprite;
    }
}
}
