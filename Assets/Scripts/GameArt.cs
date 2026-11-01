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

        public static void FastJet(Transform root,Material hull,Material trim,Material dark)
        {
            Shape("Jet fuselage",PrimitiveType.Capsule,root,Vector3.zero,new Vector3(0.42f,1.7f,0.48f),hull).transform.localRotation=Quaternion.Euler(0,0,90);
            Shape("Swept wing",PrimitiveType.Cube,root,new Vector3(-0.35f,0,0),new Vector3(0.8f,0.12f,3.3f),hull).transform.localRotation=Quaternion.Euler(0,25,0);
            Shape("Jet tail",PrimitiveType.Cube,root,new Vector3(-1.2f,0.42f,0),new Vector3(0.7f,0.7f,0.13f),trim).transform.localRotation=Quaternion.Euler(0,0,20);
            Shape("Jet canopy",PrimitiveType.Sphere,root,new Vector3(0.5f,0.23f,-0.05f),new Vector3(0.9f,0.3f,0.42f),dark);
            Shape("Jet intake",PrimitiveType.Cube,root,new Vector3(-0.75f,-0.15f,-0.29f),new Vector3(0.6f,0.14f,0.12f),dark);
            Shape("Jet nose",PrimitiveType.Sphere,root,new Vector3(1.6f,0,0),new Vector3(0.35f,0.25f,0.3f),trim);
        }

        public static Transform Helicopter(Transform root,Material hull,Material trim,Material dark)
        {
            Shape("Helicopter cabin",PrimitiveType.Sphere,root,new Vector3(0.2f,0,0),new Vector3(2,0.95f,0.85f),hull);
            Shape("Helicopter glass",PrimitiveType.Sphere,root,new Vector3(0.85f,0.15f,-0.16f),new Vector3(0.7f,0.6f,0.68f),dark);
            Shape("Tail boom",PrimitiveType.Cube,root,new Vector3(-1.3f,0.1f,0),new Vector3(1.7f,0.2f,0.22f),hull);
            Shape("Tail fin",PrimitiveType.Cube,root,new Vector3(-2,0.4f,0),new Vector3(0.3f,0.8f,0.16f),trim);
            Shape("Skid front",PrimitiveType.Cube,root,new Vector3(0,-0.66f,-0.45f),new Vector3(1.8f,0.1f,0.1f),dark);
            Shape("Skid back",PrimitiveType.Cube,root,new Vector3(0,-0.66f,0.45f),new Vector3(1.8f,0.1f,0.1f),dark);
            Shape("Rocket pod",PrimitiveType.Cube,root,new Vector3(0.2f,-0.38f,-0.55f),new Vector3(0.9f,0.24f,0.25f),trim);
            var rotor=new GameObject("Main rotor").transform;
            rotor.SetParent(root,false); rotor.localPosition=new Vector3(0,0.75f,0);
            Shape("Rotor X",PrimitiveType.Cube,rotor,Vector3.zero,new Vector3(4.5f,0.05f,0.14f),dark);
            Shape("Rotor Z",PrimitiveType.Cube,rotor,Vector3.zero,new Vector3(0.14f,0.05f,4.5f),dark);
            Shape("Rotor hub",PrimitiveType.Cylinder,root,new Vector3(0,0.58f,0),new Vector3(0.16f,0.18f,0.16f),trim);
            return rotor;
        }
    }
}
