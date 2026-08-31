namespace XeptGame.Core
{
    public struct XeptGameConsts
    {
        public struct Editor
        {
            private const string PlayerProfileMenuName = "XeptGame/Profiles/Player";

            public const string PlayerMotorProfileMenuName = PlayerProfileMenuName + "/PlayerMotorProfile";
            public const string PlayerMotorProfileFileName = "PlayerMotorProfile";
            public const int PlayerMotorProfileOrder = 0;

            public const string PlayerLookProfileMenuName = PlayerProfileMenuName + "/PlayerLookProfile";
            public const string PlayerLookProfileFileName = "PlayerLookProfile";
            public const int PlayerLookProfileOrder = 1;

            public const string PlayerCameraFeelProfileMenuName = PlayerProfileMenuName + "/PlayerCameraFeelProfile";
            public const string PlayerCameraFeelProfileFileName = "PlayerCameraFeelProfile";
            public const int PlayerCameraFeelProfileOrder = 2;
        }
    }
}
