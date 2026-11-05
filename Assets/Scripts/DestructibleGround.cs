using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airstrike
{
    [System.Serializable]
    public sealed class TerrainWindow
    {
        public float centerX, width, retainedFraction, minimumColumnSoil, centerColumnSoil, longestWalkableRun;
        public int initialCells, remainingCells, standableColumns;
    }

    // A 2D occupancy grid extruded into a 3D cross-section. Rebuild only touched chunks.
    public sealed class DestructibleGround : MonoBehaviour
    {
        private const float Left=-32, Width=64;
        private const int ChunkWidth = 8;
        private BalanceSettings rules;
        private int Columns, Rows;
        private float Cell => rules.TerrainCell;
        private float Bottom => rules.TerrainBottom;
        private bool[,] solid, wreck;
        private int[] originalTop;
        private GameObject[] chunks;
        private Material[] materials;
        public int SolidCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int WreckCells { get; private set; }

        public void Initialize(BalanceSettings settings,Material soil,Material grass,Material deep,Material band,Material sand,Material metal,Material trim)
        {
            rules=settings;
            Columns=Mathf.CeilToInt(Width/Cell);
            var ceiling=Mathf.Max(2,rules.TerrainMean+rules.TerrainHill+rules.TerrainDetail+Cell);
            Rows=Mathf.Max(rules.BedrockRows+1,Mathf.CeilToInt((ceiling-Bottom)/Cell));
            solid=new bool[Columns,Rows];
            wreck=new bool[Columns,Rows];
            originalTop=new int[Columns];
            chunks=new GameObject[(Columns+ChunkWidth-1)/ChunkWidth];
            materials = new[] { soil, grass, deep, band, sand, metal, trim };
            for (var x = 0; x < Columns; x++)
            {
                var worldX = Left + (x + 0.5f) * Cell;
                var surface=rules.TerrainMean+Mathf.Sin(worldX*0.18f)*rules.TerrainHill+Mathf.Sin(worldX*0.47f)*rules.TerrainDetail;
                originalTop[x]=-1;
                for (var y = 0; y < Rows; y++)
                    if (y<rules.BedrockRows || Bottom+(y+0.5f)*Cell<surface) { solid[x,y]=true; SolidCount++; originalTop[x]=y; }
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
            return Bottom;
        }

        public bool IsSolidAt(Vector2 point)
        {
            var x = Mathf.FloorToInt((point.x - Left) / Cell);
            var y = Mathf.FloorToInt((point.y - Bottom) / Cell);
            return Filled(x, y);
        }

        public TerrainWindow InspectWindow(float centerX,float width=14)
        {
            var result=new TerrainWindow {centerX=centerX,width=width,minimumColumnSoil=float.PositiveInfinity};
            var first=Mathf.Clamp(Mathf.FloorToInt((centerX-width*0.5f-Left)/Cell),0,Columns-1);
            var last=Mathf.Clamp(Mathf.CeilToInt((centerX+width*0.5f-Left)/Cell)-1,0,Columns-1);
            var center=Mathf.Clamp(Mathf.FloorToInt((centerX-Left)/Cell),first,last);
            var run=0; var previousFeet=0f;
            for(var x=first;x<=last;x++)
            {
                var remaining=0;
                result.initialCells+=Mathf.Max(0,originalTop[x]+1-rules.BedrockRows);
                for(var y=rules.BedrockRows;y<=originalTop[x];y++) if(solid[x,y] && !wreck[x,y]) remaining++;
                result.remainingCells+=remaining;
                result.minimumColumnSoil=Mathf.Min(result.minimumColumnSoil,remaining*Cell);
                if(x==center) result.centerColumnSoil=remaining*Cell;
                var feet=SurfaceAt(Left+(x+0.5f)*Cell);
                // Conservative standing clearance; this is not a navigation proof.
                var clear=true;
                for(var neighbour=Mathf.Max(0,x-1);neighbour<=Mathf.Min(Columns-1,x+1);neighbour++)
                    for(var y=0;y<Rows;y++)
                        if(solid[neighbour,y] && Bottom+y*Cell<feet+1.65f && Bottom+(y+1)*Cell>feet+0.01f) clear=false;
                if(clear)
                {
                    result.standableColumns++;
                    run=run>0 && Mathf.Abs(feet-previousFeet)<=0.52f?run+1:1;
                    result.longestWalkableRun=Mathf.Max(result.longestWalkableRun,run*Cell);
                    previousFeet=feet;
                }
                else run=0;
            }
            result.retainedFraction=result.initialCells>0?result.remainingCells/(float)result.initialCells:1;
            return result;
        }

        // Sample the authoritative occupancy grid, so disabled/deferred colliders
        // and the decorative depth of the mesh cannot change blast protection.
        public float BlastTransmission(Vector2 origin, Vector2 target)
        {
            var delta = target - origin;
            var length = delta.magnitude;
            if (length < 0.16f || rules.CoverDepth<=0) return 1;
            var direction = delta / length;
            var step = Cell / 10;
            var thickness = 0f;
            for (var distance = 0.08f; distance < length - 0.08f; distance += step)
            {
                var segment = Mathf.Min(step, length - 0.08f - distance);
                if (IsSolidAt(origin + direction * (distance + segment * 0.5f))) thickness += segment;
                if (thickness >= rules.CoverDepth) return 0;
            }
            return Mathf.Clamp01(1 - thickness / rules.CoverDepth);
        }

        // Blast damage/reach and excavated soil are separate: ordinary ordnance
        // makes a shallow crater instead of consuming several layers at once.
        public int CarveBlast(Vector2 center, bool playerRocket)
        {
            return CarveEllipse(center,playerRocket?rules.RocketCraterX:rules.BombCraterX,
                playerRocket?rules.RocketCraterY:rules.BombCraterY);
        }

        public int Carve(Vector2 center, float radius) => CarveEllipse(center, radius, radius);

        public bool TryPlaceWreck(Vector2 impact,Vector2 player)
        {
            var width=rules.WreckWidthCells;
            if(WreckCells+width>rules.WreckCellCap) return false;
            var first=Mathf.FloorToInt((impact.x-Left)/Cell)-(width-1)/2;
            if(first<0 || first+width>Columns) return false;
            var row=Mathf.RoundToInt((impact.y-Bottom)/Cell);
            if(row<=0 || row>=Rows || Mathf.Abs(impact.y-(Bottom+row*Cell))>0.05f) return false;
            var center=Left+(first+width*0.5f)*Cell;
            if(Mathf.Abs(center-AirstrikeGame.RespawnX)<2.5f) return false;
            if(Mathf.Abs(center-player.x)<1.75f && Mathf.Abs(impact.y+Cell*0.5f-player.y)<1.4f) return false;
            for(var x=first;x<first+width;x++)
            {
                // A single step on a level, supported surface. Never bridge a trench or stack metal.
                if(!solid[x,row-1] || wreck[x,row-1] || SurfaceAt(Left+(x+0.5f)*Cell)!=Bottom+row*Cell) return false;
            }
            var changed=new HashSet<int>();
            for(var x=first;x<first+width;x++)
            {
                solid[x,row]=true;
                wreck[x,row]=true;
                MarkChanged(changed,x);
            }
            WreckCells+=width;
            SolidCount+=width;
            foreach(var chunk in changed) Rebuild(chunk);
            Physics.SyncTransforms();
            return true;
        }

        private int CarveEllipse(Vector2 center, float horizontalRadius, float verticalRadius)
        {
            if (horizontalRadius <= 0 || verticalRadius <= 0) return 0;
            var minX = Mathf.Clamp(Mathf.FloorToInt((center.x - horizontalRadius - Left) / Cell), 0, Columns - 1);
            var maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + horizontalRadius - Left) / Cell), 0, Columns - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt((center.y - verticalRadius - Bottom) / Cell), rules.BedrockRows, Rows - 1);
            var maxY = Mathf.Clamp(Mathf.FloorToInt((center.y + verticalRadius - Bottom) / Cell), rules.BedrockRows, Rows - 1);
            var changed = new HashSet<int>();
            var removed = 0;
            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                {
                    var p = new Vector2(Left + (x + 0.5f) * Cell, Bottom + (y + 0.5f) * Cell);
                    var dx = (p.x - center.x) / horizontalRadius;
                    var dy = (p.y - center.y) / verticalRadius;
                    if (!solid[x, y] || dx * dx + dy * dy > 1) continue;
                    RemoveCell(x,y);
                    removed++;
                    if(y+1<Rows && wreck[x,y+1]) { RemoveCell(x,y+1); removed++; }
                    MarkChanged(changed,x);
                }
            SolidCount -= removed;
            RemovedCount += removed;
            foreach (var c in changed) Rebuild(c);
            if (removed > 0) Physics.SyncTransforms();
            return removed;
        }

        private void RemoveCell(int x,int y)
        {
            solid[x,y]=false;
            if(wreck[x,y]) { wreck[x,y]=false; WreckCells--; }
        }

        private void MarkChanged(HashSet<int> changed,int x)
        {
            changed.Add(x/ChunkWidth);
            if(x>0) changed.Add((x-1)/ChunkWidth);
            if(x+1<Columns) changed.Add((x+1)/ChunkWidth);
        }

        private bool Filled(int x, int y) => x >= 0 && x < Columns && y >= 0 && y < Rows && solid[x, y];

        private void Rebuild(int c)
        {
            var root = chunks[c];
            foreach (var collider in root.GetComponents<BoxCollider>()) { collider.enabled = false; Destroy(collider); }
            var vertices = new List<Vector3>();
            var triangles = new List<int>[materials.Length];
            for(var i=0;i<triangles.Length;i++) triangles[i]=new List<int>();
            var first = c * ChunkWidth;
            for (var x = first; x < Mathf.Min(Columns,first+ChunkWidth); x++)
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
                    var depth=originalTop[x]-y;
                    var grass = y==originalTop[x] && !wreck[x,y];
                    var mat = wreck[x,y]?5:y<4?2:depth==3 || depth==8?4:depth>6?3:0;
                    Quad(vertices, triangles[mat], new Vector3(l,b,-1.5f), new Vector3(l,t,-1.5f), new Vector3(r,t,-1.5f), new Vector3(r,b,-1.5f));
                    if(wreck[x,y])
                        Quad(vertices,triangles[6],new Vector3(l+0.16f*Cell,b+0.24f*Cell,-1.51f),new Vector3(l+0.34f*Cell,t-0.24f*Cell,-1.51f),
                            new Vector3(r-0.14f*Cell,t-0.24f*Cell,-1.51f),new Vector3(r-0.30f*Cell,b+0.24f*Cell,-1.51f));
                    if(grass)
                        Quad(vertices,triangles[1],new Vector3(l,t-0.09f,-1.51f),new Vector3(l,t,-1.51f),new Vector3(r,t,-1.51f),new Vector3(r,t-0.09f,-1.51f));
                    Quad(vertices, triangles[mat], new Vector3(r,b,1.5f), new Vector3(r,t,1.5f), new Vector3(l,t,1.5f), new Vector3(l,b,1.5f));
                    if (!Filled(x,y+1)) Quad(vertices, triangles[grass?1:mat], new Vector3(l,t,-1.5f), new Vector3(l,t,1.5f), new Vector3(r,t,1.5f), new Vector3(r,t,-1.5f));
                    if (!Filled(x,y-1)) Quad(vertices, triangles[mat], new Vector3(l,b,1.5f), new Vector3(l,b,-1.5f), new Vector3(r,b,-1.5f), new Vector3(r,b,1.5f));
                    if (!Filled(x-1,y)) Quad(vertices, triangles[mat], new Vector3(l,b,1.5f), new Vector3(l,t,1.5f), new Vector3(l,t,-1.5f), new Vector3(l,b,-1.5f));
                    if (!Filled(x+1,y)) Quad(vertices, triangles[mat], new Vector3(r,b,-1.5f), new Vector3(r,t,-1.5f), new Vector3(r,t,1.5f), new Vector3(r,b,1.5f));
                }
            }
            var mesh = new Mesh { name = "Ground mesh " + c, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (var i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
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
            if(chunks==null) return;
            foreach (var chunk in chunks)
                if (chunk != null && chunk.GetComponent<MeshFilter>().sharedMesh != null)
                    Destroy(chunk.GetComponent<MeshFilter>().sharedMesh);
        }
    }
}
