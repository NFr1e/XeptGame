using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 第一人称视角（观感层输入，纯消费者）：
    /// 每帧读取输入层产出的 Look 增量 → 驱动 <see cref="PlayerLookController"/> 维护视角角度 →
    /// 把基准旋转（yaw + pitch）写入 <see cref="CameraRig.BaseRotation"/>（合成器 LateUpdate 应用，
    /// 同帧应用，"鼠标移到哪指哪"语义不变）→
    /// 把移动意图（MoveInput × 视角 yaw）写入电机输入状态（LocalMoveIntent，body 局部参考系）。
    /// 本类不持有视角角度、**不写相机 transform**（唯一写入者为 CameraRig）；Yaw/Pitch 权威在
    /// <see cref="PlayerLookController"/>。
    /// 层级约定：眼位挂点（CameraRig）为 body（KCC 电机）子级（位置跟随 body）。
    /// **相机 local 旋转（含 yaw）**：相机世界 = body × local——站旋转平台上相机随 body 旋转
    /// （身体=视角，符合真实项目）；离开旋转平台时 body 残转并入 LookController.Yaw 并重置 body
    /// 的处理为待办（视角世界朝向连续性，见 3C_CharacterMotor_Design.md 扩展节）。
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private CameraRig cameraRig;

        private PlayerLookController _lookController;
        private PlayerMotorInputState _motorInput;
        private PlayerLookInputState _lookInput;

        private bool _initialized;

        /// <summary>装配注入（由 PlayerController 在 Awake 调用；构造注入对 MonoBehaviour 不可用）。</summary>
        public void Initialize(
            PlayerLookController lookController, 
            PlayerMotorInputState motorInput,
            PlayerLookInputState lookInput)
        {
            Guard.NotNull(lookController, nameof(lookController));
            Guard.NotNull(motorInput, nameof(motorInput));
            Guard.NotNull(lookInput, nameof(lookInput));

            _lookController = lookController;
            _motorInput = motorInput;
            _lookInput = lookInput;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (cameraRig == null)
            {
                Log.Warning("[PlayerLook] 未配置 CameraRig（场景未装配相机合成器？），跳过视角更新。");
                return;
            }

            // 1. 输入增量 → 视角控制器（消费即清：增量输入不留存，防止静止时残留重复应用）
            _lookController.ApplyDelta(_lookInput.Delta);
            _lookInput.Delta = Vector2.zero;

            // 2. 基准旋转写入合成器（CameraRig.LateUpdate 同帧应用："鼠标移到哪指哪"零延迟语义不变）。
            //    相机 local 旋转（含 yaw）——相机世界 = body × local，站旋转平台上相机随 body 旋转。
            cameraRig.BaseRotation = Quaternion.Euler(_lookController.Pitch, _lookController.Yaw, 0f);

            // 3. 移动意图：MoveInput（x=左右, y=前后）经"相对 body 的视角 yaw"旋转到
            //    **body 局部空间**（参考系 = body）——消费方经 Context.WorldMoveIntent
            //    （body 局部 → 世界）使用；body 被平台旋转时视角与移动同步跟随参考系。
            var move = _motorInput.MoveInput;
            var localIntent = Quaternion.Euler(0f, _lookController.Yaw, 0f) * new Vector3(move.x, 0f, move.y);
            localIntent.y = 0f;
            _motorInput.LocalMoveIntent = localIntent.sqrMagnitude > 1e-6f ? localIntent.normalized : Vector3.zero;
        }
    }
}
