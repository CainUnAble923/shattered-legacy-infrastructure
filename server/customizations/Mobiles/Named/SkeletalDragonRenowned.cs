// ServUO: Mobiles/Named/SkeletalDragonRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
//
// The breath is NOT dropped. ServUO's SetSpecialAbility(SpecialAbility.DragonBreath) resolves, for this type, to the
// second DragonBreathDefinition (Services/Pet Training/SpecialAbility.cs:880-897: Uses = { SkeletalDragonRenowned,
// SkeletalDragon }; 100% cold, effect hue 0x480, 0.16 of current hits, 30-45 s between breaths). Pinned ModernUO
// carries the same thing as MonsterAbilities.ColdBreath (Mobiles/Abilities/Fire Breath/ColdBreath.cs: ColdDamage 100,
// BreathEffectHue 0x480, over FireBreath's 0.16 / 30-45 s / 0x36D4 / 0x227 / animation 12), which is exactly what
// stock SkeletalDragon.cs:60 declares. CC4 Shame made the same mapping for fire; note section 2 for the cadence
// difference (ServUO: on think, 10%, 30 mana; here: on a combat action, 50%, no mana).
//
// Dropped: nothing.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SkeletalDragonRenowned : BaseRenowned
{
    private static MonsterAbility[] _abilities = { MonsterAbilities.ColdBreath };

    [Constructible]
    public SkeletalDragonRenowned() : base(AIType.AI_Mage)
    {
        Title = "[Renowned]";
        Body = 104;
        BaseSoundID = 0x488;

        Hue = 906;

        SetStr(898, 1030);
        SetDex(100, 200);
        SetInt(488, 620);

        SetHits(558, 599);

        SetDamage(29, 35);

        SetDamageType(ResistanceType.Physical, 75);
        SetDamageType(ResistanceType.Fire, 25);

        SetResistance(ResistanceType.Physical, 75, 80);
        SetResistance(ResistanceType.Fire, 40, 60);
        SetResistance(ResistanceType.Cold, 40, 60);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 40, 60);

        SetSkill(SkillName.EvalInt, 80.1, 100.0);
        SetSkill(SkillName.Magery, 80.1, 100.0);
        SetSkill(SkillName.MagicResist, 100.3, 130.0);
        SetSkill(SkillName.Tactics, 97.6, 100.0);
        SetSkill(SkillName.Wrestling, 97.6, 100.0);

        Fame = 22500;
        Karma = -22500;

        VirtualArmor = 80;
    }

    public override string CorpseName => "Skeletal Dragon [Renowned] corpse";
    public override string DefaultName => "Skeletal Dragon";

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override Type[] UniqueSAList => Array.Empty<Type>();
    public override Type[] SharedSAList => new[] { typeof(AxeOfAbandon), typeof(DemonBridleRing), typeof(VoidInfusedKilt), typeof(BladeOfBattle) };

    public override bool ReacquireOnMovement => true;
    public override double BonusPetDamageScalar => Core.SE ? 3.0 : 1.0;
    public override bool AutoDispel => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override bool BleedImmune => true;
    public override int Meat => 19;
    public override int Hides => 20;
    public override HideType HideType => HideType.Barbed;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 3);
    }
}
