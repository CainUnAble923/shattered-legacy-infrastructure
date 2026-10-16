// ServUO: Mobiles/Normal/DragonWolf.cs (cc-P129). Values verbatim; serialization by the generator.
// SetSpecialAbility(DragonBreath) kept as written: no stock breath, so the profile's pinned twin fires the default
// (fire) definition, as ServUO's DragonBreathDefinitions does for a type it does not list.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DragonWolf : BaseCreature
{
    [Constructible]
    public DragonWolf() : base(AIType.AI_Melee)
    {
        Body = 719;
        BaseSoundID = 0x5ED;

        SetStr(750, 850);
        SetDex(60, 75);
        SetInt(50, 55);

        SetHits(800, 860);

        SetDamage(20, 25);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.Anatomy, 60.0, 70.0);
        SetSkill(SkillName.MagicResist, 125.0, 140.0);
        SetSkill(SkillName.Tactics, 95.0, 110.0);
        SetSkill(SkillName.Wrestling, 90.0, 105.0);
        SetSkill(SkillName.DetectHidden, 60.0);

        Fame = 8500;
        Karma = -8500;

        Tamable = true;
        ControlSlots = 4;
        MinTameSkill = 102.0;

        SetSpecialAbility(SpecialAbility.DragonBreath);
    }

    public override string CorpseName => "a dragon wolf corpse";
    public override string DefaultName => "a dragon wolf";

    public override bool CanAngerOnTame => true;

    public override int Meat => 4;
    public override int Hides => 25;
    public override FoodType FavoriteFood => FoodType.Meat;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Rich);
    }
}
