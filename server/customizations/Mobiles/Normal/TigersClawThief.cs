// ServUO: Mobiles/Normal/TigersClawThief.cs (CC6 batch 2). Values verbatim; serialization by the generator. The three
// Black Order humans of the Citadel (shared/malas/Citadel.json). ServUO's `Race.RandomFacialHair(this);` is a
// discarded int on both emulators (Server/Race.cs:81 here, :157 there): it assigns nothing, and is kept as
// written. Race is set before Hue and hair, as ServUO does, so Mobile.Race's setter picks the body from Female.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TigersClawThief : BaseCreature
{
    [Constructible]
    public TigersClawThief() : base(AIType.AI_Melee)
    {
        Title = "of the Tiger's Claw Sect";
        Female = Utility.RandomBool();
        Race = Race.Human;
        Hue = Race.RandomSkinHue();
        HairItemID = Race.RandomHair(Female);
        HairHue = Race.RandomHairHue();
        Race.RandomFacialHair(this);

        AddItem(new ThighBoots(0x51D));
        AddItem(new Wakizashi());
        AddItem(new FancyShirt(0x51D));
        AddItem(new StuddedMempo());
        AddItem(new JinBaori(0x69));

        Item item;

        item = new StuddedGloves();
        item.Hue = 0x69;
        AddItem(item);

        item = new LeatherNinjaPants();
        item.Hue = 0x51D;
        AddItem(item);

        item = new LightPlateJingasa();
        item.Hue = 0x51D;
        AddItem(item);

        // ServUO: "TODO quest items"

        SetStr(340, 360);
        SetDex(400, 415);
        SetInt(200, 215);

        SetHits(800, 815);

        SetDamage(13, 15);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 65);
        SetResistance(ResistanceType.Fire, 60, 70);
        SetResistance(ResistanceType.Cold, 55, 60);
        SetResistance(ResistanceType.Poison, 30, 50);
        SetResistance(ResistanceType.Energy, 30, 50);

        SetSkill(SkillName.MagicResist, 80.0, 100.0);
        SetSkill(SkillName.Tactics, 115.0, 130.0);
        SetSkill(SkillName.Wrestling, 95.0, 120.0);
        SetSkill(SkillName.Anatomy, 105.0, 120.0);
        SetSkill(SkillName.Fencing, 78.0, 100.0);
        SetSkill(SkillName.Swords, 90.1, 105.0);
        SetSkill(SkillName.Ninjitsu, 90.0, 120.0);
        SetSkill(SkillName.Hiding, 100.0, 120.0);
        SetSkill(SkillName.Stealth, 100.0, 120.0);

        Fame = 13000;
        Karma = -13000;

        VirtualArmor = 58;
    }

    public override string CorpseName => "a black order thief corpse";
    public override string DefaultName => "Black Order Thief";

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosFilthyRich, 4);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.3)
        {
            c.DropItem(new TigerClawSectBadge());
        }
    }
}
