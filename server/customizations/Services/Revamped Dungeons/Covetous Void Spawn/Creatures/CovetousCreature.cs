// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/CovetousCreature.cs (cc-P194).
//
// The base of the 25 void creatures. Each is built at a Level (the wave number for a void spawn, 5 to 15 for the
// level-3 population) and half a second later raised to that level's Stage (Level / 5): skills toward 160,
// resistances toward 95, stats toward 410/422/250, hits toward 2400, stamina 3000, mana 8500 (mages) or 1500,
// and sometimes a point of damage. A void spawn (VoidSpawn) has no loot and no fame or karma; a level-3 one gives
// Rich loot, one pack per two stages.
//
// A creature on a waypoint path that has not reached the next waypoint for two minutes teleports to it, unless
// a player is within ten tiles, it is frozen or paralyzed, its combatant can see it, or one of the Void Pool's
// wall items (0x82, 0x3946, 0x3956, 0x3967, 0x3979) is within eight tiles.
//
// Conversion notes: GivesFameAndKarmaAward has no pinned counterpart; OnDeath zeroes Fame and Karma around the
// base call so Titles.ComputeKillAwards (BaseCreature.cs:3548) hands out nothing, and restores them after
// (the region's OnDeath, which reads Fame for the score, runs before OnDeath: Mobile.cs:4872-4873). ServUO's
// PlayerRangeSensitive => false and CanDestroyObstacles => true exist and are carried. The waypoint timer is a
// field pair rather than a Tuple. The level-3 spawner records its entry on the creature (Level3Entry).

using System;
using System.Linq;
using ModernUO.Serialization;
using Server.Engines.VoidPool;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CovetousCreature : BaseCreature
{
    private WayPoint _timeOnWayPointFor;
    private DateTime _timeOnWayPointUntil;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _level;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _voidSpawn;

    // Ours: the index of the Level3Spawner entry that spawned this creature, -1 for none (see Level3Spawner.cs).
    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _level3Entry = -1;

    public CovetousCreature(AIType ai, int level = 60, bool voidspawn = false) : base(ai)
    {
        _level = level;
        _voidSpawn = voidspawn;

        SetSkill(SkillName.MagicResist, SkillStart);
        SetSkill(SkillName.Tactics, SkillStart);
        SetSkill(SkillName.Wrestling, SkillStart);
        SetSkill(SkillName.Anatomy, SkillStart);

        if (ai == AIType.AI_Mage)
        {
            SetSkill(SkillName.Magery, SkillStart);
            SetSkill(SkillName.EvalInt, SkillStart);
            SetSkill(SkillName.Meditation, SkillStart);
        }

        SetStr(StrStart);
        SetDex(DexStart);
        SetInt(IntStart);

        SetHits(HitsStart);
        SetStam(StamStart);
        SetMana(ManaStart);

        SetDamage(MinDamStart, MaxDamStart);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, ResistStart - 5, ResistStart + 5);
        SetResistance(ResistanceType.Fire, ResistStart - 5, ResistStart + 5);
        SetResistance(ResistanceType.Cold, ResistStart - 5, ResistStart + 5);
        SetResistance(ResistanceType.Poison, ResistStart - 5, ResistStart + 5);
        SetResistance(ResistanceType.Energy, ResistStart - 5, ResistStart + 5);

        if (Stage > 1)
        {
            Timer.DelayCall(TimeSpan.FromSeconds(.5), SetPower);
        }

        Fame = Math.Min(8500, _level * 142);
        Karma = Math.Min(8500, _level * 142) * -1;
    }

    public virtual int Stage => Math.Max(1, _level / 5);
    public virtual int MaxStage => 15;

    public virtual int StatRatio => Utility.RandomMinMax(35, 60);

    public virtual double SkillStart => Utility.RandomMinMax(35, 50);
    public virtual double SkillMax => 160.0;

    public virtual int StrStart => Utility.RandomMinMax(91, 100);
    public virtual int DexStart => Utility.RandomMinMax(91, 100);
    public virtual int IntStart => IsMagical ? Utility.RandomMinMax(91, 100) : 1;

    public virtual int StrMax => 410;
    public virtual int DexMax => 422;
    public virtual int IntMax => 250;

    public virtual int MaxHits => 2400;
    public virtual int MaxStam => 3000;
    public virtual int MaxMana => IsMagical ? 8500 : 1500;

    public virtual int MinDamMax => 5;
    public virtual int MaxDamMax => 12;

    public virtual int MinDamStart => 5;
    public virtual int MaxDamStart => 15;

    public virtual int HitsStart => StrStart + (int)(StrStart * (StatRatio / 100.0));
    public virtual int StamStart => DexStart + (int)(DexStart * (StatRatio / 100.0));
    public virtual int ManaStart => IntStart + (int)(IntStart * (StatRatio / 100.0));

    public virtual bool RaiseDamage => true;
    public virtual double RaiseDamageFactor => 0.33;

    public virtual int ResistStart => 25;
    public virtual int ResistMax => 95;

    public virtual bool IsMagical => AIObject is MageAI;

    public virtual bool GivesFameAndKarmaAward => false;
    public override bool PlayerRangeSensitive => false;
    public override bool CanDestroyObstacles => true;

    public override void OnThink()
    {
        base.OnThink();

        if (!Alive)
        {
            return;
        }

        var waypoint = CurrentWayPoint;

        if (_timeOnWayPointFor == null && waypoint != null)
        {
            _timeOnWayPointFor = waypoint;
            _timeOnWayPointUntil = Core.Now + TimeSpan.FromMinutes(2);
        }
        else if (_timeOnWayPointFor != null && _timeOnWayPointFor == waypoint && _timeOnWayPointUntil < Core.Now)
        {
            if (CheckCanTeleport())
            {
                MoveToWorld(waypoint.Location, Map);
            }
        }
        else if (_timeOnWayPointFor != null && _timeOnWayPointFor != waypoint)
        {
            _timeOnWayPointFor = waypoint;
            _timeOnWayPointUntil = Core.Now + TimeSpan.FromMinutes(2);
        }
    }

    protected virtual bool CheckCanTeleport()
    {
        if (CurrentWayPoint == null || Frozen || Paralyzed || Combatant is Mobile combatant && combatant.InLOS(this))
        {
            return false;
        }

        foreach (var m in Map.GetMobilesInRange(Location, 10))
        {
            if (m is PlayerMobile && m.AccessLevel == AccessLevel.Player)
            {
                return false;
            }
        }

        foreach (var item in Map.GetItemsInRange(Location, 8))
        {
            var id = item.ItemID;

            if (id is 0x82 or 0x3946 or 0x3956 or 0x3967 or 0x3979)
            {
                return false;
            }
        }

        return true;
    }

    public override void GenerateLoot()
    {
        if (!_voidSpawn)
        {
            AddLoot(LootPack.Rich, Math.Max(1, Stage / 2));
        }
    }

    public virtual void SetPower()
    {
        if (Deleted)
        {
            return;
        }

        foreach (var skill in Skills)
        {
            if (skill != null && skill.Base > 0 && skill.Base < SkillMax)
            {
                var toRaise = SkillMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5);

                if (toRaise > skill.Base)
                {
                    skill.Base = Math.Min(SkillMax, toRaise);
                }
            }
        }

        SetResistance(ResistanceType.Physical, ResistMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5));
        SetResistance(ResistanceType.Fire, ResistMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5));
        SetResistance(ResistanceType.Cold, ResistMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5));
        SetResistance(ResistanceType.Poison, ResistMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5));
        SetResistance(ResistanceType.Energy, ResistMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5));

        var strRaise = StrMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5);
        var dexRaise = DexMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5);
        var intRaise = IntMax / MaxStage * Stage + Utility.RandomMinMax(-5, 5);

        if (strRaise > RawStr)
        {
            SetStr(Math.Min(StrMax, strRaise));
        }

        if (dexRaise > RawDex)
        {
            SetDex(Math.Min(DexMax, dexRaise));
        }

        if (intRaise > RawInt)
        {
            SetInt(Math.Min(IntMax, intRaise));
        }

        var hitsRaise = MaxHits / 60 * _level + Utility.RandomMinMax(-5, 5);
        var stamRaise = MaxStam / 60 * _level + Utility.RandomMinMax(-5, 5);
        var manaRaise = MaxMana / 60 * _level + Utility.RandomMinMax(-5, 5);

        if (hitsRaise > HitsMax)
        {
            SetHits(Math.Min(MaxHits, hitsRaise));
        }

        if (stamRaise > StamMax)
        {
            SetStam(Math.Min(MaxStam, stamRaise));
        }

        if (manaRaise > ManaMax)
        {
            SetMana(Math.Min(MaxMana, manaRaise));
        }

        if (RaiseDamage && Utility.RandomDouble() < RaiseDamageFactor)
        {
            DamageMin = Math.Min(MinDamMax, DamageMin + 1);
            DamageMax = Math.Min(MaxDamMax, DamageMax + 1);
        }
    }

    // ServUO: NoLootOnDeath (set by the controller on every wave creature) keeps the pack off the corpse. Pinned
    // has no such flag, and BaseCreature packs loot in its own constructor (BaseCreature.cs:864), before the
    // constructor argument has set VoidSpawn, so a void spawn empties its pack as it dies.
    public override bool OnBeforeDeath()
    {
        if (_voidSpawn && Backpack != null)
        {
            foreach (var item in Backpack.Items.ToArray())
            {
                item.Delete();
            }
        }

        return base.OnBeforeDeath();
    }

    public override void OnDeath(Container c)
    {
        if (Spawner is Level3Spawner level3)
        {
            level3.OnKilled(this);
        }

        if (GivesFameAndKarmaAward)
        {
            base.OnDeath(c);
            return;
        }

        var fame = Fame;
        var karma = Karma;

        Fame = 0;
        Karma = 0;

        base.OnDeath(c);

        Fame = fame;
        Karma = karma;
    }
}
