// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LeTai.TrueShadow
{
public static class ObjectHandle
{
    public static ObjectHandle<T> Take<T>(T   obj) where T : Object => new ObjectHandle<T>(obj, true);
    public static ObjectHandle<T> Borrow<T>(T obj) where T : Object => new ObjectHandle<T>(obj, false);
}

public readonly struct ObjectHandle<T> : IDisposable
    where T : Object
{
    public readonly bool own;
    public readonly T    obj;

    internal ObjectHandle(T obj, bool own)
    {
        this.obj = obj;
        this.own = own;
    }

    public void Dispose()
    {
        if (own && obj != null)
        {
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }

    public static implicit operator T(ObjectHandle<T> handle) => handle.obj;
    // public static implicit operator bool(ObjectHandle<T> handle) => (bool)handle.obj;
}
}
