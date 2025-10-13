// Assets/_Project/Scripts/FX/SimplePool.cs
//
// A tiny, allocation-free object pool for GameObjects/Components.
// - Safe Enqueue/Dequeue
// - Prewarm support
// - Clear() for scene changes
//
// Use inside managers (like VFXManager_Pooled) to avoid Instantiate/Destroy spikes.
//
// MIT-like free-to-use.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchTheFruit
{
    public class SimplePool<T> where T : Component
    {
        readonly Queue<T> _q = new();
        readonly Func<T> _factory;
        readonly Transform _poolRoot;

        public int CountInactive => _q.Count;

        public SimplePool(Func<T> factory, Transform poolRoot = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _poolRoot = poolRoot;
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var t = CreateOne();
                Return(t);
            }
        }

        public T Get()
        {
            if (_q.Count > 0)
            {
                var t = _q.Dequeue();
                if (t) t.gameObject.SetActive(true);
                return t;
            }
            return CreateOne();
        }

        public void Return(T t)
        {
            if (!t) return;
            t.gameObject.SetActive(false);
            if (_poolRoot) t.transform.SetParent(_poolRoot, false);
            _q.Enqueue(t);
        }

        public void Clear()
        {
            while (_q.Count > 0)
            {
                var t = _q.Dequeue();
                if (t) UnityEngine.Object.Destroy(t.gameObject);
            }
        }

        T CreateOne()
        {
            var t = _factory();
            if (_poolRoot) t.transform.SetParent(_poolRoot, false);
            return t;
        }
    }
}
