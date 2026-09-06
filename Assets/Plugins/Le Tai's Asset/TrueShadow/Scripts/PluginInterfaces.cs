// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.TrueShadow.PluginInterfaces
{
public interface ITrueShadowCasterMaterialProvider
{
    event Action materialReplaced;
    event Action materialModified;
    Material     GetTrueShadowCasterMaterial();
}

public interface ITrueShadowCasterSubMeshMaterialProvider
{
    Material GetTrueShadowCasterMaterialForSubMesh(int subMeshIndex);
}

public interface ITrueShadowCasterMeshProvider
{
    event Action<Mesh> trueShadowCasterMeshChanged;
}

public interface ITrueShadowCasterMeshModifier
{
    void ModifyTrueShadowCasterMesh(Mesh mesh);
}

public interface ITrueShadowCasterMaterialPropertiesModifier
{
    void ModifyTrueShadowCasterMaterialProperties(MaterialPropertyBlock propertyBlock);
}

public interface ITrueShadowCasterClearColorProvider
{
    Color GetTrueShadowCasterClearColor();
}

public interface ITrueShadowRendererMaterialProvider
{
    event Action materialReplaced;
    event Action materialModified;
    Material     GetTrueShadowRendererMaterial();
}

public interface ITrueShadowRendererMaterialModifier
{
    void ModifyTrueShadowRendererMaterial(Material baseMaterial);
}

public interface ITrueShadowRendererMeshModifier
{
    void ModifyTrueShadowRendererMesh(VertexHelper vertexHelper);
}

/// <summary>
/// Use <see cref="ITrueShadowCustomHashProviderV2"/> instead
/// </summary>
public interface ITrueShadowCustomHashProvider { }

public interface ITrueShadowCustomHashProviderV2
{
    public event Action<int> trueShadowCustomHashChanged;
}
}
