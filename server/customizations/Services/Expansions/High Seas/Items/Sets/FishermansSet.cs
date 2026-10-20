// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Sets/FishermansSet.cs -> ours, the High Seas fishing
// port. The Fisherman's set, Charybdis's unique artifacts: four pieces (tall straw hat, vest, eelskin gloves, trousers),
// hue 2578, 125 hits, each a bait bonus of 10% and a set bonus of 50% to a baited catch (IFishingAttire, read by
// FishInfo.GetBaitStrength). ServUO's set also has four gargish pieces (FishermansChestguard on GargishPlateChest,
// FishermansKilt on GargishClothKiltArmor, FishermansArms on GargishLeatherArms, FishermansEarrings on GargishEarrings);
// two of those bases have no pinned equivalent (gargish plate chest, gargish earrings), and none of the four drops here
// (Charybdis gives the human four; Corgul, not ported, gives the others), so they are left for the next prompt.
//
// ServUO derives each piece from a stock type and gets set state from BaseArmor and BaseClothing. Here that state lives
// on BaseSetArmor and BaseSetClothing (S1, S10), so each piece derives from the carrier and reproduces the stock base's
// own members, the way the Hunter and Sorcerer sets do. ServUO's resist overrides read the piece's own value when the
// SA resist bonus is zero and the base otherwise; here the piece's own value is the base resist, as every ported set does.
// The eelskin gloves do not reproduce LeatherGloves's arcane-charge members: an artifact is never an arcane item.

using ModernUO.Serialization;

namespace Server.Items;

public interface IFishingAttire
{
    int BaitBonus { get; }
    int SetBonus { get; }
}

[SerializationGenerator(0, false)]
public partial class FishermansHat : BaseSetClothing, IFishingAttire
{
    [Constructible]
    public FishermansHat() : base(0x1716, Layer.Helm)
    {
        Hue = 2578;
        SetHue = 2578;
    }

    public override int LabelNumber => 1151190; // Fisherman's Tall Straw Hat
    public override SetItem SetID => SetItem.Fisherman;
    public override int Pieces => 4;

    public int BaitBonus => 10;
    public int SetBonus => 50;

    public override int InitMinHits => 125;
    public override int InitMaxHits => 125;

    public override int BasePhysicalResistance => 3;
    public override int BaseFireResistance => 8;
    public override int BaseColdResistance => 23;
    public override int BasePoisonResistance => 8;
    public override int BaseEnergyResistance => 8;

    // Stock TallStrawHat member, reproduced because the parent changed.
    public override double DefaultWeight => 1.0;
}

[Flippable(0x13da, 0x13e1)]
[SerializationGenerator(0, false)]
public partial class FishermansTrousers : BaseSetArmor, IFishingAttire
{
    [Constructible]
    public FishermansTrousers() : base(0x13DA)
    {
        Hue = 2578;
        SetHue = 2578;
        ArmorAttributes.MageArmor = 1;
    }

    public override int LabelNumber => 1151191; // Fisherman's Trousers
    public override SetItem SetID => SetItem.Fisherman;
    public override int Pieces => 4;

    public int BaitBonus => 10;
    public int SetBonus => 50;

    public override int InitMinHits => 125;
    public override int InitMaxHits => 125;

    public override int BasePhysicalResistance => 6;
    public override int BaseFireResistance => 20;
    public override int BaseColdResistance => 7;
    public override int BasePoisonResistance => 7;
    public override int BaseEnergyResistance => 8;

    // Stock StuddedLegs members, reproduced because the parent changed.
    public override double DefaultWeight => 5.0;
    public override int AosStrReq => 30;
    public override int OldStrReq => 35;
    public override int ArmorBase => 16;
    public override ArmorMaterialType MaterialType => ArmorMaterialType.Studded;
    public override CraftResource DefaultResource => CraftResource.RegularLeather;
    public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.Half;
}

[Flippable(0x13cc, 0x13d3)]
[SerializationGenerator(0, false)]
public partial class FishermansVest : BaseSetArmor, IFishingAttire
{
    [Constructible]
    public FishermansVest() : base(0x13CC)
    {
        Hue = 2578;
        SetHue = 2578;
    }

    public override int LabelNumber => 1151192; // Fisherman's Vest
    public override SetItem SetID => SetItem.Fisherman;
    public override int Pieces => 4;

    public int BaitBonus => 10;
    public int SetBonus => 50;

    public override int InitMinHits => 125;
    public override int InitMaxHits => 125;

    public override int BasePhysicalResistance => 4;
    public override int BaseFireResistance => 19;
    public override int BaseColdResistance => 5;
    public override int BasePoisonResistance => 5;
    public override int BaseEnergyResistance => 5;

    // Stock LeatherChest members, reproduced because the parent changed.
    public override double DefaultWeight => 6.0;
    public override int AosStrReq => 25;
    public override int OldStrReq => 15;
    public override int ArmorBase => 13;
    public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
    public override CraftResource DefaultResource => CraftResource.RegularLeather;
    public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
}

[Flippable(0x13c6, 0x13ce)]
[SerializationGenerator(0, false)]
public partial class FishermansEelskinGloves : BaseSetArmor, IFishingAttire
{
    [Constructible]
    public FishermansEelskinGloves() : base(0x13C6)
    {
        Hue = 2578;
        SetHue = 2578;
    }

    public override int LabelNumber => 1151189; // Fisherman's Eelskin Gloves
    public override SetItem SetID => SetItem.Fisherman;
    public override int Pieces => 4;

    public int BaitBonus => 10;
    public int SetBonus => 50;

    public override int InitMinHits => 125;
    public override int InitMaxHits => 125;

    public override int BasePhysicalResistance => 4;
    public override int BaseFireResistance => 19;
    public override int BaseColdResistance => 5;
    public override int BasePoisonResistance => 5;
    public override int BaseEnergyResistance => 5;

    // Stock LeatherGloves members, reproduced because the parent changed.
    public override double DefaultWeight => 1.0;
    public override int AosStrReq => 20;
    public override int OldStrReq => 10;
    public override int ArmorBase => 13;
    public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
    public override CraftResource DefaultResource => CraftResource.RegularLeather;
    public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
}
