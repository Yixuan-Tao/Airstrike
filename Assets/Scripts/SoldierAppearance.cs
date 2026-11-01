using UnityEngine;

namespace Airstrike
{
    public sealed class SoldierAppearance : MonoBehaviour
    {
        private SoldierController owner;
        private Transform leftLeg, rightLeg, leftArm, rightArm;
        private LineRenderer shield;
        private float phase;
        public float StrideDistance { get; private set; }

        public void Initialize(SoldierController soldier,Material[] palette)
        {
            owner=soldier;
            GameArt.Shape("Armored coat",PrimitiveType.Cube,transform,new Vector3(0,0.10f,0),new Vector3(0.48f,0.55f,0.42f),palette[3]);
            GameArt.Shape("Chest plate",PrimitiveType.Cube,transform,new Vector3(0.03f,0.16f,-0.23f),new Vector3(0.34f,0.30f,0.08f),palette[4]);
            GameArt.Shape("Belt",PrimitiveType.Cube,transform,new Vector3(0,-0.19f,-0.01f),new Vector3(0.52f,0.11f,0.45f),palette[7]);
            GameArt.Shape("Buckle",PrimitiveType.Cube,transform,new Vector3(0.06f,-0.19f,-0.25f),new Vector3(0.10f,0.09f,0.04f),palette[19]);
            GameArt.Shape("Face",PrimitiveType.Cube,transform,new Vector3(0.02f,0.52f,0),new Vector3(0.40f,0.32f,0.36f),palette[18]);
            GameArt.Shape("Helmet",PrimitiveType.Sphere,transform,new Vector3(-0.01f,0.70f,0),new Vector3(0.57f,0.28f,0.49f),palette[4]);
            GameArt.Shape("Helmet brim",PrimitiveType.Cube,transform,new Vector3(0.06f,0.63f,-0.02f),new Vector3(0.63f,0.08f,0.52f),palette[4]);
            GameArt.Shape("Goggles",PrimitiveType.Cube,transform,new Vector3(0.09f,0.55f,-0.20f),new Vector3(0.29f,0.10f,0.045f),palette[7]);
            GameArt.Shape("Nose",PrimitiveType.Cube,transform,new Vector3(0.25f,0.49f,-0.04f),new Vector3(0.09f,0.11f,0.16f),palette[18]);
            GameArt.Shape("Field pack",PrimitiveType.Cube,transform,new Vector3(-0.25f,0.05f,0.13f),new Vector3(0.29f,0.49f,0.38f),palette[4]);
            GameArt.Shape("Pack strap",PrimitiveType.Cube,transform,new Vector3(-0.18f,0.18f,-0.24f),new Vector3(0.07f,0.43f,0.05f),palette[7]);
            leftLeg=Leg("Left leg",-0.14f,palette); rightLeg=Leg("Right leg",0.14f,palette);
            leftArm=Arm("Left arm",-0.25f,0.03f,palette); rightArm=Arm("Right arm",0.24f,-0.28f,palette);
            shield=new GameObject("Respawn shield ring").AddComponent<LineRenderer>(); shield.transform.SetParent(transform,false);
            shield.useWorldSpace=false; shield.positionCount=24; shield.loop=true; shield.widthMultiplier=0.025f; shield.sharedMaterial=palette[8];
            for(var i=0;i<24;i++) { var a=i*Mathf.PI*2/24; shield.SetPosition(i,new Vector3(Mathf.Cos(a)*0.5f,-0.77f+Mathf.Sin(a)*0.10f,-0.35f)); }
        }

        private Transform Leg(string name,float x,Material[] palette)
        {
            var pivot=new GameObject(name).transform; pivot.SetParent(transform,false); pivot.localPosition=new Vector3(x,-0.23f,0);
            GameArt.Shape("Trouser",PrimitiveType.Cube,pivot,new Vector3(0,-0.17f,0),new Vector3(0.21f,0.34f,0.30f),palette[4]);
            GameArt.Shape("Knee guard",PrimitiveType.Cube,pivot,new Vector3(0,-0.30f,-0.18f),new Vector3(0.22f,0.14f,0.09f),palette[19]);
            GameArt.Shape("Boot",PrimitiveType.Cube,pivot,new Vector3(0.045f,-0.48f,0),new Vector3(0.29f,0.23f,0.38f),palette[7]);
            return pivot;
        }

        private Transform Arm(string name,float x,float z,Material[] palette)
        {
            var pivot=new GameObject(name).transform; pivot.SetParent(transform,false); pivot.localPosition=new Vector3(x,0.26f,z);
            GameArt.Shape("Sleeve",PrimitiveType.Cube,pivot,new Vector3(0.08f,-0.13f,0),new Vector3(0.19f,0.32f,0.22f),palette[3]);
            GameArt.Shape("Glove",PrimitiveType.Cube,pivot,new Vector3(0.20f,-0.24f,0),new Vector3(0.20f,0.15f,0.24f),palette[7]);
            return pivot;
        }

        private void LateUpdate()
        {
            if(owner==null) return;
            var velocity=owner.Body.linearVelocity.x;
            StrideDistance+=Mathf.Abs(velocity)*Time.deltaTime;
            phase+=Mathf.Abs(velocity)*Time.deltaTime*1.6f;
            var stride=owner.Grounded?Mathf.Sin(phase)*Mathf.Clamp01(Mathf.Abs(velocity)/4)*27:18;
            var facing=owner.AimDirection.x<0?-1:1;
            transform.localScale=new Vector3(facing,1,1);
            transform.localPosition=Vector3.up*(owner.Grounded?Mathf.Abs(Mathf.Sin(phase))*0.025f:0);
            transform.localRotation=Quaternion.Euler(0,0,owner.Health<=0?70:0);
            leftLeg.localRotation=Quaternion.Euler(0,0,stride); rightLeg.localRotation=Quaternion.Euler(0,0,-stride);
            var aim=Mathf.Atan2(owner.AimDirection.y,Mathf.Abs(owner.AimDirection.x))*Mathf.Rad2Deg;
            leftArm.localRotation=Quaternion.Euler(0,0,aim*0.4f); rightArm.localRotation=Quaternion.Euler(0,0,aim*0.6f+18);
            shield.enabled=owner.Protected && owner.Health>0;
        }
    }
}
