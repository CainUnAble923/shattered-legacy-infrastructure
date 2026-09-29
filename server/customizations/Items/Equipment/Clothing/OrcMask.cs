// ServUO: Items/Equipment/Clothing/Hats.cs:1843 (CC9 close, 2026-09-29), extracted from ServUO's multi-type file under
// its own name. The tailored orc mask. It shares graphic 0x141B with pinned's OrcishKinMask and the same five resists
// and hit points, but it is a different item in both trees: the kin mask is hued 0x8A4, named "a mask of orcish kin",
// costs 20 karma on equip, is refused over savage paint and is what orcs treat as kin (pinned Orc.cs:87 and three
// more); this one is undyed, cliloc 1025147 "orc mask", and does none of that. ServUO carries both.
//
// Reached by tailoring (Engines/Craft/OrcMaskTailoringRecipe.cs, ServUO DefTailoring.cs:245). ServUO also lists it in
// Loot.HatTypes (Loot.cs:411); pinned's HatTypes (Loot.cs:313-319) omits it, and TribalMask too, and a get-only static
// array cannot be extended without a patch. So it is craft-only here, and not in random hat loot; see the note.
//
// What changed: IRepairable and its RepairSystem are dropped, because pinned has no such interface. Pinned repairs
// clothing whose type its craft system lists (Engines/Craft/Core/Repair.cs:407), so once the tailoring entry exists
// the mask is tailor-repairable, as ServUO's RepairSystem made it. No DefaultWeight override: ServUO sets no weight
// on any mask, and pinned's default reads the tile, as ServUO's does.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class OrcMask : BaseHat
{
    [Constructible]
    public OrcMask() : base(0x141B)
    {
    }

    public override int LabelNumber => 1025147; // orc mask

    public override int BasePhysicalResistance => 1;
    public override int BaseFireResistance => 1;
    public override int BaseColdResistance => 7;
    public override int BasePoisonResistance => 7;
    public override int BaseEnergyResistance => 8;

    public override int InitMinHits => 20;
    public override int InitMaxHits => 30;

    public override bool Dye(Mobile from, DyeTub sender)
    {
        from.SendLocalizedMessage(sender.FailMessage);
        return false;
    }
}
