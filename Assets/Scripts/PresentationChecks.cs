using System;
using System.Collections;
using UnityEngine;

namespace Airstrike
{
    public static class PresentationChecks
    {
        public static IEnumerator Run(AirstrikeGame game,GameplayAutomation.Report report,Action<bool,string> check)
        {
            var appearance=game.Player.GetComponentInChildren<SoldierAppearance>();
            var capsule=game.Player.GetComponent<CapsuleCollider>();
            check(appearance!=null && Mathf.Abs(capsule.height-1.65f)<0.001f && Mathf.Abs(capsule.radius-0.32f)<0.001f,
                "The articulated player model retains the established physical capsule.");
            report.visualStrideDistance=appearance.StrideDistance;
            check(report.visualStrideDistance>3,"The character visual updates its walking pose during real movement.");
            yield return new WaitForSeconds(1.3f);
            for(var i=0;i<40;i++) game.Flash(new Vector2(-5+i%10,5+i%3),2);
            report.visualPoolSize=game.Effects.PooledCount;
            report.visualPeakActive=game.Effects.ActiveCount;
            check(game.Effects.PooledCount==BattleEffects.Capacity && game.Effects.ActiveCount<=BattleEffects.Capacity,
                "Overlapping explosions reuse a bounded visual-effects pool.");
            yield return new WaitForSeconds(1.4f);
            report.visualReturnedInactive=game.Effects.ActiveCount==0;
            check(report.visualReturnedInactive,"Smoke, fragments and shockwaves return to the pool after their lifetime.");
            check(UnityEngine.Object.FindFirstObjectByType<BattleBackdrop>()!=null,"The layered battlefield backdrop is present after wave progression.");
        }
    }
}
