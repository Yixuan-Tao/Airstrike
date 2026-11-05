using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airstrike
{
    internal sealed class MenuAutomation : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public string version=AirstrikeGame.Version;
            public bool passed;
            public string error="";
            public int runtimeErrors;
            public List<string> checks=new List<string>();
        }

        private static MenuAutomation active;
        private AirstrikeGame game;
        private string outputPath;
        private readonly Report report=new Report();
        private bool finished;
        private float deadline;

        public static void AttachIfRequested(AirstrikeGame owner)
        {
            if(active!=null) { active.game=owner; return; }
            var args=Environment.GetCommandLineArgs();
            var index=Array.IndexOf(args,"--menu-smoke");
            if(index<0 || index+1>=args.Length) return;
            var driver=new GameObject("Menu acceptance").AddComponent<MenuAutomation>();
            active=driver;
            driver.game=owner;
            driver.outputPath=Path.GetFullPath(args[index+1]);
            driver.deadline=Time.realtimeSinceStartup+20;
            DontDestroyOnLoad(driver.gameObject);
            Application.logMessageReceived+=driver.OnLog;
            driver.StartCoroutine(driver.Run());
        }

        private IEnumerator Run()
        {
            var suite=Suite();
            while(!finished)
            {
                bool more=false;
                string error=null;
                try { more=suite.MoveNext(); }
                catch(Exception exception) { error=exception.ToString(); }
                if(!more || error!=null) { Finish(error); yield break; }
                yield return suite.Current;
            }
        }

        private IEnumerator Suite()
        {
            yield return null;
            Check(!game.RunStarted && game.Player==null && game.Hud.VisibleMenuTitle=="CHOOSE A RULESET","The launcher waits for a ruleset before starting a battle.");
            var table=File.ReadAllText(BalanceSettings.TablePath);
            var changed=table.Replace("PlayerHealth,100,1,","PlayerHealth,141,1,");
            Check(BalanceSettings.Parse(changed,BalanceMode.PC).PlayerHealth==141
                && BalanceSettings.Parse(changed,BalanceMode.Classic).PlayerHealth==1,"One edited table column changes only its selected ruleset.");
            var rejected=false;
            try { BalanceSettings.Parse(table.Replace("PlayerHealth,100,1,","PlayerHealth,bad,1,"),BalanceMode.PC); }
            catch(FormatException error) { rejected=error.Message.Contains("PlayerHealth"); }
            Check(rejected,"An invalid balance row is rejected with the key that needs correction.");
            game.Hud.LanguageButton.onClick.Invoke();
            Check(UiLanguage.Chinese && game.Hud.VisibleMenuTitle=="选择玩法规则" && game.Hud.CurrentFont.HasCharacter('规'),"The menu switch enables Simplified Chinese with a CJK glyph.");
            game.Hud.PcEntry.onClick.Invoke();
            yield return null;
            Check(game.RunStarted && game.Rules.Mode==BalanceMode.PC && game.Player.Health==100
                && game.Hud.VisibleWaveLabel.StartsWith("波次"),"The PC entry loads PC settings and carries Chinese into the combat HUD.");
            game.Player.ApplyDamage(1,true);
            Check(game.Player.Health==99 && game.Lives==3,"The PC entry preserves hit points and the initial lives.");
            game.ReturnToMenu();
            yield return null;
            yield return null;
            Check(!game.RunStarted && UiLanguage.Chinese,"Returning to the launcher preserves the language and stops the battle.");
            game.Hud.ClassicEntry.onClick.Invoke();
            yield return null;
            Check(game.Rules.Mode==BalanceMode.Classic && game.Player.Health==1 && game.Rules.RespawnShield==4
                && game.Rules.TerrainCell==0.25f && game.Rules.RocketCooldown==0.2f,"The classic entry applies original-style survival, terrain and fire timing.");
            game.Hud.ToggleLanguage();
            yield return null;
            Check(!UiLanguage.Chinese && game.Hud.VisibleWaveLabel=="WAVE 01","F1's language action updates the active HUD immediately.");

            var armor=game.SpawnAircraft(new Vector2(8,7),1,AircraftRole.Bomber,true);
            armor.AttacksEnabled=false;
            armor.Damage(110);
            Check(armor.Health==1 && game.Aircraft.Contains(armor),"A classic armored aircraft absorbs the first hit with a shield.");
            armor.Damage(110);
            Check(!game.Aircraft.Contains(armor),"A follow-up hit destroys the aircraft while the shield is down.");
            for(var i=0;i<3;i++) game.CollectSupply(game.SpawnSupply(SupplyKind.RocketSlot,new Vector2(10,8)));
            Check(game.RocketCapacity==4,"Classic extra rockets can raise the slot count past the PC limit.");
            game.CollectSupply(game.SpawnSupply(SupplyKind.Piercing,new Vector2(10,8)));
            Check(game.PiercingEnabled && game.RocketCapacity==2,"Picking up classic piercing clamps rocket capacity to two.");
            game.Player.ApplyDamage(1,true);
            Check(game.Lives==2 && game.Player.Health==0 && (!game.PiercingEnabled || game.RocketCapacity==1),"A classic hit costs a life and one available upgrade.");
            game.Restart();
            yield return null;
            yield return null;
            Check(game.RunStarted && game.Rules.Mode==BalanceMode.Classic && game.Lives==3
                && !game.PiercingEnabled && game.RocketCapacity==1,"Restarting retains the ruleset and resets the run.");
        }

        private void Update()
        {
            if(!finished && Time.realtimeSinceStartup>deadline) Finish("Menu smoke test timed out.");
        }
        private void Check(bool condition,string description)
        {
            if(!condition) throw new InvalidOperationException(description);
            report.checks.Add(description);
        }
        private void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) report.runtimeErrors++;
        }
        private void Finish(string error)
        {
            if(finished) return;
            finished=true;
            Application.logMessageReceived-=OnLog;
            report.error=error??"";
            report.passed=error==null && report.runtimeErrors==0;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath,JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived-=OnLog;
            if(active==this) active=null;
        }
    }
}
