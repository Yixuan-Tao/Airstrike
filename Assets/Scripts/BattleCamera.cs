using UnityEngine;

namespace Airstrike
{
    public sealed class BattleCamera : MonoBehaviour
    {
        public Transform Target;
        private float shake;
        public void Shake(float strength) { shake = Mathf.Max(shake,strength); }
        private void LateUpdate()
        {
            if (Target == null) return;
            var x = Mathf.Clamp(Target.position.x*0.65f,-10,10);
            var desired = new Vector3(x,7.4f,-32);
            var position = Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-4*Time.deltaTime));
            shake = Mathf.MoveTowards(shake,0,Time.deltaTime*0.7f);
            position += new Vector3(Mathf.Sin(Time.time*85),Mathf.Cos(Time.time*73),0)*shake;
            transform.position = position;
            transform.rotation = Quaternion.Euler(8,0,0);
        }
    }
}
