// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/StockingSerpent.cs (cc-P408), the eighth boss
// of the Treasures of Sorcerer's Dungeon rotation. Body 0x509 (1289) is the client's Najasaurus animation.
//
// The port recipe: base(..., 10, 1, 0.2, 0.4) is base(AI_Melee) (Q-008); SetSpecialAbility(ViciousBite) goes to the
// cc-P121 ability profile, as ServUO's does.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class StockingSerpent : BaseCreature
{
    [Constructible]
    public StockingSerpent() : base(AIType.AI_Melee)
    {
        Name = "a stocking serpent";

        Body = 0x509;
        BaseSoundID = 0xDB;
        Hue = 1459;

        SetStr(800);
        SetDex(150);
        SetInt(1200);

        SetHits(8000);

        SetDamage(22, 29);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 100);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Anatomy, 115, 120);
        SetSkill(SkillName.Poisoning, 120);
        SetSkill(SkillName.MagicResist, 115, 120);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 120, 130);

        Fame = 12000;
        Karma = -12000;

        SetSpecialAbility(SpecialAbility.ViciousBite);
    }

    public override string CorpseName => "a stocking serpent corpse";
    public override Poison HitPoison => Poison.Lethal;
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
