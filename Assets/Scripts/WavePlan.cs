using System;
using System.Collections.Generic;

namespace Airstrike
{
    public enum AircraftRole { Bomber, FastBomber, Helicopter, SupplyTransport }
    public enum SupplyKind { RocketSlot, Piercing, Emp, JumpBoots }

    public static class SupplyRules
    {
        public static string Name(SupplyKind kind) => kind switch
        {
            SupplyKind.RocketSlot=>UiLanguage.Text("ROCKET SLOT","额外火箭位"),
            SupplyKind.Piercing=>UiLanguage.Text("PIERCING","穿甲火箭"),
            SupplyKind.Emp=>UiLanguage.Text("EMP","电磁脉冲"),
            _=>UiLanguage.Text("JUMP BOOTS","跳跳靴")
        };
    }

    public readonly struct AircraftSpawn
    {
        public readonly AircraftRole Role;
        public readonly bool Armored;
        public readonly int AttackRank;
        public readonly bool SpeedBoosted;
        public int Cost => 1+(Armored?1:0)+AttackRank+(SpeedBoosted?1:0);
        public AircraftSpawn(AircraftRole role,bool armored=false,int attackRank=0,bool speedBoosted=false)
        { Role=role; Armored=armored; AttackRank=attackRank; SpeedBoosted=speedBoosted; }
    }

    public sealed class WaveDefinition
    {
        public string Name { get; }
        public IReadOnlyList<AircraftSpawn> Spawns { get; }
        public WaveDefinition(string name,params AircraftSpawn[] spawns)
        { Name=name; Spawns=Array.AsReadOnly(spawns); }
    }

    public static class WavePlan
    {
        private static readonly string[] OpeningNames =
            {"FIRST CONTACT","FAST MOVERS","ROCKET LOCK","HARD TARGETS","CROSS FIRE","HOLD THE SECTOR"};

        public static int Budget(int wave,BalanceSettings rules) => (int)Math.Min(rules.WaveBudgetCap,Math.Floor(Math.Pow(wave,rules.WaveExponent)));

        public static WaveDefinition Get(int wave,BalanceSettings rules,int runSeed=0)
        {
            if(wave<1) throw new ArgumentOutOfRangeException(nameof(wave));
            if(wave<=rules.Opening.Length && rules.Opening[wave-1]!=null)
                return new WaveDefinition(OpeningNames[wave-1],rules.Opening[wave-1]);
            var remaining=Budget(wave,rules);
            var random=new Random(unchecked(runSeed*397+wave));
            var spawns=new List<AircraftSpawn>();
            while(remaining>0)
            {
                var roll=random.NextDouble();
                var role=roll<rules.HeliWeight?AircraftRole.Helicopter:
                    roll<rules.HeliWeight+rules.FastWeight?AircraftRole.FastBomber:AircraftRole.Bomber;
                var spawn=new AircraftSpawn(role);
                if(spawn.Role==AircraftRole.Bomber && remaining>=2)
                {
                    var armored=random.NextDouble()<rules.ArmorChance;
                    var availableRank=wave<rules.AttackRankStart?0:1+(wave-rules.AttackRankStart)/rules.AttackRankStep;
                    var rank=random.NextDouble()<rules.AttackChance?
                        Math.Min(Math.Min(availableRank,rules.AttackRankCap),remaining-1-(armored?1:0)):0;
                    spawn=new AircraftSpawn(AircraftRole.Bomber,armored,rank);
                }
                if(rules.SpeedBoostChance>0 && remaining>spawn.Cost && random.NextDouble()<rules.SpeedBoostChance)
                    spawn=new AircraftSpawn(role,spawn.Armored,spawn.AttackRank,true);
                spawns.Add(spawn);
                remaining-=spawn.Cost;
            }
            return new WaveDefinition("ENDLESS SKY",spawns.ToArray());
        }

        public static SupplyKind? Cargo(int wave,BalanceSettings rules,int runSeed=0)
        {
            if(rules.CargoInterval==0 || wave%rules.CargoInterval!=0) return null;
            if(rules.RandomCargo) return (SupplyKind)new Random(unchecked(runSeed*719+wave)).Next(4);
            var cycle=(wave/rules.CargoInterval-1)%4;
            return cycle==0?SupplyKind.RocketSlot:cycle==1?SupplyKind.Piercing:cycle==2?SupplyKind.JumpBoots:SupplyKind.Emp;
        }
    }
}
