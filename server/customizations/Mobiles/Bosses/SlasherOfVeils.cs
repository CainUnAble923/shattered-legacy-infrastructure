// ServUO: Mobiles/Bosses/SlasherOfVeils.cs (CC6 batch 8, Part B). The Slasher of Veils, the Abyss's demon boss, on
// BaseSABoss (the altar-less Peerless base, D-79: a walk-up fight). Body 741 is a bodyTable.cfg row.
//
// What changed, each proved against D:\UO\ModernUO-pinned:
//   SetSpecialAbility(AngryFire / ManaDrain / TrueFear)  pinned has no MonsterAbility that does any of the three
//                                                        (a 30-40 fire/energy burst on hit; a 40-60 mana drain from
//                                                        everyone within 8 on hit; a resist-scaled freeze on approach
//                                                        with the five 10803xx messages). Dropped, D-82.
//   SetWeaponAbility(ParalyzingBlow)                      GetWeaponAbility (the recipe).
//   OnDamagedBySpell(Mobile)                              pinned's virtual is (Mobile from, int damage); same body.
//   the FireRing override                                 identical to the base's, kept as ServUO has it; the OnThink
//                                                         override's body is commented out in ServUO and stays so.
//   Loot.Construct over the 14 artifact types             all 14 are ours (CC9), checked by name and by the test.
// ServUO Abilities/SlayerGroup.cs:320 lists this type under Exorcism; pinned's Exorcism entry is a static typeof list
// that nothing registers into (D-86).

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SlasherOfVeils : BaseSABoss
{
    private static readonly int[] _north =
    {
        -1, -1,
        1, -1,
        -1, 2,
        1, 2
    };

    private static readonly int[] _east =
    {
        -1, 0,
        2, 0
    };

    [Constructible]
    public SlasherOfVeils() : base(AIType.AI_Mage)
    {
        Body = 741;

        SetStr(901, 1010);
        SetDex(127, 153);
        SetInt(1078, 1263);

        SetHits(50000, 65000);
        SetMana(10000);

        SetDamage(10, 15);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Cold, 20);
        SetDamageType(ResistanceType.Poison, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 65, 80);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 70, 80);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 70, 80);

        SetSkill(SkillName.Anatomy, 110.8, 129.7);
        SetSkill(SkillName.EvalInt, 113.4, 130);
        SetSkill(SkillName.Magery, 111.7, 130);
        SetSkill(SkillName.Spellweaving, 111.1, 125);
        SetSkill(SkillName.Meditation, 113.5, 129.9);
        SetSkill(SkillName.MagicResist, 110, 129.8);
        SetSkill(SkillName.Tactics, 110.5, 126.3);
        SetSkill(SkillName.Wrestling, 110.1, 130);
        SetSkill(SkillName.DetectHidden, 127.1);

        Fame = 35000;
        Karma = -35000;

        // ServUO: SetSpecialAbility(AngryFire), SetSpecialAbility(ManaDrain), SetSpecialAbility(TrueFear); D-82.
    }

    public override string CorpseName => "a slasher of veils corpse";
    public override string DefaultName => "The Slasher of Veils";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ParalyzingBlow;

    public override Type[] UniqueSAList => new[]
    {
        typeof(ClawsOfTheBerserker), typeof(Lavaliere), typeof(Mangler), typeof(HumanSignOfChaos),
        typeof(GargishSignOfChaos), typeof(StandardOfChaosG), typeof(StandardOfChaos)
    };

    public override Type[] SharedSAList => new[]
    {
        typeof(AxesOfFury), typeof(BladeOfBattle), typeof(DemonBridleRing), typeof(PetrifiedSnake),
        typeof(PillarOfStrength), typeof(SwordOfShatteredHopes), typeof(SummonersKilt)
    };

    public override bool Unprovokable => false;
    public override bool BardImmune => false;

    public override int GetIdleSound() => 1589;
    public override int GetAngerSound() => 1586;
    public override int GetHurtSound() => 1588;
    public override int GetDeathSound() => 1587;

    public override bool AlwaysMurderer => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 4);
        AddLoot(LootPack.Gems, 8);
    }

    public override void OnThink()
    {
        base.OnThink();

        // ServUO (commented out there too):
        // if (Combatant == null)
        //     return;
        //
        // if (Hits > 0.6 * HitsMax && Utility.RandomDouble() < 0.05)
        //     FireRing();
    }

    public override void FireRing()
    {
        for (var i = 0; i < _north.Length; i += 2)
        {
            var p = Location;

            p.X += _north[i];
            p.Y += _north[i + 1];

            IPoint3D po = p;

            SpellHelper.GetSurfaceTop(ref po);

            Effects.SendLocationEffect(new Point3D(po), Map, 0x3E27, 50);
        }

        for (var i = 0; i < _east.Length; i += 2)
        {
            var p = Location;

            p.X += _east[i];
            p.Y += _east[i + 1];

            IPoint3D po = p;

            SpellHelper.GetSurfaceTop(ref po);

            Effects.SendLocationEffect(new Point3D(po), Map, 0x3E31, 50);
        }
    }

    public override void OnDamagedBySpell(Mobile caster, int damage)
    {
        if (0.5 > Utility.RandomDouble() && caster.InRange(Location, 10) && Map != null && caster.Alive &&
            caster != this && caster.Map == Map)
        {
            MoveToWorld(caster.Location, Map);

            Timer.DelayCall(() => Combatant = caster);

            Effects.PlaySound(Location, Map, 0x1FE);
        }

        base.OnDamagedBySpell(caster, damage);
    }
}
