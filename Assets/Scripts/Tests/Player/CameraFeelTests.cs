using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;
using XeptKit.Core;

namespace XeptGame.Tests
{
    /// <summary>
    /// 相机观感层回归（3C_CameraFeel_Design.md）：`CameraRig` 合成与 FOV 平滑、`FovController` 状态映射
    /// （含**滑铲取冲刺档**）、`HeadBob`（含**滑铲抑制**）、`SlideTilt`（固定倾斜角）。
    /// 手法：效果源暴露 `Initialize`（注入**假 <see cref="ICameraEffectTarget"/>**）与 `Tick(dt, snapshot)`，
    /// `CameraRig` 暴露 `Initialize` / `ComposeFrame` —— EditMode 下无需 Play Mode 即可确定性驱动。
    /// </summary>
    public class CameraFeelTests
    {
        private readonly List<Object> _owned = new();
        private PlayerCameraFeelProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<PlayerCameraFeelProfile>();
            _owned.Add(_profile);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _owned)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _owned.Clear();
        }

        // ============================================================
        // 替身与装配辅助
        // ============================================================

        private sealed class FakeTarget : ICameraEffectTarget
        {
            public Vector3 Offset;
            public Quaternion Rotation = Quaternion.identity;
            public int PositionWrites;
            public int RotationWrites;

            public Vector3 PositionOffset => Offset;
            public Quaternion RotationOffset => Rotation;
            public OverrideValue<float> Fov { get; } = new();

            public void AddPositionOffset(Vector3 offset)
            {
                Offset += offset;
                PositionWrites++;
            }

            public void AddRotationOffset(Quaternion offset)
            {
                Rotation *= offset;
                RotationWrites++;
            }
        }

        private static FeelSnapshot Snapshot(bool sliding = false, bool sprinting = false, bool crouching = false,
            bool grounded = true, float horizontalSpeed = 4f)
            => new FeelSnapshot(horizontalSpeed, 0f, 1f, 1f, grounded, null, crouching, sprinting, sliding);

        private GameObject NewGo(string name)
        {
            var go = new GameObject(name);
            _owned.Add(go);
            return go;
        }

        private FovController NewFov(FakeTarget target)
        {
            var c = NewGo("Fov").AddComponent<FovController>();
            c.Initialize(target, _profile);
            return c;
        }

        private HeadBob NewBob(FakeTarget target)
        {
            var c = NewGo("Bob").AddComponent<HeadBob>();
            c.Initialize(target, _profile);
            return c;
        }

        private SlideTilt NewTilt(FakeTarget target)
        {
            var c = NewGo("Tilt").AddComponent<SlideTilt>();
            c.Initialize(target, _profile);
            return c;
        }

        private CameraRig NewRig(out Camera camera, Vector3 baseLocalPosition = default)
        {
            var rigGo = NewGo("Rig");
            var camGo = NewGo("Cam");
            camGo.transform.SetParent(rigGo.transform);
            camGo.transform.localPosition = baseLocalPosition;
            camera = camGo.AddComponent<Camera>();

            var rig = rigGo.AddComponent<CameraRig>();
            rig.Initialize(_profile, camGo.transform);
            return rig;
        }

        // ============================================================
        // CameraRig：合成与 FOV
        // ============================================================

        [Test]
        public void 合成_位置为眼位基准加偏移()
        {
            var rig = NewRig(out var camera, new Vector3(0f, 1.6f, 0f));
            rig.AddPositionOffset(new Vector3(0.1f, -0.2f, 0.05f));

            rig.ComposeFrame(0.02f);

            Assert.AreEqual(new Vector3(0.1f, 1.4f, 0.05f), camera.transform.localPosition);
        }

        [Test]
        public void 合成_旋转为基准乘修饰()
        {
            var rig = NewRig(out var camera);
            rig.BaseRotation = Quaternion.Euler(10f, 20f, 0f);
            rig.AddRotationOffset(Quaternion.Euler(0f, 0f, 3f));

            rig.ComposeFrame(0.02f);

            var expected = Quaternion.Euler(10f, 20f, 0f) * Quaternion.Euler(0f, 0f, 3f);
            Assert.That(Quaternion.Angle(camera.transform.localRotation, expected), Is.LessThan(0.01f));
        }

        [Test]
        public void 合成后清空偏移_供下一帧重新累积()
        {
            var rig = NewRig(out _);
            rig.AddPositionOffset(new Vector3(1f, 2f, 3f));
            rig.AddRotationOffset(Quaternion.Euler(0f, 0f, 5f));

            rig.ComposeFrame(0.02f);

            Assert.AreEqual(Vector3.zero, rig.PositionOffset);
            Assert.That(Quaternion.Angle(rig.RotationOffset, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void FOV_单帧不跳变_多帧收敛到目标()
        {
            var rig = NewRig(out var camera);
            Assert.AreEqual(_profile.fov.fovIdle, rig.CurrentFov, 1e-3f, "初始 = 基准 FOV");

            rig.Fov.Set(_profile.fov.fovSprint, OverridePriority.Effect);
            rig.ComposeFrame(0.02f);

            Assert.Greater(rig.CurrentFov, _profile.fov.fovIdle, "应朝目标移动");
            Assert.Less(rig.CurrentFov, _profile.fov.fovSprint, "单帧不应跳变到目标");

            for (int i = 0; i < 60; i++)
            {
                rig.ComposeFrame(0.02f);
            }

            Assert.AreEqual(_profile.fov.fovSprint, rig.CurrentFov, 0.05f);
            Assert.AreEqual(_profile.fov.fovSprint, camera.fieldOfView, 0.05f, "相机 FOV 应同步");
        }

        [Test]
        public void FOV_覆盖清除后回落默认基线()
        {
            var rig = NewRig(out _);
            rig.Fov.Set(_profile.fov.fovSprint, OverridePriority.Effect);
            for (int i = 0; i < 60; i++)
            {
                rig.ComposeFrame(0.02f);
            }

            rig.Fov.Clear(OverridePriority.Effect);
            for (int i = 0; i < 60; i++)
            {
                rig.ComposeFrame(0.02f);
            }

            Assert.AreEqual(_profile.fov.fovIdle, rig.CurrentFov, 0.05f);
        }

        // ============================================================
        // FovController：状态 → 目标（含滑铲取冲刺档）
        // ============================================================

        [Test]
        public void FovController_滑铲取冲刺档()
        {
            var target = new FakeTarget();
            var fov = NewFov(target);

            fov.Tick(0.02f, Snapshot(sliding: true, horizontalSpeed: 10f));

            Assert.AreEqual(_profile.fov.fovSprint, target.Fov.Value,
                "滑铲必须显式取冲刺档——滑铲时 IsCrouching/IsSprinting 均为 false，否则会错误回落到 fovIdle");
        }

        [Test]
        public void FovController_蹲伏优先于冲刺与滑铲()
        {
            var target = new FakeTarget();
            var fov = NewFov(target);

            fov.Tick(0.02f, Snapshot(crouching: true, sprinting: true, sliding: true));

            Assert.AreEqual(_profile.fov.fovCrouch, target.Fov.Value);
        }

        [Test]
        public void FovController_冲刺档与默认档()
        {
            var target = new FakeTarget();
            var fov = NewFov(target);

            fov.Tick(0.02f, Snapshot(sprinting: true));
            Assert.AreEqual(_profile.fov.fovSprint, target.Fov.Value);

            fov.Tick(0.02f, Snapshot());
            Assert.AreEqual(_profile.fov.fovIdle, target.Fov.Value);
        }

        [Test]
        public void FovController_销毁时清除覆盖()
        {
            var target = new FakeTarget();
            target.Fov.DefaultValue = _profile.fov.fovIdle; // 与 CameraRig.Initialize 一致
            var go = NewGo("Fov");
            var fov = go.AddComponent<FovController>();
            fov.Initialize(target, _profile);
            fov.Tick(0.02f, Snapshot(sprinting: true));
            Assert.AreEqual(_profile.fov.fovSprint, target.Fov.Value);

            // EditMode 下 AddComponent 创建的组件不走 Unity 生命周期（DestroyImmediate 不触发 OnDestroy），
            // 故直接调用 OnDestroy 验证"清除覆盖"逻辑本身
            typeof(FovController)
                .GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(fov, null);

            Assert.AreEqual(_profile.fov.fovIdle, target.Fov.Value,
                "销毁后应清除 Effect 覆盖，避免冻结目标 FOV");
        }

        // ============================================================
        // HeadBob：正常摆头 vs 滑铲抑制
        // ============================================================

        [Test]
        public void HeadBob_行走时产生摆头()
        {
            var target = new FakeTarget();
            var bob = NewBob(target);

            for (int i = 0; i < 30; i++)
            {
                bob.Tick(0.02f, Snapshot(horizontalSpeed: 4f));
            }

            Assert.Greater(target.PositionWrites, 0);
            Assert.Greater(target.Offset.magnitude, 0.001f, "行走应有摆头偏移");
        }

        [Test]
        public void HeadBob_速度低于阈值或空中不摆头()
        {
            var target = new FakeTarget();
            var bob = NewBob(target);

            for (int i = 0; i < 30; i++)
            {
                bob.Tick(0.02f, Snapshot(horizontalSpeed: 0.1f));
            }

            Assert.AreEqual(0, target.PositionWrites, "速度低于阈值（Idle）不应摆头");

            for (int i = 0; i < 30; i++)
            {
                bob.Tick(0.02f, Snapshot(grounded: false, horizontalSpeed: 8f));
            }

            Assert.AreEqual(0, target.PositionWrites, "空中不应摆头");
        }

        [Test]
        public void HeadBob_滑铲时抑制并回零()
        {
            var target = new FakeTarget();
            var bob = NewBob(target);

            for (int i = 0; i < 30; i++)
            {
                bob.Tick(0.02f, Snapshot(horizontalSpeed: 4f));
            }

            float before = target.Offset.magnitude;
            Assert.Greater(before, 0.001f);

            // 转入滑铲（速度更高，但应被抑制）——每帧新累积的偏移应逐帧衰减
            float previous = float.MaxValue;
            for (int i = 0; i < 30; i++)
            {
                target.Offset = Vector3.zero; // 模拟 CameraRig 每帧应用后清零
                bob.Tick(0.02f, Snapshot(sliding: true, horizontalSpeed: 10f));

                Assert.Less(target.Offset.magnitude, before, "滑铲中不应再产生摆头");
                previous = target.Offset.magnitude;
            }

            Assert.Less(previous, 0.001f, "惯性滑停后应回到零");
        }

        // ============================================================
        // SlideTilt：固定倾斜角
        // ============================================================

        [Test]
        public void SlideTilt_滑铲时倾斜到目标角_非滑铲回正()
        {
            var target = new FakeTarget();
            var tilt = NewTilt(target);

            for (int i = 0; i < 60; i++)
            {
                target.Rotation = Quaternion.identity; // 模拟 CameraRig 每帧应用后重置
                tilt.Tick(0.02f, Snapshot(sliding: true, horizontalSpeed: 10f));
            }

            float roll = target.Rotation.eulerAngles.z;
            if (roll > 180f)
            {
                roll -= 360f;
            }

            Assert.AreEqual(_profile.slideTilt.tiltAngle, roll, 0.2f, "滑铲应倾斜到固定角");
            Assert.Greater(roll, 0f, "v1 固定方向（正角）");

            for (int i = 0; i < 60; i++)
            {
                target.Rotation = Quaternion.identity;
                tilt.Tick(0.02f, Snapshot());
            }

            float finalRoll = target.Rotation.eulerAngles.z;
            if (finalRoll > 180f)
            {
                finalRoll -= 360f;
            }

            Assert.AreEqual(0f, finalRoll, 0.1f, "非滑铲应回正（SmoothDamp 渐近归零）");
        }

        [Test]
        public void SlideTilt_单帧不跳变()
        {
            var target = new FakeTarget();
            var tilt = NewTilt(target);

            tilt.Tick(0.02f, Snapshot(sliding: true));

            Assert.Greater(target.RotationWrites, 0, "滑铲首帧即应开始倾斜");
            float roll = target.Rotation.eulerAngles.z;
            Assert.Less(roll, _profile.slideTilt.tiltAngle, "过渡不应单帧跳变到目标角");
        }
    }
}
