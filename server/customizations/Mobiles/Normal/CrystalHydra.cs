// ServUO: Mobiles/Normal/CrystalHydra.cs (CC6 batch 4). Values verbatim; serialization by the generator. Spawned by the
// two Prism of Light spawners (shared/felucca/PrismOfLight.json, shared/trammel/PrismOfLight.json), one entry each.
//
// DEVIATION (D-68): ServUO's SetSpecialAbility(SpecialAbility.DragonBreath) is dropped; ModernUO has no SpecialAbility
// table (P6, the Saurosaurus precedent). Everything else is as ServUO's, including the loop that re-rolls
// Utility.RandomMinMax(0, 1) on every iteration (so it packs 0 arcanist scrolls half the time and otherwise one or
// more, geometrically) and the drop of CrystallineFragments, which is a STOCK ModernUO type here
// (Items/Misc/Prism of Light/CrystallineFragments.cs, same ItemID, hue, loot type and label) and is not re-ported.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CrystalHydra : BaseCreature
{
    [Constructible]
    public CrystalHydra() : base(AIType.AI_Melee, FightMode.Closest)
    {
        Body = 0x109;
        Hue = 0x47E;
        BaseSoundID = 0x16A;

        SetStr(800, 830);
        SetDex(100, 120);
        SetInt(100, 120);

        SetHits(1450, 1500);

        SetDamage(21, 26);

        SetDamageType(ResistanceType.Physical, 5);
        SetDamageType(ResistanceType.Fire, 5);
        SetDamageType(ResistanceType.Cold, 80);
        SetDamageType(ResistanceType.Poison, 5);
        SetDamageType(ResistanceType.Energy, 5);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 80, 100);
        SetResistance(ResistanceType.Poison, 35, 45);
        SetResistance(ResistanceType.Energy, 80, 100);

        SetSkill(SkillName.Wrestling, 100.0, 120.0);
        SetSkill(SkillName.Tactics, 100.0, 110.0);
        SetSkill(SkillName.MagicResist, 80.0, 100.0);
        SetSkill(SkillName.Anatomy, 70.0, 80.0);

        Fame = 17000;
        Karma = -17000;

        for (var i = 0; i < Utility.RandomMinMax(0, 1); i++)
        {
            PackItem(Loot.RandomScroll(0, Loot.ArcanistScrollTypes.Length, SpellbookType.Arcanist));
        }

        // ServUO: SetSpecialAbility(SpecialAbility.DragonBreath); -- D-68
    }

    public override string CorpseName => "a crystal hydra corpse";
    public override string DefaultName => "a crystal hydra";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
        AddLoot(LootPack.HighScrolls);
        // ServUO: AddLoot(LootPack.Parrot); -- a 10% ParrotItem. Pinned LootPack.cs:515-523 carries the same pack
        // inside a comment block because ModernUO has no ParrotItem (D-72). Build A2 of batch 4 went red on it.
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.25)
        {
            c.DropItem(new ShatteredCrystals());
        }

        c.DropItem(new CrystallineFragments());
    }

    public override int Hides => 40;
    public override int Meat => 19;
    public override int TreasureMapLevel => 5;
}
