using System.Collections.Generic;
using UnityEngine;

namespace Airstrike
{
    public sealed class BattleEffects : MonoBehaviour
    {
        public const int Capacity=16;
        private readonly List<PooledBlast> pool=new List<PooledBlast>();
        private Material[] palette;
        private int reuse;
        private EmpPulse emp;
        public int PooledCount => pool.Count;
        public int ActiveCount { get { var count=0; foreach(var item in pool) if(item.gameObject.activeSelf) count++; return count; } }

        public void Initialize(Material[] materials) { palette=materials; }

        public void Pulse(Vector2 position)
        {
            if(emp==null)
            {
                emp=new GameObject("EMP pulse").AddComponent<EmpPulse>(); emp.transform.SetParent(transform,false);
                emp.Initialize(palette[8]);
            }
            emp.Begin(position);
        }

        public void Emit(Vector2 position,float radius)
        {
            PooledBlast effect=null;
            foreach(var item in pool) if(!item.gameObject.activeSelf) { effect=item; break; }
            if(effect==null && pool.Count<Capacity)
            {
                var go=new GameObject("Pooled battle effect"); go.transform.SetParent(transform,false);
                effect=go.AddComponent<PooledBlast>(); effect.Initialize(palette); pool.Add(effect);
            }
            if(effect==null) { effect=pool[reuse++%pool.Count]; }
            effect.Begin(position,radius);
        }
    }

    public sealed class EmpPulse : MonoBehaviour
    {
        private LineRenderer ring;
        private float age;
        public void Initialize(Material material)
        {
            ring=gameObject.AddComponent<LineRenderer>(); ring.sharedMaterial=material;
            ring.loop=true; ring.positionCount=64; ring.useWorldSpace=false;
            var tint=new MaterialPropertyBlock(); tint.SetColor("_BaseColor",new Color(0.3f,0.85f,1)); ring.SetPropertyBlock(tint);
        }
        public void Begin(Vector2 position)
        {
            transform.position=new Vector3(position.x,position.y,-0.6f); age=0;
            gameObject.SetActive(true); Pose();
        }
        private void Update()
        {
            age+=Time.deltaTime;
            if(age>=0.7f) { gameObject.SetActive(false); return; }
            Pose();
        }
        private void Pose()
        {
            var radius=Mathf.Lerp(0.5f,30,age/0.7f);
            ring.widthMultiplier=Mathf.Lerp(0.16f,0.015f,age/0.7f);
            for(var i=0;i<64;i++) { var a=i*Mathf.PI*2/64; ring.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0)); }
        }
    }

    public sealed class PooledBlast : MonoBehaviour
    {
        private Transform core;
        private Transform[] smoke, sparks;
        private LineRenderer ring;
        private float age, radius, lifetime;
        private bool large;

        public void Initialize(Material[] palette)
        {
            core=GameArt.Shape("Blast core",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one,palette[8]).transform;
            ring=new GameObject("Shockwave").AddComponent<LineRenderer>(); ring.transform.SetParent(transform,false);
            ring.useWorldSpace=false; ring.loop=true; ring.positionCount=32; ring.sharedMaterial=palette[8];
            smoke=new Transform[3]; sparks=new Transform[6];
            for(var i=0;i<smoke.Length;i++) smoke[i]=GameArt.Shape("Smoke puff",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one,palette[17]).transform;
            for(var i=0;i<sparks.Length;i++) sparks[i]=GameArt.Shape("Hot fragment",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one,palette[8]).transform;
        }

        public void Begin(Vector2 position,float size)
        {
            transform.position=new Vector3(position.x,position.y,-0.3f);
            radius=size; large=size>=1.2f; age=0; lifetime=large?1.15f:0.22f;
            gameObject.SetActive(true);
            foreach(var puff in smoke) puff.gameObject.SetActive(large);
            Pose();
        }

        private void Update()
        {
            age+=Time.deltaTime;
            if(age>=lifetime) { gameObject.SetActive(false); return; }
            Pose();
        }

        private void Pose()
        {
            var flash=Mathf.Clamp01(age/(large?0.35f:0.22f));
            core.gameObject.SetActive(flash<1);
            core.localScale=Vector3.one*Mathf.Max(0.01f,Mathf.Sin(flash*Mathf.PI)*radius*0.65f);
            ring.enabled=flash<1;
            ring.widthMultiplier=(1-flash)*(large?0.08f:0.035f);
            for(var i=0;i<32;i++)
            {
                var angle=i*Mathf.PI*2/32;
                ring.SetPosition(i,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),-0.08f)*radius*flash);
            }
            for(var i=0;i<sparks.Length;i++)
            {
                var t=age/(large?0.65f:0.22f);
                sparks[i].gameObject.SetActive(t<1);
                var angle=0.3f+i*0.49f;
                sparks[i].localPosition=new Vector3(Mathf.Cos(angle)*(i%2==0?1:-1),Mathf.Sin(angle),-0.15f)*radius*t*0.9f+Vector3.down*t*t*0.9f;
                sparks[i].localScale=new Vector3(0.05f,0.14f,0.05f)*(1-Mathf.Clamp01(t));
                sparks[i].localRotation=Quaternion.Euler(0,0,i*40+age*180);
            }
            if(!large) return;
            var progress=age/lifetime;
            for(var i=0;i<smoke.Length;i++)
            {
                var offset=(i-1)*0.22f*radius*Mathf.Clamp01(progress*2);
                smoke[i].localPosition=new Vector3(offset,0.1f+progress*radius*(0.55f+i*0.18f),-0.06f-i*0.02f);
                var size=radius*(0.2f+progress*0.35f)*Mathf.Clamp01((1-progress)*3)*(0.7f+i*0.15f);
                smoke[i].localScale=new Vector3(size,size*0.8f,size*0.45f);
            }
        }
    }
}
