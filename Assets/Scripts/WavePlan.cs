using System;
using System.Collections.Generic;

namespace Airstrike
{
    public enum AircraftRole { Bomber, FastBomber, Helicopter, SupplyTransport }
    public enum SupplyKind { RocketSlot, Piercing, Emp, JumpBoots }

    public static class SupplyRules
    {
        public static string Name(SupplyKind kind) => kind==SupplyKind.RocketSlot?"ROCKET SLOT":
            kind==SupplyKind.Piercing?"PIERCING":kind==SupplyKind.Emp?"EMP":"JUMP BOOTS";
    }

    public readonly struct AircraftSpawn
    {
        public readonly AircraftRole Role;
        public readonly bool Armored;
        public readonly int AttackRank;
        public int Cost => 1+(Armored?1:0)+AttackRank;
        public AircraftSpawn(AircraftRole role,bool armored=false,int attackRank=0)
        { Role=role; Armored=armored; AttackRank=attackRank; }
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
        private static readonly AircraftSpawn B=new AircraftSpawn(AircraftRole.Bomber);
        private static readonly AircraftSpawn A=new AircraftSpawn(AircraftRole.Bomber,true);
        private static readonly AircraftSpawn F=new AircraftSpawn(AircraftRole.FastBomber);
        private static readonly AircraftSpawn H=new AircraftSpawn(AircraftRole.Helicopter);
        private static readonly WaveDefinition[] Opening = {
            new WaveDefinition("FIRST CONTACT",B),
            new WaveDefinition("FAST MOVERS",B,F),
            new WaveDefinition("ROCKET LOCK",B,F,H),
            new WaveDefinition("HARD TARGETS",A,F,H),
            new WaveDefinition("CROSS FIRE",B,F,H,F,H,B),
            new WaveDefinition("HOLD THE SECTOR",H,F,A,H,F,B)
        };

        public const int MaximumActiveAircraft=12;
        public static int Budget(int wave) => (int)Math.Floor(Math.Pow(wave,1.12));

        public static WaveDefinition Get(int wave,int runSeed=0)
        {
            if(wave<1) throw new ArgumentOutOfRangeException(nameof(wave));
            if(wave<=Opening.Length) return Opening[wave-1];
            var remaining=Budget(wave);
            var random=new Random(unchecked(runSeed*397+wave));
            var spawns=new List<AircraftSpawn>();
            while(remaining>0)
            {
                var roll=random.Next(10);
                var spawn=roll<2?H:roll<4?F:B;
                if(spawn.Role==AircraftRole.Bomber && remaining>=2)
                {
                    var armored=random.Next(4)==0;
                    var rank=random.Next(3)==0?Math.Min(Math.Min((wave-3)/4,3),remaining-1-(armored?1:0)):0;
                    spawn=new AircraftSpawn(AircraftRole.Bomber,armored,rank);
                }
                spawns.Add(spawn);
                remaining-=spawn.Cost;
            }
            return new WaveDefinition("ENDLESS SKY",spawns.ToArray());
        }

        public static SupplyKind? Cargo(int wave)
        {
            if(wave<4 || wave%4!=0) return null;
            var cycle=(wave/4-1)%4;
            return cycle==0?SupplyKind.RocketSlot:cycle==1?SupplyKind.Piercing:cycle==2?SupplyKind.JumpBoots:SupplyKind.Emp;
        }
    }
}
