using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airstrike
{
    public sealed class BattleBackdrop : MonoBehaviour
    {
        private readonly List<Mesh> ownedMeshes=new List<Mesh>();
        private readonly List<Transform> layers=new List<Transform>();
        private readonly List<float> parallax=new List<float>();
        private Transform[] clouds;
        private Camera cameraTarget;

        public void Initialize(Material[] palette,Camera camera)
        {
            cameraTarget=camera;
            var skyVertices=new List<Vector3>(); var skyColors=new List<Color>(); var skyTriangles=new List<int>();
            var heights=new[] {-30f,-10f,6f,25f,50f};
            var colors=new[] {new Color(0.62f,0.62f,0.48f),new Color(0.47f,0.58f,0.56f),
                new Color(0.16f,0.29f,0.37f),new Color(0.055f,0.12f,0.22f),new Color(0.035f,0.07f,0.15f)};
            for(var i=0;i<heights.Length;i++)
            {
                skyVertices.Add(new Vector3(-140,heights[i],70)); skyVertices.Add(new Vector3(140,heights[i],70));
                skyColors.Add(colors[i].linear); skyColors.Add(colors[i].linear);
                if(i==0) continue;
                var n=(i-1)*2;
                skyTriangles.AddRange(new[] {n,n+2,n+3,n,n+3,n+1});
            }
            var sky=MeshObject("Gradient sky",skyVertices,skyTriangles,palette[10]);
            sky.GetComponent<MeshFilter>().sharedMesh.SetColors(skyColors);
            Ridge("Far mountains",42,1.6f,1.7f,0.8f,palette[11],0.78f);
            Ridge("Middle mountains",25,0.2f,1.9f,2.3f,palette[12],0.48f);
            Ridge("Near foothills",14,-1.7f,1.1f,4.1f,palette[13],0.18f);
            var sunRoot=Layer("Distant sun",0.85f);
            GameArt.Shape("Sun disk",PrimitiveType.Sphere,sunRoot,new Vector3(-18,2.8f,60),new Vector3(3.8f,3.8f,0.2f),palette[9]);
            var industrial=Layer("Industrial silhouettes",0.08f);
            for(var i=0;i<9;i++)
            {
                var pylon=new GameObject("Power pylon").transform; pylon.SetParent(industrial,false);
                pylon.localPosition=new Vector3(-46+i*12,-0.8f,8);
                GameArt.Shape("Pylon L",PrimitiveType.Cube,pylon,new Vector3(-0.28f,1.4f,0),new Vector3(0.09f,3.2f,0.12f),palette[13]).transform.localRotation=Quaternion.Euler(0,0,-9);
                GameArt.Shape("Pylon R",PrimitiveType.Cube,pylon,new Vector3(0.28f,1.4f,0),new Vector3(0.09f,3.2f,0.12f),palette[13]).transform.localRotation=Quaternion.Euler(0,0,9);
                GameArt.Shape("Power crossarm",PrimitiveType.Cube,pylon,new Vector3(0,2.5f,0),new Vector3(1.8f,0.1f,0.15f),palette[13]);
                var wire=new GameObject("Power cable").AddComponent<LineRenderer>(); wire.transform.SetParent(pylon,false);
                wire.useWorldSpace=false; wire.positionCount=16; wire.sharedMaterial=palette[13]; wire.widthMultiplier=0.025f;
                for(var j=0;j<16;j++) { var t=j/15f; wire.SetPosition(j,new Vector3(t*12,2.5f-Mathf.Sin(t*Mathf.PI)*0.65f,0)); }
            }
            var cloudLayer=Layer("Cloud layer",0.88f);
            clouds=new Transform[8];
            for(var i=0;i<clouds.Length;i++)
            {
                var vertices=new List<Vector3> {Vector3.zero}; var triangles=new List<int>();
                for(var j=0;j<14;j++)
                {
                    var angle=j*Mathf.PI*2/14;
                    vertices.Add(new Vector3(Mathf.Cos(angle)*(3.1f+0.4f*Mathf.Sin(j*2.1f)),Mathf.Sin(angle)*(0.32f+0.12f*Mathf.Cos(j*1.7f)),0));
                }
                for(var j=0;j<14;j++) triangles.AddRange(new[] {0,1+(j+1)%14,1+j});
                var cloud=MeshObject("Faceted cloud "+i,vertices,triangles,palette[14]);
                cloud.transform.SetParent(cloudLayer,false); cloud.transform.localPosition=new Vector3(-62+i*18,3.2f+i%3*1.5f,40);
                cloud.transform.localScale=new Vector3(1+(i%3)*0.3f,1,1);
                clouds[i]=cloud.transform;
            }
        }

        private Transform Layer(string name,float follow)
        {
            var layer=new GameObject(name).transform; layer.SetParent(transform,false);
            layers.Add(layer); parallax.Add(follow); return layer;
        }

        private void Ridge(string name,float z,float baseHeight,float amplitude,float phase,Material material,float follow)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(var i=0;i<=60;i++)
            {
                var x=-120+i*4;
                var top=baseHeight+Mathf.Sin(x*0.075f+phase)*amplitude+Mathf.Sin(x*0.18f-phase)*amplitude*0.4f;
                vertices.Add(new Vector3(x,-20,z)); vertices.Add(new Vector3(x,top,z));
                if(i==0) continue;
                var n=(i-1)*2; triangles.AddRange(new[] {n,n+1,n+3,n,n+3,n+2});
            }
            var ridge=MeshObject(name,vertices,triangles,material);
            layers.Add(ridge.transform); parallax.Add(follow);
        }

        private GameObject MeshObject(string name,List<Vector3> vertices,List<int> triangles,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(transform,false);
            var mesh=new Mesh {name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ownedMeshes.Add(mesh); go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.receiveShadows=false; renderer.shadowCastingMode=ShadowCastingMode.Off;
            return go;
        }

        private void LateUpdate()
        {
            if(cameraTarget==null) return;
            for(var i=0;i<layers.Count;i++) layers[i].localPosition=Vector3.right*cameraTarget.transform.position.x*parallax[i];
            if(clouds==null) return;
            foreach(var cloud in clouds)
            {
                var p=cloud.localPosition; p.x+=Time.deltaTime*0.12f;
                if(p.x>85) p.x=-85;
                cloud.localPosition=p;
            }
        }

        private void OnDestroy() { foreach(var mesh in ownedMeshes) if(mesh!=null) Destroy(mesh); }
    }
}
