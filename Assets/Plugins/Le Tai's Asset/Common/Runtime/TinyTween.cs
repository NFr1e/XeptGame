// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeTai.Common
{
public class TinyTween : MonoBehaviour
{
    [Serializable]
    public struct Spring
    {
        public float stiffness;
        public float damping;
        public float approxDuration;
        public float overshoot;

        public static readonly Spring DEFAULT = DurationOvershoot(.5f, .1f);

        private Spring(float stiffness, float damping, float approxDuration, float overshoot)
        {
            this.stiffness      = stiffness;
            this.damping        = damping;
            this.approxDuration = approxDuration;
            this.overshoot      = overshoot;
        }

        public static Spring DurationOvershoot(float approxDuration, float overshoot)
        {
            approxDuration = Mathf.Max(0.001f, approxDuration);
            overshoot      = Mathf.Clamp(overshoot, 0f, 0.999f);

            float z;
            if (overshoot < 1e-3f)
            {
                z = 1f;
            }
            else
            {
                float lnOvershoot = Mathf.Log(overshoot);
                z = -lnOvershoot / Mathf.Sqrt(Mathf.PI * Mathf.PI + lnOvershoot * lnOvershoot);
            }

            float wn = 2f * Mathf.PI / approxDuration;
            wn = -Mathf.Log(2e-3f) / (z * approxDuration);

            float k = wn * wn;
            float c = 2f * z * wn;

            return new Spring(k, c, approxDuration, overshoot);
        }
    }

    private static class Ops<TValue>
    {
        public static readonly Func<TValue, TValue, TValue> ADD;
        public static readonly Func<TValue, TValue, TValue> SUB;
        public static readonly Func<TValue, float, TValue>  MUL;
        public static readonly Func<TValue, bool>           IS_NEAR_ZERO;

        static Ops()
        {
            const float threshold = 2e-3f;

            Ops<float>.ADD          = (a, b) => a + b;
            Ops<float>.SUB          = (a, b) => a - b;
            Ops<float>.MUL          = (a, s) => a * s;
            Ops<float>.IS_NEAR_ZERO = v => Mathf.Abs(v) < threshold;

            Ops<Vector2>.ADD          = (a, b) => a + b;
            Ops<Vector2>.SUB          = (a, b) => a - b;
            Ops<Vector2>.MUL          = (a, s) => a * s;
            Ops<Vector2>.IS_NEAR_ZERO = v => v.sqrMagnitude < (threshold * threshold);

            Ops<Vector3>.ADD          = (a, b) => a + b;
            Ops<Vector3>.SUB          = (a, b) => a - b;
            Ops<Vector3>.MUL          = (a, s) => a * s;
            Ops<Vector3>.IS_NEAR_ZERO = v => v.sqrMagnitude < (threshold * threshold);

            Ops<Color>.ADD          = (a, b) => a + b;
            Ops<Color>.SUB          = (a, b) => a - b;
            Ops<Color>.MUL          = (a, s) => a * s;
            Ops<Color>.IS_NEAR_ZERO = c => (c.r * c.r + c.g * c.g + c.b * c.b + c.a * c.a) < (threshold * threshold);
        }
    }

    private static class Ops
    {
        public static T    Add<T>(T        a, T     b) => Ops<T>.ADD(a, b);
        public static T    Sub<T>(T        a, T     b) => Ops<T>.SUB(a, b);
        public static T    Mul<T>(T        a, float s) => Ops<T>.MUL(a, s);
        public static bool IsNearZero<T>(T v) => Ops<T>.IS_NEAR_ZERO(v);
    }

    private abstract class Tween
    {
        protected Spring spring;

        public abstract bool MaybeRetarget(object newContext, Delegate newOnUpdate, object newTarget);
        public abstract bool Tick(float           dt);
        public abstract void Reset();
    }

    private static readonly Dictionary<Type, Stack<Tween>> TWEEN_POOLS = new(16);
    private static          TinyTween                      instance;

    private readonly List<Tween> _activeTweens = new();

    private class Tween<TCtx, TVal> : Tween where TCtx : class
    {
        private TCtx               _context;
        private Action<TCtx, TVal> _onUpdate;
        private TVal               _target;
        private TVal               _current;
        private TVal               _velocity;

        public void Setup(TCtx ctx, TVal from, TVal to, Spring spring, Action<TCtx, TVal> update)
        {
            _context  = ctx;
            _onUpdate = update;
            _target   = to;
            _current  = from;
            _velocity = default;

            this.spring = spring;
        }

        public override bool MaybeRetarget(object newContext, Delegate newOnUpdate, object newTarget)
        {
            if (newTarget is TVal val
             && ReferenceEquals(_context,  newContext)
             && ReferenceEquals(_onUpdate, newOnUpdate))
            {
                _target = val;
                return true;
            }
            return false;
        }

        public override bool Tick(float dt)
        {
            if (_context is UnityEngine.Object uc && !uc)
                return true;
            if (_context == null)
                return true;

            TVal force        = Ops.Mul(Ops.Sub(_target, _current), spring.stiffness);
            TVal dampingForce = Ops.Mul(_velocity,                  spring.damping);
            TVal acceleration = Ops.Sub(force, dampingForce);
            _velocity = Ops.Add(_velocity, Ops.Mul(acceleration, dt));
            _current  = Ops.Add(_current,  Ops.Mul(_velocity,    dt));

            var shouldStop = Ops.IsNearZero(Ops.Sub(_target, _current))
                          && Ops.IsNearZero(_velocity);

            if (shouldStop)
                _current = _target;

            try
            {
                _onUpdate?.Invoke(_context, _current);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return true;
            }

            return shouldStop;
        }

        public override void Reset()
        {
            _context  = null;
            _onUpdate = null;
        }
    }

    public static void Animate<TCtx, TVal>(TCtx context, TVal from, TVal to, Action<TCtx, TVal> onUpdate, Spring? spring = null) where TCtx : class
    {
        if (!instance)
        {
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            instance = new GameObject("[TinyTween]").AddComponent<TinyTween>();
            DontDestroyOnLoad(instance.gameObject);
        }

        for (int i = 0; i < instance._activeTweens.Count; i++)
        {
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            if (instance._activeTweens[i].MaybeRetarget(context, onUpdate, to))
                return;
        }

        Tween<TCtx, TVal> newTween  = null;
        Type              tweenType = typeof(Tween<TCtx, TVal>);
        if (TWEEN_POOLS.TryGetValue(tweenType, out var pool) && pool.Count > 0)
        {
            newTween = (Tween<TCtx, TVal>)pool.Pop();
        }

        newTween ??= new Tween<TCtx, TVal>();
        newTween.Setup(context, from, to, spring ?? Spring.DEFAULT, onUpdate);
        instance._activeTweens.Add(newTween);
    }

    private void Update()
    {
        if (_activeTweens.Count == 0) return;

        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            var tween = _activeTweens[i];
            if (tween.Tick(Time.deltaTime))
            {
                SwapAndPop(_activeTweens, i);
                tween.Reset();

                Type tweenType = tween.GetType();
                if (!TWEEN_POOLS.TryGetValue(tweenType, out var pool))
                {
                    pool                   = new Stack<Tween>();
                    TWEEN_POOLS[tweenType] = pool;
                }
                pool.Push(tween);
            }
        }
    }

    static void SwapAndPop<T>(List<T> list, int index)
    {
        int last = list.Count - 1;
        list[index] = list[last];
        list.RemoveAt(last);
    }

    public static void Move(RectTransform rt, Vector2 to, Spring? spring = null)
    {
        Animate(rt, rt.anchoredPosition, to, MOVE, spring);
    }

    static readonly Action<RectTransform, Vector3> MOVE = static (rt, pos) => rt.anchoredPosition = pos;
}
}
