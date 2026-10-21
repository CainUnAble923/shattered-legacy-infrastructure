// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Creatures/Cora.cs (cc-P194).
//
// Cora the sorceress, the Covetous boss (35,000 hits; spawned at 5457,1808 every hour to an hour and a half by
// [SetupNewCovetous). She breathes fire, drains mana in an aura (range 3, 10 direct damage, and "Cora drains
// ~1_VAL~ points of your mana!" for a random share of the victim's mana), teleports a player or pet to her side
// every 30 to 60 seconds, and every 15 seconds to two minutes sends four cracks across the floor (north, west,
// south, east) that empty the mana of any player or pet they pass under. Her death hands a void artifact to a
// random damager three times in ten.
//
// Conversion notes (notes/cc-P194-port-wrong-and-covetous-void.md for the deviations):
//   SetSpecialAbility(DragonBreath) is GetMonsterAbilities => FireBreath (the CC4 Shame mapping).
//   SetAreaEffect(AuraDamage) + IAuraCreature.AuraEffect is HasAura with ServUO's Cora AuraDefinition (5 s,
//     range 3, 10 damage, 100% direct: AreaEffects.cs AuraDefinition.Initialize); pinned's AuraDamage has no
//     direct slot, so AuraDamage is overridden to deal it through AOS.Damage's direct argument. ServUO rolls
//     the aura at 40% per tick (AuraDamage.TriggerChance); pinned fires every tick: a deviation.
//   TeleportsTo/TeleportDuration/TeleportProb/TeleportsPets are ServUO BaseCreature members with no pinned
//     counterpart; TryTeleport is reproduced here from ServUO BaseCreature.cs:7280-7362 for Cora alone.
//   IElementalCreature/ElementType.Chaos is Pet Training's training-ground tag; nothing reads it here.
//   TownCryerSystem.UnderMysteriousPotionEffects (a Town Cryer quest potion) and the Siege branch of
//     OnKilledBy are not on this shard.
//   The mana-drain crack is CoraManaDrainItem, top level for the generator (ServUO nests it).
//   Animate(AnimationType.Spell, 1) is the classic Animate(16, 7, 1, true, false, 0) here, as pinned Spell.cs
//     animates a human caster.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.VoidPool;
using Server.Items;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CoraTheSorceress : BaseCreature
{
    private static readonly int[] _teleportOffsets =
    {
        -1, -1,
        -1, 0,
        -1, 1,
        0, -1,
        0, 1,
        1, -1,
        1, 0,
        1, 1
    };

    private DateTime _nextManaDrain;
    private DateTime _nextTeleport;

    [Constructible]
    public CoraTheSorceress() : base(AIType.AI_Mage)
    {
        Body = 0x191;
        Title = "the sorceress";

        HairItemID = 0x2045;
        HairHue = 452;

        SetStr(909, 949);
        SetDex(125);
        SetInt(903, 947);

        SetHits(35000);

        SetDamage(17, 25);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 52, 67);
        SetResistance(ResistanceType.Fire, 51, 68);
        SetResistance(ResistanceType.Cold, 51, 69);
        SetResistance(ResistanceType.Poison, 51, 70);
        SetResistance(ResistanceType.Energy, 50, 68);

        SetSkill(SkillName.Wrestling, 100.1, 119.7);
        SetSkill(SkillName.Tactics, 102.3, 118.5);
        SetSkill(SkillName.MagicResist, 101.2, 119.6);
        SetSkill(SkillName.Anatomy, 100.1, 117.5);
        SetSkill(SkillName.Magery, 100.1, 117.5);
        SetSkill(SkillName.EvalInt, 100.1, 117.5);

        Fame = 32000;
        Karma = -32000;

        AddAndEquip(new WildStaff(), 1971);
        AddAndEquip(new ThighBoots(), 1910);
        AddAndEquip(new ChainLegs(), 1936);
        AddAndEquip(new LeatherGloves(), 1910);
        AddAndEquip(new LeatherBustierArms(), 1947);
    }

    public override string DefaultName => "Cora";

    public override bool AlwaysMurderer => true;
    public override bool ClickTitle => false;
    public override bool ShowFameTitle => false;

    public override MonsterAbility[] GetMonsterAbilities() => [MonsterAbilities.FireBreath];

    // ServUO AuraDefinition for CoraTheSorceress: cooldown 5 s, range 3, damage 10, 100% direct.
    public override bool HasAura => true;
    public override TimeSpan AuraInterval => TimeSpan.FromSeconds(5);
    public override int AuraRange => 3;
    public override int AuraBaseDamage => 10;

    public TimeSpan ManaDrainInterval => TimeSpan.FromSeconds(Utility.RandomMinMax(15, 120));

    // ServUO: TeleportsTo true, TeleportDuration 30-60 s, TeleportProb 1.0, TeleportsPets true, TeleportRange 16.
    public virtual TimeSpan TeleportDuration => TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
    public virtual int TeleportRange => 16;
    public virtual double TeleportProb => 1.0;
    public virtual bool TeleportsPets => true;

    public override int GetDeathSound() => 0x316;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 3);
        AddLoot(LootPack.SuperBoss, 3);
    }

    private void AddAndEquip(Item item, int hue = 0)
    {
        item.Movable = false;
        item.Hue = hue;
        AddItem(item);
    }

    public override void AuraDamage()
    {
        if (!Alive || IsDeadBondedPet)
        {
            return;
        }

        var targets = new List<Mobile>();

        foreach (var m in GetMobilesInRange(AuraRange))
        {
            if (m != this && CanBeHarmful(m, false) && (Core.AOS || InLOS(m)) &&
                (m is BaseCreature bc && (bc.Controlled || bc.Summoned || bc.Team != Team) || m.Player))
            {
                targets.Add(m);
            }
        }

        foreach (var m in targets)
        {
            AOS.Damage(m, this, AuraBaseDamage, false, 0, 0, 0, 0, 0, 0, 100); // 100% direct, as ServUO's Cora aura
            m.RevealingAction();
            AuraEffect(m);
        }
    }

    public override void AuraEffect(Mobile m)
    {
        var mana = Utility.Random(1, m.Mana);
        m.Mana -= mana;
        m.SendLocalizedMessage(1153114, mana.ToString()); // Cora drains ~1_VAL~ points of your mana!
    }

    public override void OnThink()
    {
        base.OnThink();

        if (!Alive || Map == null || Map == Map.Internal)
        {
            return;
        }

        if (_nextManaDrain < Core.Now)
        {
            DoManaDrain();
        }

        if (Combatant != null && _nextTeleport < Core.Now)
        {
            TryTeleport();
            _nextTeleport = Core.Now + TeleportDuration;
        }
    }

    public void TryTeleport()
    {
        if (Deleted || !(TeleportProb > Utility.RandomDouble()))
        {
            return;
        }

        var toTeleport = GetTeleportTarget();

        if (toTeleport == null)
        {
            return;
        }

        var offset = Utility.Random(8) * 2;

        var to = Location;

        for (var i = 0; i < _teleportOffsets.Length; i += 2)
        {
            var x = X + _teleportOffsets[(offset + i) % _teleportOffsets.Length];
            var y = Y + _teleportOffsets[(offset + i + 1) % _teleportOffsets.Length];

            if (Map.CanSpawnMobile(x, y, Z))
            {
                to = new Point3D(x, y, Z);
                break;
            }

            var z = Map.GetAverageZ(x, y);

            if (Map.CanSpawnMobile(x, y, z))
            {
                to = new Point3D(x, y, z);
                break;
            }
        }

        var from = toTeleport.Location;
        toTeleport.MoveToWorld(to, Map);

        SpellHelper.Turn(this, toTeleport);
        SpellHelper.Turn(toTeleport, this);

        toTeleport.ProcessDelta();

        Effects.SendLocationParticles(EffectItem.Create(from, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
        Effects.SendLocationParticles(EffectItem.Create(to, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);

        toTeleport.PlaySound(0x1FE);

        Combatant = toTeleport;
    }

    public virtual Mobile GetTeleportTarget()
    {
        var list = new List<Mobile>();

        foreach (var m in GetMobilesInRange(TeleportRange))
        {
            var isPet = m is BaseCreature bc && bc.GetMaster() is PlayerMobile;

            if (m != this && (m.Player || TeleportsPets && isPet) && CanBeHarmful(m) && CanSee(m))
            {
                list.Add(m);
            }
        }

        return list.Count > 0 ? list[Utility.Random(list.Count)] : null;
    }

    public void DoManaDrain()
    {
        Animate(16, 7, 1, true, false, 0);

        DoEffects(Direction.North);
        DoEffects(Direction.West);
        DoEffects(Direction.South);
        DoEffects(Direction.East);

        _nextManaDrain = Core.Now + ManaDrainInterval;
    }

    private bool DoEffects(Direction d)
    {
        var x = X;
        var y = Y;
        const int range = 10;
        const int offset = 8;

        switch (d)
        {
            case Direction.North:
                x = X + Utility.RandomMinMax(-offset, offset);
                y = Y - range;
                break;
            case Direction.West:
                x = X - range;
                y = Y + Utility.RandomMinMax(-offset, offset);
                break;
            case Direction.South:
                x = X + Utility.RandomMinMax(-offset, offset);
                y = Y + range;
                break;
            case Direction.East:
                x = X + range;
                y = Y + Utility.RandomMinMax(-offset, offset);
                break;
        }

        for (var i = 0; i < range; i++)
        {
            switch (d)
            {
                case Direction.North:
                    y += i;
                    break;
                case Direction.West:
                    x += i;
                    break;
                case Direction.South:
                    y -= i;
                    break;
                case Direction.East:
                    x -= i;
                    break;
            }

            var z = Map.GetAverageZ(x, y);
            var p = new Point3D(x, y, z);

            if (SpellHelper.AdjustField(ref p, Map, 12, false))
            {
                var path = new MovementPath(this, p);

                if (path.Success)
                {
                    DropCrack(path);
                    return true;
                }
            }
        }

        return false;
    }

    private void DropCrack(MovementPath path)
    {
        var time = 10;
        var x = X;
        var y = Y;
        var map = Map;

        for (var i = 0; i < path.Directions.Length; ++i)
        {
            Server.Movement.Movement.Offset(path.Directions[i], ref x, ref y);
            IPoint3D p = new Point3D(x, y, map.GetAverageZ(x, y));

            Timer.DelayCall(TimeSpan.FromMilliseconds(time), () => ManaDrainEffects(p, map));

            time += 200;
        }
    }

    private void ManaDrainEffects(IPoint3D p, Map map)
    {
        if (Deleted || map == null || map == Map.Internal)
        {
            return;
        }

        var item = new CoraManaDrainItem(Utility.RandomList(6913, 6915, 6917, 6919), this);
        SpellHelper.GetSurfaceTop(ref p);

        item.MoveToWorld(new Point3D(p), map);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.30 > Utility.RandomDouble())
        {
            var m = DemonKnight.FindRandomPlayer(this);

            if (m != null)
            {
                var artifact = VoidPoolRewards.DropRandomArtifact();

                if (artifact != null)
                {
                    var pack = m.Backpack;

                    if (pack == null || !pack.TryDropItem(m, artifact, false))
                    {
                        m.BankBox.DropItem(artifact);
                    }

                    m.SendLocalizedMessage(1062317); // For your valor in combating the fallen beast, a special artifact has been bestowed on you.
                }
            }
        }
    }
}

// ServUO: CoraTheSorceress.ManaDrainItem. One crack on the floor (hue 1152, a second static one id up on the same
// tile); anyone it lands on or who walks onto it loses all mana (players and non-monster creatures that Cora may
// harm); after five seconds both turn hue 1153 and half a second later they are gone. One loaded from a save
// deletes itself and its static.
[SerializationGenerator(0, false)]
public partial class CoraManaDrainItem : Item
{
    [SerializableField(0)]
    private Item _static;

    [SerializableField(1)]
    private Mobile _owner;

    public CoraManaDrainItem(int id, Mobile owner) : base(id)
    {
        _owner = owner;

        Movable = false;
        Hue = 1152;
        Timer.DelayCall(TimeSpan.FromSeconds(5), ChangeHue);
    }

    private void ChangeHue()
    {
        if (Deleted)
        {
            return;
        }

        Hue = 1153;

        if (_static != null)
        {
            _static.Hue = 1153;
        }

        Timer.DelayCall(TimeSpan.FromSeconds(0.5), () => Delete());
    }

    public override void OnDelete()
    {
        _static?.Delete();
        _static = null;

        base.OnDelete();
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);

        if (Map == null || Map == Map.Internal)
        {
            return;
        }

        _static = new Static(ItemID + 1);
        _static.MoveToWorld(Location, Map);

        var victims = new List<Mobile>();

        foreach (var m in Map.GetMobilesInRange(Location, 0))
        {
            victims.Add(m);
        }

        foreach (var m in victims)
        {
            OnMoveOver(m);
        }
    }

    public override bool OnMoveOver(Mobile m)
    {
        if ((m is PlayerMobile || m is BaseCreature { Controlled: true } || m is BaseCreature { Summoned: true }) && _owner != null && m.CanBeHarmful(_owner, false))
        {
            m.FixedParticles(0x3779, 10, 25, 5002, EffectLayer.Head);
            m.Mana = 0;
        }

        return true;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _static?.Delete();
        Delete();
    }
}
