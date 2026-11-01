using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airstrike
{
    // A 2D occupancy grid extruded into a 3D cross-section. Rebuild only touched chunks.
    public sealed class DestructibleGround : MonoBehaviour
    {
        public const int Columns = 128, Rows = 18;
        public const float Cell = 0.5f, Left = -32, Bottom = -8;
        private const int ChunkWidth = 8;
        private readonly bool[,] solid = new bool[Columns, Rows];
        private readonly GameObject[] chunks = new GameObject[Columns / ChunkWidth];
        private Material[] materials;
        public int SolidCount { get; private set; }
        public int RemovedCount { get; private set; }

        public void Initialize(Material soil, Material grass, Material deep)
        {
            materials = new[] { soil, grass, deep };
            for (var x = 0; x < Columns; x++)
            {
                var worldX = Left + (x + 0.5f) * Cell;
                var surface = 0.35f + Mathf.Sin(worldX * 0.18f) * 0.33f + Mathf.Sin(worldX * 0.47f) * 0.14f;
                for (var y = 0; y < Rows; y++)
                    if (Bottom + (y + 0.5f) * Cell < surface) { solid[x, y] = true; SolidCount++; }
            }
            for (var c = 0; c < chunks.Length; c++)
            {
                chunks[c] = new GameObject("Ground chunk " + c);
                chunks[c].transform.SetParent(transform, false);
                chunks[c].layer = GameArt.TerrainLayer;
                chunks[c].AddComponent<MeshFilter>();
                chunks[c].AddComponent<MeshRenderer>().sharedMaterials = materials;
                Rebuild(c);
            }
        }

        public float SurfaceAt(float worldX)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt((worldX - Left) / Cell), 0, Columns - 1);
            for (var y = Rows - 1; y >= 0; y--) if (solid[x, y]) return Bottom + (y + 1) * Cell;
            return Bottom + Cell;
        }

        public bool IsSolidAt(Vector2 point)
        {
            var x = Mathf.FloorToInt((point.x - Left) / Cell);
            var y = Mathf.FloorToInt((point.y - Bottom) / Cell);
            return Filled(x, y);
        }

        // Sample the authoritative occupancy grid, so disabled/deferred colliders
        // and the decorative depth of the mesh cannot change blast protection.
        public float BlastTransmission(Vector2 origin, Vector2 target)
        {
            var delta = target - origin;
            var length = delta.magnitude;
            if (length < 0.16f) return 1;
            var direction = delta / length;
            const float step = Cell / 10;
            var thickness = 0f;
            for (var distance = 0.08f; distance < length - 0.08f; distance += step)
            {
                var segment = Mathf.Min(step, length - 0.08f - distance);
                if (IsSolidAt(origin + direction * (distance + segment * 0.5f))) thickness += segment;
                if (thickness >= 0.75f) return 0;
            }
            return Mathf.Clamp01(1 - thickness / 0.75f);
        }

        public int Carve(Vector2 center, float radius)
        {
            var minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - Left) / Cell), 0, Columns - 1);
            var maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + radius - Left) / Cell), 0, Columns - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt((center.y - radius - Bottom) / Cell), 1, Rows - 1);
            var maxY = Mathf.Clamp(Mathf.FloorToInt((center.y + radius - Bottom) / Cell), 1, Rows - 1);
            var changed = new HashSet<int>();
            var removed = 0;
            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                {
                    var p = new Vector2(Left + (x + 0.5f) * Cell, Bottom + (y + 0.5f) * Cell);
                    if (!solid[x, y] || (p - center).sqrMagnitude > radius * radius) continue;
                    solid[x, y] = false;
                    removed++;
                    changed.Add(x / ChunkWidth);
                    if (x > 0) changed.Add((x - 1) / ChunkWidth);
                    if (x + 1 < Columns) changed.Add((x + 1) / ChunkWidth);
                }
            SolidCount -= removed;
            RemovedCount += removed;
            foreach (var c in changed) Rebuild(c);
            if (removed > 0) Physics.SyncTransforms();
            return removed;
        }

        private bool Filled(int x, int y) => x >= 0 && x < Columns && y >= 0 && y < Rows && solid[x, y];

        private void Rebuild(int c)
        {
            var root = chunks[c];
            foreach (var collider in root.GetComponents<BoxCollider>()) { collider.enabled = false; Destroy(collider); }
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            var first = c * ChunkWidth;
            for (var x = first; x < first + ChunkWidth; x++)
            {
                var start = -1;
                for (var y = 0; y <= Rows; y++)
                {
                    if (y < Rows && solid[x, y]) { if (start < 0) start = y; }
                    else if (start >= 0)
                    {
                        var box = root.AddComponent<BoxCollider>();
                        box.center = new Vector3(Left + (x + 0.5f) * Cell, Bottom + (start + y) * Cell * 0.5f, 0);
                        box.size = new Vector3(Cell, (y - start) * Cell, 3);
                        start = -1;
                    }
                    if (y >= Rows || !solid[x, y]) continue;
                    var l = Left + x * Cell; var r = l + Cell;
                    var b = Bottom + y * Cell; var t = b + Cell;
                    var mat = y < 7 ? 2 : 0;
                    Quad(vertices, triangles[mat], new Vector3(l,b,-1.5f), new Vector3(l,t,-1.5f), new Vector3(r,t,-1.5f), new Vector3(r,b,-1.5f));
                    Quad(vertices, triangles[mat], new Vector3(r,b,1.5f), new Vector3(r,t,1.5f), new Vector3(l,t,1.5f), new Vector3(l,b,1.5f));
                    if (!Filled(x,y+1)) Quad(vertices, triangles[1], new Vector3(l,t,-1.5f), new Vector3(l,t,1.5f), new Vector3(r,t,1.5f), new Vector3(r,t,-1.5f));
                    if (!Filled(x,y-1)) Quad(vertices, triangles[mat], new Vector3(l,b,1.5f), new Vector3(l,b,-1.5f), new Vector3(r,b,-1.5f), new Vector3(r,b,1.5f));
                    if (!Filled(x-1,y)) Quad(vertices, triangles[mat], new Vector3(l,b,1.5f), new Vector3(l,t,1.5f), new Vector3(l,t,-1.5f), new Vector3(l,b,-1.5f));
                    if (!Filled(x+1,y)) Quad(vertices, triangles[mat], new Vector3(r,b,-1.5f), new Vector3(r,t,-1.5f), new Vector3(r,t,1.5f), new Vector3(r,b,1.5f));
                }
            }
            var mesh = new Mesh { name = "Ground mesh " + c, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            for (var i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var filter = root.GetComponent<MeshFilter>();
            if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
            filter.sharedMesh = mesh;
        }

        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(i); t.Add(i+1); t.Add(i+2); t.Add(i); t.Add(i+2); t.Add(i+3);
        }

        private void OnDestroy()
        {
            foreach (var chunk in chunks)
                if (chunk != null && chunk.GetComponent<MeshFilter>().sharedMesh != null)
                    Destroy(chunk.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
