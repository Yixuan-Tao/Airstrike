using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Airstrike
{
    public enum BalanceMode { PC, Classic }

    public sealed class BalanceSettings
    {
        public BalanceMode Mode { get; }
        public int InitialLives { get; }
        public int LivesCap { get; }
        public int PlayerHealth { get; }
        public float RespawnDelay { get; }
        public float RespawnShield { get; }
        public float HurtShield { get; }
        public bool LoseUpgrade { get; }
        public float MoveSpeed { get; }
        public float JumpSpeed { get; }
        public int JumpCount { get; }
        public float AltitudeLimit { get; }
        public float CoyoteTime { get; }
        public float JumpBuffer { get; }
        public float RocketCooldown { get; }
        public float RocketSpeed { get; }
        public float RocketLifetime { get; }
        public int RocketSlots { get; }
        public int RocketSlotsCap { get; }
        public int PiercingSlotsCap { get; }
        public int PierceLimit { get; }
        public int RocketDamage { get; }
        public float RocketDamageRadius { get; }
        public float DigCooldown { get; }
        public float DigReach { get; }
        public float DigRadius { get; }
        public float TerrainCell { get; }
        public float TerrainBottom { get; }
        public float TerrainMean { get; }
        public float TerrainHill { get; }
        public float TerrainDetail { get; }
        public int BedrockRows { get; }
        public float BombCraterX { get; }
        public float BombCraterY { get; }
        public float RocketCraterX { get; }
        public float RocketCraterY { get; }
        public float CoverDepth { get; }
        public float BombDamageRadius { get; }
        public int BombDamageNear { get; }
        public int BombDamageFar { get; }
        public float BombSpeed { get; }
        public float BombGravity { get; }
        public float RocketHelicopterSpeed { get; }
        public float WaveExponent { get; }
        public int WaveBudgetCap { get; }
        public int ActiveAircraftCap { get; }
        public float FirstSpawnDelay { get; }
        public float WaveIntermission { get; }
        public float EntrySpreadBase { get; }
        public float EntrySpreadGrowth { get; }
        public float EntrySpreadCap { get; }
        public int CargoInterval { get; }
        public bool CargoBlocksWave { get; }
        public bool RandomCargo { get; }
        public float FastWeight { get; }
        public float HeliWeight { get; }
        public float ArmorChance { get; }
        public float AttackChance { get; }
        public int AttackRankCap { get; }
        public int AttackRankStart { get; }
        public int AttackRankStep { get; }
        public float SpeedBoostChance { get; }
        public float SpeedBoostMultiplier { get; }
        public int BomberHealth { get; }
        public int FastHealth { get; }
        public int HeliHealth { get; }
        public int ArmoredHealth { get; }
        public float ArmorShield { get; }
        public float BomberSpeed { get; }
        public float BomberSpeedJitter { get; }
        public float BomberGrowth { get; }
        public int BomberGrowthCap { get; }
        public float FastSpeed { get; }
        public float FastGrowth { get; }
        public int FastGrowthCap { get; }
        public float HeliSpeed { get; }
        public float HeliGrowth { get; }
        public int HeliGrowthCap { get; }
        public float CargoSpeed { get; }
        public float CargoLifetime { get; }
        public int BombCountTicks { get; }
        public float BombTicksPerSecond { get; }
        public float BombLane { get; }
        public int BurstCount { get; }
        public float BurstGap { get; }
        public float BurstCooldown { get; }
        public float BurstRange { get; }
        public float HeliWarning { get; }
        public float HeliCooldown { get; }
        public float HeliCooldownGrowth { get; }
        public float HeliCooldownFloor { get; }
        public float HeliAimJitter { get; }
        public float PilotChance { get; }
        public bool PilotRequiresCrash { get; }
        public float CrashChance { get; }
        public float CrashSolidChance { get; }
        public int FallingWreckCap { get; }
        public int WreckWidthCells { get; }
        public int WreckCellCap { get; }
        public int CrateCap { get; }
        public int PilotCap { get; }
        public float PickupReach { get; }
        public float CrateLifetime { get; }
        public float PilotLifetime { get; }
        public float CrateFallSpeed { get; }
        public float PilotFallSpeed { get; }
        public float PilotWalkSpeed { get; }
        public int DuplicateScore { get; }
        public int BomberScore { get; }
        public int FastScore { get; }
        public int HeliScore { get; }
        public int ArmoredScore { get; }
        public bool WaveScoreOnly { get; }
        internal AircraftSpawn[][] Opening { get; }

        public static string TablePath => Path.Combine(Application.streamingAssetsPath,"Balance.csv");
        public static BalanceSettings Load(BalanceMode mode) => Parse(File.ReadAllText(TablePath),mode);

        internal static BalanceSettings Parse(string csv,BalanceMode mode)
        {
            using var reader=new StringReader(csv);
            var header=reader.ReadLine()?.TrimStart('\uFEFF').Split(',');
            if(header==null || header.Length<3 || header[0].Trim('"')!="Key"
                || header[1].Trim('"')!="PC" || header[2].Trim('"')!="Classic")
                throw new FormatException("Expected columns Key, PC, Classic.");
            var values=new Dictionary<string,string>(StringComparer.Ordinal);
            var column=mode==BalanceMode.PC?1:2;
            while(reader.ReadLine() is string line)
            {
                if(string.IsNullOrWhiteSpace(line)) continue;
                var parts=line.Split(new[]{','},4);
                if(parts.Length<3) throw new FormatException("Incomplete row: "+line);
                var key=parts[0].Trim().Trim('"');
                if(key.Length==0 || !values.TryAdd(key,parts[column].Trim().Trim('"')))
                    throw new FormatException("Empty or duplicate key: "+key);
            }
            return new BalanceSettings(values,mode);
        }

        private BalanceSettings(Dictionary<string,string> values,BalanceMode mode)
        {
            Mode=mode;
            string Read(string key)
            {
                if(!values.TryGetValue(key,out var value)) throw new FormatException("Missing key: "+key);
                return value;
            }
            float Number(string key,float min,float max)
            {
                if(!float.TryParse(Read(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)
                    || float.IsNaN(number) || number<min || number>max)
                    throw new FormatException($"{key} must be between {min} and {max}.");
                return number;
            }
            int Integer(string key,int min,int max)
            {
                if(!int.TryParse(Read(key),NumberStyles.Integer,CultureInfo.InvariantCulture,out var number) || number<min || number>max)
                    throw new FormatException($"{key} must be an integer between {min} and {max}.");
                return number;
            }
            InitialLives=Integer(nameof(InitialLives),1,100);
            LivesCap=Integer(nameof(LivesCap),0,100);
            PlayerHealth=Integer(nameof(PlayerHealth),1,1000);
            RespawnDelay=Number(nameof(RespawnDelay),0,20);
            RespawnShield=Number(nameof(RespawnShield),0,30);
            HurtShield=Number(nameof(HurtShield),0,10);
            LoseUpgrade=Integer(nameof(LoseUpgrade),0,1)==1;
            MoveSpeed=Number(nameof(MoveSpeed),0.5f,25);
            JumpSpeed=Number(nameof(JumpSpeed),1,20);
            JumpCount=Integer(nameof(JumpCount),1,9);
            AltitudeLimit=Number(nameof(AltitudeLimit),3,20);
            CoyoteTime=Number(nameof(CoyoteTime),0,0.5f);
            JumpBuffer=Number(nameof(JumpBuffer),0,0.5f);
            RocketCooldown=Number(nameof(RocketCooldown),0.02f,10);
            RocketSpeed=Number(nameof(RocketSpeed),1,100);
            RocketLifetime=Number(nameof(RocketLifetime),0.1f,30);
            RocketSlots=Integer(nameof(RocketSlots),1,100);
            RocketSlotsCap=Integer(nameof(RocketSlotsCap),0,100);
            PiercingSlotsCap=Integer(nameof(PiercingSlotsCap),1,100);
            PierceLimit=Integer(nameof(PierceLimit),0,50);
            RocketDamage=Integer(nameof(RocketDamage),1,10000);
            RocketDamageRadius=Number(nameof(RocketDamageRadius),0.05f,12);
            DigCooldown=Number(nameof(DigCooldown),0.02f,5);
            DigReach=Number(nameof(DigReach),0.2f,10);
            DigRadius=Number(nameof(DigRadius),0.05f,4);
            TerrainCell=Number(nameof(TerrainCell),0.125f,1);
            TerrainBottom=Number(nameof(TerrainBottom),-14,-1);
            TerrainMean=Number(nameof(TerrainMean),-0.25f,1);
            TerrainHill=Number(nameof(TerrainHill),0,1);
            TerrainDetail=Number(nameof(TerrainDetail),0,0.5f);
            BedrockRows=Integer(nameof(BedrockRows),0,4);
            BombCraterX=Number(nameof(BombCraterX),0.05f,6);
            BombCraterY=Number(nameof(BombCraterY),0.05f,6);
            RocketCraterX=Number(nameof(RocketCraterX),0.05f,6);
            RocketCraterY=Number(nameof(RocketCraterY),0.05f,6);
            CoverDepth=Number(nameof(CoverDepth),0,8);
            BombDamageRadius=Number(nameof(BombDamageRadius),0.05f,12);
            BombDamageNear=Integer(nameof(BombDamageNear),1,10000);
            BombDamageFar=Integer(nameof(BombDamageFar),1,10000);
            BombSpeed=Number(nameof(BombSpeed),0.05f,40);
            BombGravity=Number(nameof(BombGravity),0,60);
            RocketHelicopterSpeed=Number(nameof(RocketHelicopterSpeed),1,100);
            WaveExponent=Number(nameof(WaveExponent),0.1f,1.6f);
            WaveBudgetCap=Integer(nameof(WaveBudgetCap),1,5000);
            ActiveAircraftCap=Integer(nameof(ActiveAircraftCap),1,50);
            FirstSpawnDelay=Number(nameof(FirstSpawnDelay),0,20);
            WaveIntermission=Number(nameof(WaveIntermission),0.05f,30);
            EntrySpreadBase=Number(nameof(EntrySpreadBase),0.15f,30);
            EntrySpreadGrowth=Number(nameof(EntrySpreadGrowth),0,5);
            EntrySpreadCap=Number(nameof(EntrySpreadCap),0.15f,60);
            CargoInterval=Integer(nameof(CargoInterval),0,100);
            CargoBlocksWave=Integer(nameof(CargoBlocksWave),0,1)==1;
            RandomCargo=Integer(nameof(RandomCargo),0,1)==1;
            FastWeight=Number(nameof(FastWeight),0,1);
            HeliWeight=Number(nameof(HeliWeight),0,1);
            ArmorChance=Number(nameof(ArmorChance),0,1);
            AttackChance=Number(nameof(AttackChance),0,1);
            AttackRankCap=Integer(nameof(AttackRankCap),0,6);
            AttackRankStart=Integer(nameof(AttackRankStart),1,60);
            AttackRankStep=Integer(nameof(AttackRankStep),1,60);
            SpeedBoostChance=Number(nameof(SpeedBoostChance),0,1);
            SpeedBoostMultiplier=Number(nameof(SpeedBoostMultiplier),1,3);
            BomberHealth=Integer(nameof(BomberHealth),1,10000);
            FastHealth=Integer(nameof(FastHealth),1,10000);
            HeliHealth=Integer(nameof(HeliHealth),1,10000);
            ArmoredHealth=Integer(nameof(ArmoredHealth),1,10000);
            ArmorShield=Number(nameof(ArmorShield),0,30);
            BomberSpeed=Number(nameof(BomberSpeed),0.1f,30);
            BomberSpeedJitter=Number(nameof(BomberSpeedJitter),0,10);
            BomberGrowth=Number(nameof(BomberGrowth),0,3);
            BomberGrowthCap=Integer(nameof(BomberGrowthCap),1,100);
            FastSpeed=Number(nameof(FastSpeed),0.1f,30);
            FastGrowth=Number(nameof(FastGrowth),0,3);
            FastGrowthCap=Integer(nameof(FastGrowthCap),1,100);
            HeliSpeed=Number(nameof(HeliSpeed),0.1f,30);
            HeliGrowth=Number(nameof(HeliGrowth),0,3);
            HeliGrowthCap=Integer(nameof(HeliGrowthCap),1,100);
            CargoSpeed=Number(nameof(CargoSpeed),0.1f,30);
            CargoLifetime=Number(nameof(CargoLifetime),1,120);
            BombCountTicks=Integer(nameof(BombCountTicks),1,5000);
            BombTicksPerSecond=Number(nameof(BombTicksPerSecond),1,120);
            BombLane=Number(nameof(BombLane),1,35);
            BurstCount=Integer(nameof(BurstCount),1,30);
            BurstGap=Number(nameof(BurstGap),0.02f,5);
            BurstCooldown=Number(nameof(BurstCooldown),0.1f,30);
            BurstRange=Number(nameof(BurstRange),0.5f,25);
            HeliWarning=Number(nameof(HeliWarning),0.05f,10);
            HeliCooldown=Number(nameof(HeliCooldown),0.05f,20);
            HeliCooldownGrowth=Number(nameof(HeliCooldownGrowth),0,2);
            HeliCooldownFloor=Number(nameof(HeliCooldownFloor),0.05f,20);
            HeliAimJitter=Number(nameof(HeliAimJitter),0,8);
            PilotChance=Number(nameof(PilotChance),0,1);
            PilotRequiresCrash=Integer(nameof(PilotRequiresCrash),0,1)==1;
            CrashChance=Number(nameof(CrashChance),0,1);
            CrashSolidChance=Number(nameof(CrashSolidChance),0,1);
            FallingWreckCap=Integer(nameof(FallingWreckCap),0,25);
            WreckWidthCells=Integer(nameof(WreckWidthCells),1,24);
            WreckCellCap=Integer(nameof(WreckCellCap),0,600);
            CrateCap=Integer(nameof(CrateCap),1,30);
            PilotCap=Integer(nameof(PilotCap),1,30);
            PickupReach=Number(nameof(PickupReach),0.1f,5);
            CrateLifetime=Number(nameof(CrateLifetime),1,300);
            PilotLifetime=Number(nameof(PilotLifetime),1,300);
            CrateFallSpeed=Number(nameof(CrateFallSpeed),0.1f,20);
            PilotFallSpeed=Number(nameof(PilotFallSpeed),0.1f,20);
            PilotWalkSpeed=Number(nameof(PilotWalkSpeed),0.05f,5);
            DuplicateScore=Integer(nameof(DuplicateScore),0,10000);
            BomberScore=Integer(nameof(BomberScore),0,10000);
            FastScore=Integer(nameof(FastScore),0,10000);
            HeliScore=Integer(nameof(HeliScore),0,10000);
            ArmoredScore=Integer(nameof(ArmoredScore),0,10000);
            WaveScoreOnly=Integer(nameof(WaveScoreOnly),0,1)==1;
            if(LivesCap>0 && LivesCap<InitialLives) throw new FormatException("LivesCap cannot be lower than InitialLives.");
            if(RocketSlotsCap>0 && RocketSlotsCap<RocketSlots) throw new FormatException("RocketSlotsCap cannot be lower than RocketSlots.");
            if(FastWeight+HeliWeight>1) throw new FormatException("FastWeight plus HeliWeight cannot exceed 1.");
            if(BomberSpeed<=BomberSpeedJitter) throw new FormatException("BomberSpeed must exceed BomberSpeedJitter.");
            if(EntrySpreadBase>EntrySpreadCap) throw new FormatException("EntrySpreadCap must be at least EntrySpreadBase.");
            Opening=new AircraftSpawn[6][];
            for(var index=0;index<Opening.Length;index++)
            {
                var key="Opening"+(index+1);
                var line=Read(key);
                if(line=="-") continue;
                var roles=line.Split('|');
                if(roles.Length>50) throw new FormatException(key+" supports at most 50 aircraft.");
                Opening[index]=new AircraftSpawn[roles.Length];
                for(var i=0;i<roles.Length;i++)
                {
                    Opening[index][i]=roles[i] switch
                    {
                        "B"=>new AircraftSpawn(AircraftRole.Bomber),
                        "A"=>new AircraftSpawn(AircraftRole.Bomber,true),
                        "F"=>new AircraftSpawn(AircraftRole.FastBomber),
                        "H"=>new AircraftSpawn(AircraftRole.Helicopter),
                        _=>throw new FormatException(key+" only accepts B, A, F, H or -.")
                    };
                }
            }
        }
    }
}
