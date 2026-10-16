// ServUO: Mobiles/Normal/SabertoothedTiger.cs (cc-P129). Values verbatim; serialization by the generator.
// HealChance => .167 is pinned's CanHeal. SetMagicalAbility(Slashing) goes to the profile.
//
// DEVIATION, small: ServUO's OnAfterTame halves Str and Dex only (Int kept) when Pet Training is on and the tiger has
// never been owned. Pinned has no OnAfterTame hook (port recipe); StatLossAfterTame => true gives AnimalTaming's
// ScaleStats(0.50), which halves Str, Dex and Int. So a freshly tamed tiger has half its wild Int here, not all of it.
// Reverses when an OnAfterTame hook reaches AnimalTaming (Pet Training part 2's patch of that file). Reaches this
// creature only: no other ported tameable overrides OnAfterTame.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SabertoothedTiger : BaseCreature
{
    [Constructible]
    public SabertoothedTiger() : base(AIType.AI_Melee, FightMode.Aggressor)
    {
        Body = 0x588;
        Female = true;

        SetStr(496, 523);
        SetDex(386, 403);
        SetInt(443, 469);

        SetHits(362, 423);

        SetDamage(21, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.Parry, 105.0, 110.0);
        SetSkill(SkillName.Tactics, 90.0, 100.0);
        SetSkill(SkillName.Wrestling, 100.0, 105.0);
        SetSkill(SkillName.DetectHidden, 75.0);
        SetSkill(SkillName.Focus, 95.0, 105.0);

        Fame = 11000;
        Karma = -11000;

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 102.0;

        SetMagicalAbility(MagicalAbility.Slashing);
    }

    public override string CorpseName => "a saber-toothed tiger corpse";
    public override string DefaultName => "saber-toothed tiger";

    public override bool CanHeal => true; // ServUO: HealChance => .167

    public override int GetIdleSound() => 0x673;
    public override int GetAngerSound() => 0x670;
    public override int GetHurtSound() => 0x672;
    public override int GetDeathSound() => 0x671;

    public override double WeaponAbilityChance => 0.5;

    public override int Hides => 11;
    public override HideType HideType => HideType.Regular;
    public override int Meat => 3;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override bool StatLossAfterTame => true;
    public override bool CanAngerOnTame => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }
}
