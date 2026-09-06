// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using System.Linq;
using LeTai.TrueShadow.PluginInterfaces;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.TrueShadow
{
public partial class TrueShadow
{
    ITrueShadowCasterMaterialProvider           casterMaterialProvider;
    ITrueShadowCasterSubMeshMaterialProvider    casterSubMeshMaterialProvider;
    ITrueShadowCasterMeshProvider               casterMeshProvider;
    ITrueShadowCasterMaterialPropertiesModifier casterMaterialPropertiesModifier;
    ITrueShadowCasterMeshModifier               casterMeshModifier;
    ITrueShadowCasterClearColorProvider         casterClearColorProvider;
    ITrueShadowRendererMaterialProvider         rendererMaterialProvider;
    ITrueShadowRendererMaterialModifier         rendererMaterialModifier;
    ITrueShadowRendererMeshModifier             rendererMeshModifier;
    ITrueShadowCustomHashProviderV2             customHashProviderV2;

    public bool UsingRendererMaterialProvider => rendererMaterialProvider != null;

    void InitializePlugins()
    {
        casterMaterialProvider           = GetComponent<ITrueShadowCasterMaterialProvider>();
        casterSubMeshMaterialProvider    = GetComponent<ITrueShadowCasterSubMeshMaterialProvider>();
        casterMeshProvider               = GetComponent<ITrueShadowCasterMeshProvider>();
        casterMaterialPropertiesModifier = GetComponent<ITrueShadowCasterMaterialPropertiesModifier>();
        casterMeshModifier               = GetComponent<ITrueShadowCasterMeshModifier>();
        casterClearColorProvider         = GetComponent<ITrueShadowCasterClearColorProvider>();

        rendererMaterialProvider = GetComponent<ITrueShadowRendererMaterialProvider>();
        rendererMaterialModifier = GetComponent<ITrueShadowRendererMaterialModifier>();
        rendererMeshModifier     = GetComponent<ITrueShadowRendererMeshModifier>();

        customHashProviderV2 = GetComponent<ITrueShadowCustomHashProviderV2>();

        if (casterMeshProvider != null)
        {
            casterMeshProvider.trueShadowCasterMeshChanged += HandleCasterMeshProviderMeshChanged;
        }
        if (casterMaterialProvider != null)
        {
            casterMaterialProvider.materialReplaced += HandleCasterMaterialReplaced;
            casterMaterialProvider.materialModified += HandleCasterMaterialModified;
        }
        if (rendererMaterialProvider != null)
        {
            rendererMaterialProvider.materialReplaced += HandleRendererMaterialReplaced;
            rendererMaterialProvider.materialModified += HandleRendererMaterialModified;
        }
        if (customHashProviderV2 != null)
        {
            customHashProviderV2.trueShadowCustomHashChanged += HandleTrueShadowCustomHashChanged;
        }
    }

    void TerminatePlugins()
    {
        if (casterMeshProvider != null)
        {
            casterMeshProvider.trueShadowCasterMeshChanged -= HandleCasterMeshProviderMeshChanged;
        }
        if (casterMaterialProvider != null)
        {
            casterMaterialProvider.materialReplaced -= HandleCasterMaterialReplaced;
            casterMaterialProvider.materialModified -= HandleCasterMaterialModified;
        }
        if (rendererMaterialProvider != null)
        {
            rendererMaterialProvider.materialReplaced -= HandleRendererMaterialReplaced;
            rendererMaterialProvider.materialModified -= HandleRendererMaterialModified;
        }
        if (customHashProviderV2 != null)
        {
            customHashProviderV2.trueShadowCustomHashChanged -= HandleTrueShadowCustomHashChanged;
        }
    }

    public void RefreshPlugins()
    {
        TerminatePlugins();
        InitializePlugins();
    }

    void HandleCasterMeshProviderMeshChanged(Mesh mesh)
    {
        SpriteMeshHandle = ObjectHandle.Borrow(mesh);
        SetLayoutTextureDirty();
    }

    void HandleCasterMaterialReplaced()
    {
        SetTextureDirty();
    }

    void HandleRendererMaterialReplaced()
    {
        if (shadowRenderer)
            shadowRenderer.UpdateMaterial();
    }

    void HandleCasterMaterialModified()
    {
        SetTextureDirty();
    }

    void HandleRendererMaterialModified()
    {
        if (shadowRenderer)
            shadowRenderer.SetMaterialDirty();
    }

    void HandleTrueShadowCustomHashChanged(int customHash)
    {
        CustomHash = customHash;
        SetLayoutTextureDirty();
    }

    public virtual Material GetShadowCastingMaterial()
    {
        if (casterSubMeshMaterialProvider != null)
            return casterSubMeshMaterialProvider.GetTrueShadowCasterMaterialForSubMesh(0);

        Material provided = null;

        if (casterMaterialProvider != null)
            provided = casterMaterialProvider.GetTrueShadowCasterMaterial();

#if TMP_PRESENT
        else if (Graphic is TMPro.TextMeshProUGUI
              || Graphic is TMPro.TMP_SubMeshUI)
        {
            provided = Graphic.materialForRendering;
        }
#endif

        return provided != null ? provided : Graphic.material;
    }

    public virtual Material GetShadowCastingMaterialForSubMesh(int subMeshIndex)
    {
        if (casterSubMeshMaterialProvider == null)
        {
            Debug.LogError("Custom UI that use submeshes must implement ITrueShadowCasterSubMeshMaterialProvider");
            return null;
        }

        return casterSubMeshMaterialProvider.GetTrueShadowCasterMaterialForSubMesh(subMeshIndex);
    }

    public virtual void ModifyShadowCastingMaterialProperties(MaterialPropertyBlock propertyBlock)
    {
        casterMaterialPropertiesModifier?.ModifyTrueShadowCasterMaterialProperties(propertyBlock);
    }

    public virtual void ModifyShadowCastingMesh(Mesh mesh)
    {
        casterMeshModifier?.ModifyTrueShadowCasterMesh(mesh);

        // Caster can be semi-transparent, but cutout requires mostly opaque stencil.
        // Setting alpha to 1 in fragment can't work because of antialiasing.
        MakeOpaque(mesh);
    }

    readonly List<Color32> meshColors       = new List<Color32>(4);
    readonly List<Color32> meshColorsOpaque = new List<Color32>(4);

    void MakeOpaque(Mesh mesh)
    {
        if (shadowAsSibling)
            return;

        mesh.GetColors(meshColors);
        var meshColorCount = meshColors.Count;

        if (meshColorCount < 1) return;

        if (meshColorsOpaque.Count == meshColorCount)
        {
            // Assuming vertex colors are identical
            // TODO: This is the case for builtin graphics, but userscript may invalidate that.
            if (meshColors[0].a == meshColorsOpaque[0].a)
                return;
        }
        else
        {
            // TODO: This assumed vertex count change infrequently. Is not the case with Text
            meshColorsOpaque.Clear();
            meshColorsOpaque.AddRange(Enumerable.Repeat(new Color32(0, 0, 0, 0), meshColorCount));
        }

        for (var i = 0; i < meshColorCount; i++)
        {
            var c = meshColors[i];
            c.a = 255;

            meshColorsOpaque[i] = c;
        }

        mesh.SetColors(meshColorsOpaque);
    }

    public virtual Material GetShadowRenderingMaterial()
    {
        var provided = rendererMaterialProvider?.GetTrueShadowRendererMaterial();
        return provided != null ? provided : BlendMode.GetMaterial();
    }

    public virtual void ModifyShadowRendererMaterial(Material baseMaterial)
    {
        rendererMaterialModifier?.ModifyTrueShadowRendererMaterial(baseMaterial);
    }

    public virtual void ModifyShadowRendererMesh(VertexHelper vertexHelper)
    {
        rendererMeshModifier?.ModifyTrueShadowRendererMesh(vertexHelper);
    }
}
}
