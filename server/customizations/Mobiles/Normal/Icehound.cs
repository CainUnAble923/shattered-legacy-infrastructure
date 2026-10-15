// ServUO: Mobiles/Normal/Icehound.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class IceHound : BaseCreature
{
    [Constructible]
    public IceHound()
        : base(AIType.AI_Melee)
    {
        Body = 98;
        BaseSoundID = 229;
        Hue = 1153;

        SetStr(102, 150);
        SetDex(81, 105);
        SetInt(36, 60);

        SetHits(66, 125);

        SetDamage(11, 17);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 80);

        SetResistance(ResistanceType.Physical, 25, 35);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 10, 20);
        SetResistance(ResistanceType.Energy, 10, 20);
        
        SetSkill(SkillName.Swords, 99.0);

        Fame = 3400;
        Karma = -3400;

        VirtualArmor = 30;

        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = 85.5;
    }

    public override string CorpseName => "an ice hound corpse";
    public override string DefaultName => "an ice hound";

    public override int Meat => 1;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override PackInstinct PackInstinct => PackInstinct.Canine;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ParalyzingBlow; // ServUO: SetWeaponAbility

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Meager);
    }
}
