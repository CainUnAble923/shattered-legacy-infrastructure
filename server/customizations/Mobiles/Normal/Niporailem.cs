// ServUO: Mobiles/Normal/Niporailem.cs (CC9 close, 2026-09-29). Niporailem the Thief, the fourth Stygian Abyss boss,
// on BaseSABoss (batch 8, the altar-less Peerless base, D-79: a walk-up fight). Body 722 is a bodyTable.cfg row.
//
// Ported as the GATE for the Epiphany suites: its unique list is the only thing in ServUO that names either set, so
// without it the twelve Villainous pieces (ours since CC9 batch 5) and the twelve Virtuous ones (ported with it) are
// in no player's reach. Pinned's own post-uoml/termur/TerMur.json, "Spawner (602)" at [143, 158, -20], places one;
// that entry has resolved to nothing (EntryFlags.InvalidType) since the file was generated (Q-048).
//
// What changed, each proved against D:\UO\ModernUO-pinned:
//   AI_NecroMage                 AI_Mage (Q-050: pinned MageAI casts Necromancy from the creature's own skill).
//   base(..., 10, 1, 0.2, 0.4)   dropped: 10 is rewritten to 16 in ServUO and the speeds are dead (Q-008).
//   Name / Title / CorpseName    DefaultName, Title in the constructor, CorpseName override.
//   OnGotMeleeAttack(Mobile)     pinned's virtual carries the damage as a second argument; same body.
//   Combatant as Mobile          pinned's Combatant is already a Mobile.
//   Helpers (WriteMobileList)    a [SerializableField] list; ServUO's version-1 save of it is the generator's version 0.
//   ColUtility.Free(Helpers)     Clear(); ColUtility is ServUO's pooling helper and pinned has none.
//   DateTime.UtcNow              Core.Now.
// ServUO quirks kept, bug-list section 3's kind: OnGotMeleeAttack stops spawning once more than ten helpers are
// DELETED (not alive), and m_NextTreasure, m_Thrown and m_NextSpawn are not saved.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Niporailem : BaseSABoss
{
    [SerializableField(0, setter: "private")]
    private List<BaseCreature> _helpers = new();

    private DateTime _nextTreasure;
    private int _thrown;
    private DateTime _nextSpawn;

    [Constructible]
    public Niporailem() : base(AIType.AI_Mage)
    {
        Title = "the Thief";

        Body = 722;

        SetStr(1000);
        SetDex(1200);
        SetInt(1200);

        SetHits(10000, 10500);

        SetDamage(15, 27);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 34, 46);
        SetResistance(ResistanceType.Fire, 0);
        SetResistance(ResistanceType.Cold, 31, 49);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 31, 49);

        SetSkill(SkillName.Wrestling, 68.8, 85.0);
        SetSkill(SkillName.Tactics, 56.1, 90.0);
        SetSkill(SkillName.MagicResist, 87.7, 93.5);

        SetSkill(SkillName.EvalInt, 90.0, 100.0);
        SetSkill(SkillName.Meditation, 20.0, 30.0);
        SetSkill(SkillName.Necromancy, 120.0);
        SetSkill(SkillName.SpiritSpeak, 120.0);
        SetSkill(SkillName.Focus, 30.0, 40.0);

        PackNecroReg(12, 24); // ServUO: "Stratics didn't specify"

        Fame = 15000;
        Karma = -15000;
    }

    public override string CorpseName => "the corpse of niporailem";
    public override string DefaultName => "Niporailem";

    public override Type[] UniqueSAList => new[]
    {
        typeof(HelmOfVillainousEpiphany), typeof(GorgetOfVillainousEpiphany), typeof(BreastplateOfVillainousEpiphany),
        typeof(ArmsOfVillainousEpiphany), typeof(GauntletsOfVillainousEpiphany), typeof(LegsOfVillainousEpiphany),
        typeof(KiltOfVillainousEpiphany), typeof(EarringsOfVillainousEpiphany), typeof(GargishBreastplateOfVillainousEpiphany),
        typeof(GargishArmsOfVillainousEpiphany), typeof(NecklaceOfVillainousEpiphany), typeof(GargishLegsOfVillainousEpiphany),
        typeof(HelmOfVirtuousEpiphany), typeof(GorgetOfVirtuousEpiphany), typeof(BreastplateOfVirtuousEpiphany),
        typeof(ArmsOfVirtuousEpiphany), typeof(GauntletsOfVirtuousEpiphany), typeof(LegsOfVirtuousEpiphany),
        typeof(KiltOfVirtuousEpiphany), typeof(EarringsOfVirtuousEpiphany), typeof(GargishBreastplateOfVirtuousEpiphany),
        typeof(GargishArmsOfVirtuousEpiphany), typeof(NecklaceOfVirtuousEpiphany), typeof(GargishLegsOfVirtuousEpiphany)
    };

    public override Type[] SharedSAList => new[]
    {
        typeof(BladeOfBattle), typeof(DemonBridleRing), typeof(GiantSteps), typeof(SwordOfShatteredHopes)
    };

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 6);
        AddLoot(LootPack.Gems, 6);
    }

    public override int Meat => 1;
    public override bool AlwaysMurderer => true;

    public override int GetIdleSound() => 1609;
    public override int GetAngerSound() => 1606;
    public override int GetHurtSound() => 1608;
    public override int GetDeathSound() => 1607;

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage);

        if (_nextSpawn > Core.Now || _helpers.Count(bc => bc.Deleted) > 10)
        {
            return;
        }

        if (Hits > HitsMax / 4)
        {
            if (0.25 >= Utility.RandomDouble())
            {
                SpawnSpectralArmour(attacker);
            }
        }
        else if (0.10 >= Utility.RandomDouble())
        {
            SpawnSpectralArmour(attacker);
        }
    }

    public override void OnActionCombat()
    {
        var combatant = Combatant;

        if (combatant == null || combatant.Deleted || combatant.Map != Map || !InRange(combatant, 20) ||
            !CanBeHarmful(combatant) || !InLOS(combatant))
        {
            return;
        }

        if (Core.Now >= _nextTreasure)
        {
            ThrowTreasure(combatant);

            _thrown++;

            if (0.75 >= Utility.RandomDouble() && _thrown % 2 == 1) // 75% chance to toss a second one
            {
                _nextTreasure = Core.Now + TimeSpan.FromSeconds(3.0);
            }
            else
            {
                _nextTreasure = Core.Now + TimeSpan.FromSeconds(5.0 + 10.0 * Utility.RandomDouble()); // 5-15 seconds
            }
        }
    }

    public void SpawnSpectralArmour(Mobile m)
    {
        var map = Map;

        if (map == null)
        {
            return;
        }

        var spawned = new SpectralArmour();

        spawned.Team = Team;
        spawned.SummonMaster = this;

        var validLocation = false;
        var loc = Location;

        for (var j = 0; !validLocation && j < 10; ++j)
        {
            var x = X + Utility.Random(3) - 1;
            var y = Y + Utility.Random(3) - 1;
            var z = map.GetAverageZ(x, y);

            if (validLocation = map.CanFit(x, y, Z, 16, false, false))
            {
                loc = new Point3D(x, y, Z);
            }
            else if (validLocation = map.CanFit(x, y, z, 16, false, false))
            {
                loc = new Point3D(x, y, z);
            }
        }

        spawned.MoveToWorld(loc, map);
        spawned.Combatant = m;
        spawned.SummonMaster = this;

        _nextSpawn = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));

        _helpers.Add(spawned);
        this.MarkDirty();
    }

    public void DeleteSpectralArmour(Mobile target)
    {
        foreach (var m in _helpers.Where(bc => bc?.Deleted == false).ToList())
        {
            m.Delete();
        }

        _helpers.Clear();
        this.MarkDirty();
    }

    public override void OnDelete()
    {
        DeleteSpectralArmour(this);

        base.OnDelete();
    }

    private void ThrowTreasure(Mobile m)
    {
        DoHarmful(m);

        MovingParticles(m, 0xEEF, 9, 0, false, true, 0, 0, 9502, 6014, 0x11D, EffectLayer.Waist, 0);

        Timer.DelayCall(
            TimeSpan.FromSeconds(1),
            () =>
            {
                var treasure = new NiporailemsTreasure(this);

                m.PlaySound(0x033);
                m.AddToBackpack(treasure);
                m.SendLocalizedMessage(1112111); // To steal my gold? To give it freely!
            }
        );
    }
}
