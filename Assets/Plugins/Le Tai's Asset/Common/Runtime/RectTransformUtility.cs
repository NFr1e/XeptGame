// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;

namespace LeTai.Common
{
public static class RectTransformUtilityPatch
{
    public static Ray ScreenPointToRay(Camera cam, Vector2 screenPos)
    {
        if (cam != null)
            return cam.ScreenPointToRay(screenPos);

        Vector3 pos = screenPos;
        pos.z -= 1e4f; // Larger offset to account for rotation
        return new Ray(pos, Vector3.forward);
    }

    public static bool ScreenPointToWorldPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector3 worldPoint)
    {
        worldPoint = Vector2.zero;
        Ray ray   = ScreenPointToRay(cam, screenPoint);
        var plane = new Plane(rect.rotation * Vector3.back, rect.position);

        // Remove unnecessary parallel check
        if (!plane.Raycast(ray, out var dist))
            return false;

        worldPoint = ray.GetPoint(dist);
        return true;
    }

    public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (ScreenPointToWorldPointInRectangle(rect, screenPoint, cam, out Vector3 worldPoint))
        {
            localPoint = rect.InverseTransformPoint(worldPoint);
            return true;
        }
        return false;
    }
}
}
