using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XeptKit.UI.Manager;

namespace XeptGame
{
    public class AppUIBridge : MonoBehaviour
    {
        [SerializeField] private UIComponent UIComponent;

        private IUIManager _uiMgr;

        private void Awake()
        {
            _uiMgr ??= AppEntry.UIManager;

            if(_uiMgr is IUISceneContext ctx)
            {
                if(UIComponent)
                    UIComponent.Target = ctx;
            }
        }
    }
}
