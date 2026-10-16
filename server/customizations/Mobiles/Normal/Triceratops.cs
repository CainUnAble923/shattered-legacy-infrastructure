// ServUO: Mobiles/Normal/Triceratops.cs (cc-P129). Values verbatim; serialization by the generator.
// HealChance => .167 is pinned's CanHeal. SetMagicalAbility(Piercing) goes to the profile. FruitsAndVegies is
// FruitsAndVeggies (port recipe).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Triceratops : BaseCreature
{
    [Constructible]
    public Triceratops() : base(AIType.AI_Melee)
    {
        Body = 0x587;
        Female = true;

        SetStr(1100, 1300);
        SetDex(150, 170);
        SetInt(280, 310);

        SetHits(1100, 1200);

        SetDamage(21, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 70, 80);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.Anatomy, 65.0, 75.0);
        SetSkill(SkillName.Tactics, 90.0, 100.0);
        SetSkill(SkillName.Wrestling, 95.0, 105.0);
        SetSkill(SkillName.DetectHidden, 75.0);
        SetSkill(SkillName.Focus, 95.0, 105.0);
        SetSkill(SkillName.Parry, 0.0, 105.0);

        Tamable = true;
        ControlSlots = 3;
        MinTameSkill = 102.0;

        SetMagicalAbility(MagicalAbility.Piercing);
    }

    public override string CorpseName => "a triceratops corpse";
    public override string DefaultName => "Triceratops";

    public override bool CanHeal => true; // ServUO: HealChance => .167

    public override int GetIdleSound() => 0x673;
    public override int GetAngerSound() => 0x670;
    public override int GetHurtSound() => 0x672;
    public override int GetDeathSound() => 0x671;

    public override double WeaponAbilityChance => 0.5;

    public override int Hides => 11;
    public override HideType HideType => HideType.Regular;
    public override int Meat => 3;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies;
    public override int TreasureMapLevel => 2;

    public override bool CanAngerOnTame => true;
    public override bool StatLossAfterTame => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }
}
