using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.UI.Billboard;

public class HudBillboardTest : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private HudBillboard test;

    private void Awake()
    {
        test.CameraTransform = cam.transform;
        test.SetAnchor(transform);
    }
}
