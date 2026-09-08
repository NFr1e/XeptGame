namespace XeptGame.Core
{
    public struct XeptGameConsts
    {
        public struct Editor
        {
            #region CreateAssetMenu 配置

            #region Player
            private const string PlayerProfileMenuName = "XeptGame/Player";

            public const string PlayerMotorProfileMenuName = PlayerProfileMenuName + "/PlayerMotorProfile";
            public const string PlayerMotorProfileFileName = "PlayerMotorProfile";
            public const int PlayerMotorProfileOrder = 0;

            public const string PlayerLookProfileMenuName = PlayerProfileMenuName + "/PlayerLookProfile";
            public const string PlayerLookProfileFileName = "PlayerLookProfile";
            public const int PlayerLookProfileOrder = 1;

            public const string PlayerCameraFeelProfileMenuName = PlayerProfileMenuName + "/PlayerCameraFeelProfile";
            public const string PlayerCameraFeelProfileFileName = "PlayerCameraFeelProfile";
            public const int PlayerCameraFeelProfileOrder = 2;
            #endregion

            #region Interaction
            private const string InteractionProfileMenuRoot = "XeptGame/Interaction";

            public const string InteractionProfileMenuName = InteractionProfileMenuRoot + "/InteractionProfile";
            public const string InteractionProfileFileName = "InteractionProfile";
            public const int InteractionProfileOrder = 0;
            #endregion

            #region Items
            private const string ItemMenuRoot = "XeptGame/Items";
            private const string ItemFacetMenuRoot = ItemMenuRoot + "/Facets";

            public const string ItemDefinitionMenuName = ItemMenuRoot + "/ItemDefinition";
            public const string ItemDefinitionFileName = "ItemDefinition";
            public const int ItemDefinitionOrder = 0;

            public const string ItemWorldFacetProfileMenuName = ItemFacetMenuRoot + "/WorldFacetProfile";
            public const string ItemWorldFacetProfileFileName = "WorldFacetProfile";
            public const int ItemWorldFacetProfileOrder = 0;

            public const string ItemHoldableFacetProfileMenuName = ItemFacetMenuRoot + "/HoldFacetProfile";
            public const string ItemHoldableFacetProfileFileName = "HoldFacetProfile";
            public const int ItemHoldableFacetProfileOrder = 1;
            #endregion

            #endregion
        }

        /// <summary>
        /// 资产地址（Addressables address，经 IAssetLoader 加载——Editor/运行时统一）。
        /// 基础设施场景组走资产 key 而非序列化注入：index 0 为 AppEntry 场景，应用级/玩法级组非启动即加载，
        /// 序列化引用有鸡生蛋问题（见 GameplayFlow_Design.md §2.2/§4.2）。
        /// 注：address 以 Addressables Groups 中实际注册为准（AppConfigs / GameplayConfig 组条目，短名）。
        /// </summary>
        public struct AssetKeys
        {
            /// <summary>应用级基础设施场景组（AppCore 主场景 + AppMainMenu；UI 上下文/音频等），AppFSM.StartingState 加载。</summary>
            public const string AppCoreGroup = "#AppCoreSceneGroup";

            /// <summary>玩法级基座场景组（Gameplay 场景组：GameplayCore 主场景），GameplayLoadState 加载。</summary>
            public const string GameplaySceneGroup = "#GameplayCoreSceneGroup";
        }

        /// <summary>交互执行参数。</summary>
        public struct Interaction
        {
            /// <summary>E 长按判定阈值（秒）：按住超过该时长且宿主有可用 Hold 动作 → 升级为 Hold 分派（tap 取消）。</summary>
            public const float HoldPressThresholdSeconds = 0.35f;
        }
    }
}
