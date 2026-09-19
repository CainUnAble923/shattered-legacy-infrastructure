// ServUO: Mobiles/Normal/ClanRibbonPlagueRat.cs (CC6 batch 2). Values verbatim; serialization by the generator.
//
// NOT our PlagueRat. P1 ported ServUO's Mobiles/Normal/PlagueRat.cs, body 0xD7, hue 1710, which ServUO names
// "a Clan Ribbon Plague Rat". This is ServUO's *other* type, ClanRibbonPlagueRat, body 238, hue 52, named
// "Clan Ribbon Plague Rat" with no article, and it is the one the Abyss spawn data names
// (post-uoml/termur/Abyss.json). Two types, two save chains, similar names; both are ServUO's.
// Tamable = false, ControlSlots = 1 and MinTameSkill = -0.9 are ServUO's literal values and are kept.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClanRibbonPlagueRat : BaseCreature
{
    [Constructible]
    public ClanRibbonPlagueRat() : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Body = 238;
        BaseSoundID = 0xCC;

        SetStr(59);
        SetDex(51);
        SetInt(17);

        SetHits(92);
        SetStam(51);

        SetDamage(4, 8);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 40);
        SetResistance(ResistanceType.Poison, 5, 10);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.MagicResist, 30.0);
        SetSkill(SkillName.Tactics, 34.0);
        SetSkill(SkillName.Wrestling, 40.0);

        Fame = 150;
        Karma = -150;

        VirtualArmor = 6;

        Hue = 52;

        Tamable = false;
        ControlSlots = 1;
        MinTameSkill = -0.9;
    }

    public override string CorpseName => "a rat corpse";
    public override string DefaultName => "Clan Ribbon Plague Rat";

    public override int Meat => 1;

    public override FoodType FavoriteFood =>
        FoodType.Meat | FoodType.Fish | FoodType.Eggs | FoodType.GrainsAndHay;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }
}
