// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ExodusZealot.cs (cc-P195): the robed mage zealots of
// the Exodus Dungeon, twenty to a spawner; a thief's mark (StealingHandler gives a ritual key off one anywhere).
// The recipe's transformation; SetWearable is ExodusEncounter.Wear; the hair is the race's.

using ModernUO.Serialization;
using Server.Engines.Exodus;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ExodusZealot : BaseCreature
{
    [Constructible]
    public ExodusZealot() : base(AIType.AI_Mage)
    {
        Body = 401;
        Female = false;
        Hue = 33875;
        HairItemID = Race.Human.RandomHair(false);
        HairHue = Race.Human.RandomHairHue();

        Name = NameList.RandomName("male");
        Title = "The Exodus Zealot";

        SetStr(150, 210);
        SetDex(75, 90);
        SetInt(255, 310);

        SetHits(325, 390);

        SetDamage(6, 12);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 40);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 35, 40);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.Wrestling, 70.0, 100.0);
        SetSkill(SkillName.Tactics, 80.0, 100.0);
        SetSkill(SkillName.MagicResist, 50.0, 70.0);
        SetSkill(SkillName.Anatomy, 70.0, 100.0);
        SetSkill(SkillName.Magery, 85.0, 100.0);
        SetSkill(SkillName.EvalInt, 80.0, 100.0);
        SetSkill(SkillName.Poisoning, 70.0, 100.0);

        Fame = 10000;
        Karma = -10000;
        VirtualArmor = 30;

        ExodusEncounter.Wear(this, new ThighBoots());

        var shroud = new HoodedShroudOfShadows(2702) { LootType = LootType.Blessed };
        ExodusEncounter.Wear(this, shroud);

        var book = new Spellbook { LootType = LootType.Blessed };
        ExodusEncounter.Wear(this, book);
    }

    public override string CorpseName => "a human corpse";

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
        AddLoot(LootPack.MedScrolls);
    }
}
