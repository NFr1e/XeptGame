// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;
using UnityEngine.Rendering;

namespace LeTai.Effects
{
public interface IBlurAlgorithm
{
    void Configure(BlurConfig config);

    void Blur(
        CommandBuffer          cmd,
        RenderTargetIdentifier src,
        Rect                   srcCropRegion,
        RenderTexture          target
    );
}

public enum BlurAlgorithmSelection
{
    Fast,
    Accurate
}
}
