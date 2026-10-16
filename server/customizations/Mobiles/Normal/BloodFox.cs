// ServUO: Mobiles/Normal/BloodFox.cs (cc-P129). Values verbatim; serialization by the generator.
//
// SetWeaponAbility(BleedAttack) is pinned's GetWeaponAbility (port recipe, CC3). SetSpecialAbility(GraspingClaw) is
// kept as written: the ability core (cc-P121) stores it on the profile, where its pinned twin fires it, because the
// fox has no stock grasping claw. The version-0 Deserialize branch that re-set both is dead for new content.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BloodFox : BaseCreature
{
    [Constructible]
    public BloodFox() : base(AIType.AI_Melee)
    {
        Body = 0x58f;
        Female = true;

        SetStr(300, 320);
        SetDex(190, 200);
        SetInt(170, 210);

        SetHits(190, 200);

        SetDamage(16, 22);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 55, 65);
        SetResistance(ResistanceType.Poison, 25, 35);
        SetResistance(ResistanceType.Energy, 35);

        SetSkill(SkillName.MagicResist, 40.0, 50.0);
        SetSkill(SkillName.Tactics, 50.0, 70.0);
        SetSkill(SkillName.Wrestling, 75.0, 90.0);
        SetSkill(SkillName.DetectHidden, 50.0, 60.0);

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 72.0;

        SetSpecialAbility(SpecialAbility.GraspingClaw);
    }

    public override string CorpseName => "a blood fox corpse";
    public override string DefaultName => "Blood Fox";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override int Meat => 5;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override bool CanAngerOnTame => true;
    public override bool StatLossAfterTame => true;
}
