using UnityEngine;

namespace Airstrike
{
    public static class GameArt
    {
        public const int TerrainLayer = 8, EnemyLayer = 9, PlayerLayer = 10;

        public static GameObject Shape(string name, PrimitiveType primitive, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static void Aircraft(Transform root, Material hull, Material trim, Material dark)
        {
            Shape("Fuselage", PrimitiveType.Capsule, root, Vector3.zero,
                new Vector3(0.55f, 1.55f, 0.6f), hull).transform.localRotation = Quaternion.Euler(0, 0, 90);
            Shape("Wings", PrimitiveType.Cube, root, new Vector3(-0.1f, 0, 0), new Vector3(0.9f, 0.12f, 3.4f), hull);
            Shape("Tail wing", PrimitiveType.Cube, root, new Vector3(-1.15f, 0.15f, 0), new Vector3(0.5f, 0.12f, 1.5f), trim);
            Shape("Tail fin", PrimitiveType.Cube, root, new Vector3(-1.15f, 0.45f, 0), new Vector3(0.6f, 0.7f, 0.15f), trim);
            Shape("Canopy", PrimitiveType.Sphere, root, new Vector3(0.45f, 0.27f, -0.05f), new Vector3(0.8f, 0.42f, 0.52f), dark);
            Shape("Nose", PrimitiveType.Sphere, root, new Vector3(1.4f, 0, 0), new Vector3(0.35f, 0.45f, 0.45f), trim);
            Shape("Wing marking", PrimitiveType.Cube, root, new Vector3(-0.08f, 0.075f, -1.05f), new Vector3(0.65f, 0.02f, 0.25f), trim);
        }
    }

    public sealed class BlastVisual : MonoBehaviour
    {
        private float age;
        private float radius;
        private Transform core;
        private LineRenderer ring;

        public void Initialize(float size, Material glow)
        {
            radius = size;
            core = GameArt.Shape("Flash", PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.15f, glow).transform;
            ring = new GameObject("Shock ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 40;
            ring.sharedMaterial = glow;
            ring.widthMultiplier = 0.07f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = age / 0.42f;
            if (t >= 1) { Destroy(gameObject); return; }
            core.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 0.65f, Mathf.Sin(t * Mathf.PI));
            ring.widthMultiplier = (1 - t) * 0.09f;
            for (var i = 0; i < 40; i++)
            {
                var angle = i * Mathf.PI * 2 / 40;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -0.2f) * radius * t);
            }
        }
    }
}
