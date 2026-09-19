// ServUO: Mobiles/Normal/SentinelSpider.cs (CC6 batch 1). Values verbatim; serialization by the generator; ServUO's
// version-0 body/hue and BaseSoundID 387 upgrade paths in Deserialize dropped (new content starts at version 0).
// SetWeaponAbility(ArmorIgnore) is GetWeaponAbility (the CC3 transformation, notes/port-recipe.md).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SentinelSpider : BaseCreature
{
    [Constructible]
    public SentinelSpider() : base(AIType.AI_Melee)
    {
        Body = 0x9d;
        Hue = 1141;
        BaseSoundID = 0x388;

        SetStr(95, 100);
        SetDex(140, 145);
        SetInt(40, 45);

        SetHits(260, 265);

        SetDamage(15, 22);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 50);
        SetResistance(ResistanceType.Fire, 30, 35);
        SetResistance(ResistanceType.Cold, 30, 35);
        SetResistance(ResistanceType.Poison, 70, 75);
        SetResistance(ResistanceType.Energy, 30, 35);

        SetSkill(SkillName.Anatomy, 85.0, 90.0);
        SetSkill(SkillName.MagicResist, 88.5, 90.0);
        SetSkill(SkillName.Tactics, 102.9, 105.0);
        SetSkill(SkillName.Wrestling, 119.1, 120.0);
        SetSkill(SkillName.Poisoning, 101.0, 102.0);

        Fame = 775;
        Karma = -775;

        VirtualArmor = 28;
    }

    public override string CorpseName => "a sentinel spider corpse";
    public override string DefaultName => "a Sentinel spider";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ArmorIgnore;

    public override FoodType FavoriteFood => FoodType.Meat;
    public override PackInstinct PackInstinct => PackInstinct.Arachnid;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.Poor);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (!Controlled && Utility.RandomDouble() < 0.03)
        {
            c.DropItem(new LuckyCoin());
        }
    }
}
