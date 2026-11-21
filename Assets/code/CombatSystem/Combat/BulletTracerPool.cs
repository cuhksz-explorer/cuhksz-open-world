using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    public class BulletTracerPool : MonoBehaviour
    {
        public static BulletTracerPool Instance { get; private set; }

        const int PoolSize = 24;
        readonly Queue<LineRenderer> _pool = new Queue<LineRenderer>();
        Material _material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("BulletTracerPool");
            DontDestroyOnLoad(go);
            go.AddComponent<BulletTracerPool>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _material = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < PoolSize; i++)
                _pool.Enqueue(CreateLine());
        }

        LineRenderer CreateLine()
        {
            var go = new GameObject("BulletTracer", typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.material = _material;
            lr.startWidth = 0.03f;
            lr.endWidth = 0.01f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.enabled = false;
            return lr;
        }

        public void Play(Vector3 start, Vector3 end, Color color)
        {
            if (_pool.Count == 0)
                return;
            var lr = _pool.Dequeue();
            lr.enabled = true;
            lr.startColor = lr.endColor = color;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            StartCoroutine(CoReturn(lr, 0.08f));
        }

        System.Collections.IEnumerator CoReturn(LineRenderer lr, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (lr != null)
            {
                lr.enabled = false;
                _pool.Enqueue(lr);
            }
        }
    }
}
