// ServUO: Mobiles/Normal/DragonsFlameMage.cs (CC6 batch 2). Values verbatim; serialization by the generator. The three
// Black Order humans of the Citadel (shared/malas/Citadel.json). ServUO's `Race.RandomFacialHair(this);` is a
// discarded int on both emulators (Server/Race.cs:81 here, :157 there): it assigns nothing, and is kept as
// written. Race is set before Hue and hair, as ServUO does, so Mobile.Race's setter picks the body from Female.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DragonsFlameMage : BaseCreature
{
    [Constructible]
    public DragonsFlameMage() : base(AIType.AI_Mage)
    {
        Title = "of the Dragon's Flame Sect";
        Female = Utility.RandomBool();
        Race = Race.Human;
        Hue = Race.RandomSkinHue();
        HairItemID = Race.RandomHair(Female);
        HairHue = Race.RandomHairHue();
        Race.RandomFacialHair(this);

        AddItem(new NinjaTabi());
        AddItem(new FancyShirt(0x51D));
        AddItem(new Hakama(0x51D));
        AddItem(new Kasa(0x51D));

        SetStr(340, 360);
        SetDex(200, 215);
        SetInt(400, 415);

        SetHits(600, 615);

        SetDamage(13, 15);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Cold, 20);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 30, 50);
        SetResistance(ResistanceType.Cold, 55, 60);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.EvalInt, 70.1, 80.0);
        SetSkill(SkillName.Magery, 90.1, 100.0);
        SetSkill(SkillName.MagicResist, 85.1, 95.0);
        SetSkill(SkillName.Tactics, 70.1, 80.0);
        SetSkill(SkillName.Wrestling, 60.1, 80.0);

        Fame = 13000;
        Karma = -13000;

        VirtualArmor = 58;
    }

    public override string CorpseName => "a black order mage corpse";
    public override string DefaultName => "Black Order Mage";

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosFilthyRich, 4);
    }

    // ServUO: half of any spell damage taken is dealt back to the caster.
    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        if (from != null)
        {
            from.Damage(damage / 2, from);
        }
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.3)
        {
            c.DropItem(new DragonFlameSectBadge());
        }
    }
}
