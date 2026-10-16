// ServUO: Mobiles/Normal/OsseinRam.cs (cc-P129). Values verbatim; serialization by the generator.
// SetWeaponAbility(MortalStrike) is GetWeaponAbility; BattleDefense and LifeLeech go to the profile (LifeLeech's
// pinned twin is DrainLifeAttack, stored because the ram has no stock drain).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class OsseinRam : BaseCreature
{
    [Constructible]
    public OsseinRam() : base(AIType.AI_Melee)
    {
        Body = 0x591;
        Female = true;

        SetStr(300, 400);
        SetDex(80, 100);
        SetInt(100, 120);

        SetHits(450, 550);

        SetDamage(18, 23);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 25);
        SetDamageType(ResistanceType.Energy, 25);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 70.0, 80.0);
        SetSkill(SkillName.Tactics, 80.0, 90.0);
        SetSkill(SkillName.Wrestling, 90.0, 100.0);
        SetSkill(SkillName.DetectHidden, 40.0, 50.0);
        SetSkill(SkillName.Anatomy, 75.0, 85.0);
        SetSkill(SkillName.Necromancy, 20.0);
        SetSkill(SkillName.SpiritSpeak, 20.0);

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 72.0;

        SetMagicalAbility(MagicalAbility.BattleDefense);
        SetSpecialAbility(SpecialAbility.LifeLeech);
    }

    public override string CorpseName => "an ossein ram corpse";
    public override string DefaultName => "Ossein Ram";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.MortalStrike;

    public override int Meat => 3;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override bool CanAngerOnTame => true;
    public override bool StatLossAfterTame => true;
}
