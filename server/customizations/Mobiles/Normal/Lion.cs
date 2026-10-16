// ServUO: Mobiles/Normal/Lion.cs (cc-P129). Values verbatim; serialization by the generator.
// HealChance => .167 is pinned's CanHeal (AGENTS.md, equivalents): the self-heal runs on pinned's own interval, and
// the profile lists Heal once as the natural twin (cc-P121). SetMagicalAbility(Piercing) goes to the profile. The
// three SetWeaponAbility calls sit only in the version-0 Deserialize branch, dead for new content (port recipe), so
// the lion has no weapon ability; WeaponAbilityChance is ported for the day it gains one.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Lion : BaseCreature
{
    [Constructible]
    public Lion() : base(AIType.AI_Melee)
    {
        Body = 0x592;
        Female = true;
        BaseSoundID = 0x3EF;

        SetStr(710, 720);
        SetDex(200, 220);
        SetInt(120, 140);

        SetHits(350, 370);

        SetDamage(16, 22);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 35, 45);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 20, 40);

        SetSkill(SkillName.Parry, 90.0, 100.0);
        SetSkill(SkillName.Tactics, 100.0, 110.0);
        SetSkill(SkillName.Wrestling, 100.0, 110.0);
        SetSkill(SkillName.DetectHidden, 80.0);

        Fame = 11000;
        Karma = -11000;

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 96.0;

        SetMagicalAbility(MagicalAbility.Piercing);
    }

    public override string CorpseName => "a lion corpse";
    public override string DefaultName => "Lion";

    public override bool CanHeal => true; // ServUO: HealChance => .167

    public override int GetIdleSound() => 0x673;
    public override int GetAngerSound() => 0x670;
    public override int GetHurtSound() => 0x672;
    public override int GetDeathSound() => 0x671;

    public override double WeaponAbilityChance => 0.5;

    public override int Hides => 11;
    public override HideType HideType => HideType.Regular;
    public override int Meat => 5;
    public override FoodType FavoriteFood => FoodType.Meat;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }

    public override bool CanAngerOnTame => true;
    public override bool StatLossAfterTame => true;
}
