using UnityEngine;
using System.Collections.Generic;

namespace CatchTheFruit
{
    public class FruitPool : MonoBehaviour
    {
        public static FruitPool Instance { get; private set; }

        [Header("Setup")]
        [SerializeField] private Fruit prefab;
        [Min(0)][SerializeField] private int prewarmCount = 24;
        [SerializeField] private Transform poolRoot; // optional

        private readonly Queue<Fruit> _pool = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (!poolRoot)
            {
                var go = new GameObject("~FruitPoolRoot");
                go.transform.SetParent(transform, false);
                poolRoot = go.transform;
            }

            Prewarm();
        }

        void Prewarm()
        {
            if (!prefab)
            {
                Debug.LogError("[FruitPool] Assign the Fruit prefab.", this);
                return;
            }
            for (int i = 0; i < prewarmCount; i++)
            {
                var f = Instantiate(prefab, poolRoot);
                f.gameObject.SetActive(false);
                _pool.Enqueue(f);
            }
        }

        public Fruit Spawn(Vector3 position, Quaternion rotation)
        {
            if (!prefab)
            {
                Debug.LogError("[FruitPool] Missing Fruit prefab.", this);
                return null;
            }

            var f = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(prefab);
            var t = f.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, rotation);
            f.gameObject.SetActive(true);
            return f;
        }

        public void Recycle(Fruit f)
        {
            if (!f) return;
            f.gameObject.SetActive(false);
            f.transform.SetParent(poolRoot, false);
            _pool.Enqueue(f);
        }

        public void Flush()
        {
            while (_pool.Count > 0) Destroy(_pool.Dequeue()?.gameObject);
        }
    }
}
