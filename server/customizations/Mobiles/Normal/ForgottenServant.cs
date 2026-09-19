// ServUO: Mobiles/Normal/ForgottenServant.cs (CC6 batch 2). Values verbatim; serialization by the generator.
// No [CorpseName] in ServUO, so the corpse name is the default for a human body, as there. The name is drawn
// per instance from the male/female name lists (Data/names.json here, names.xml there), so it is set in the
// constructor rather than through DefaultName.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ForgottenServant : BaseCreature
{
    [Constructible]
    public ForgottenServant() : base(AIType.AI_Melee)
    {
        SpeechHue = Utility.RandomDyedHue();
        Title = "Forgotten Servant";
        Hue = 768;

        Female = Utility.RandomBool();

        if (Female)
        {
            Body = 0x191;
            Name = NameList.RandomName("female");
            AddItem(new Skirt(Utility.RandomNeutralHue()));
        }
        else
        {
            Body = 0x190;
            Name = NameList.RandomName("male");
            AddItem(new ShortPants(Utility.RandomNeutralHue()));
        }

        SetStr(147, 215);
        SetDex(91, 115);
        SetInt(61, 85);

        SetHits(95, 123);

        SetDamage(4, 14);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 25, 35);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.MagicResist, 70.1, 85.0);
        SetSkill(SkillName.Swords, 60.1, 85.0);
        SetSkill(SkillName.Tactics, 75.1, 90.0);
        SetSkill(SkillName.Wrestling, 60.1, 85.0);

        Fame = 2500;
        Karma = -2500;

        AddItem(new Boots(Utility.RandomNeutralHue()));
        AddItem(new FancyShirt());
        AddItem(new Bandana());

        switch (Utility.Random(7))
        {
            case 0:
                AddItem(new Longsword());
                break;
            case 1:
                AddItem(new Cutlass());
                break;
            case 2:
                AddItem(new Broadsword());
                break;
            case 3:
                AddItem(new Axe());
                break;
            case 4:
                AddItem(new Club());
                break;
            case 5:
                AddItem(new Dagger());
                break;
            case 6:
                AddItem(new Spear());
                break;
        }

        Utility.AssignRandomHair(this);
    }

    public override bool ClickTitle => false;
    public override bool AlwaysMurderer => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }
}
