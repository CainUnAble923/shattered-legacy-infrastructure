// cc-P62, F-35: fox ears and one fox tail. A staff-only test item for the kitsune's woman form ("Moonrunner");
// nothing places it and no player can get one. [add KitsuneEarsTail
//
// Our own art, shipped in the player package (player-package\vendor\shattered-legacy-art, registry.csv):
//   item icon      art ID 0x3C3C (15420), a PNG override like the runestone's
//   on the body    Animation 4783: TazUO draws body 4783's frames over the wearer (MobileView.Draw). Its frames are
//                  slot 400 of an anim7.mul that is entirely ours, which the launcher gives TazUO with one
//                  Bodyconv.def line (app\Build-UoOverrides.ps1, scripts\build-equip-anims.py). Walking, running
//                  and standing exist, all five drawn directions (cc-P138); every other action draws nothing.
//   paperdoll      gump 64783 for a woman (60000 + 4783) and 54783 for a man (50000 + 4783), by the wearer's sex
//                  only (PaperDollInteractable.GetAnimID), each placed for the Siege of Sosaria bodies the shard
//                  ships (cc-P138; vendor\shattered-legacy-art\paperdoll-fit.json says where, and
//                  scripts\check-paperdoll-fit.py gates the package on it).
//
// Humans and elves of either sex wear it (cc-P138; P62 refused men while there was no male paperdoll art).
// Gargoyles do not: the client picks their paperdoll body (665/666) by graphic, where our gump would sit wrong,
// and their animation groups are not the people layout, so nothing would draw on the body either.
//
// The layer is Earrings (0x12): in all eight of TazUO's draw orders it comes after Hair (LayerOrder.UsedLayers),
// so the ears draw over her hair, and nothing covers it (MobileView.IsCovered has no case for it). The beard layer
// would draw in the same place but cannot hold a real item: at world load Cleanup turns any item on Hair or
// FacialHair into virtual hair and deletes it (pinned UOContent/Misc/Cleanup.cs:79, Mobile.ConvertHair). The cost
// of Earrings is that she cannot also wear earrings.
//
// The server reads the EA tiledata, where 0x3C3C is an UNUSED slot, so ApplyTileData gives the server's in-memory
// table the record the players' files get (records.json; the launcher fact compares the two), the way
// ShatteredLegacyArt does for the runestone. Its values follow OSI's earrings 0x1087: flags 0x00400002, weight 1,
// layer 0x12, height 1. Bit 0x2 is TileFlag.Weapon (pinned Server/TileData.cs:220); OSI sets it on earrings, and
// neither pinned nor TazUO 26.0909.63 reads it, so it is copied as it is.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class KitsuneEarsTail : Item
{
    public const int ItemIdValue = 0x3C3C;
    public const int AnimationId = 4783;
    public const TileFlag TileFlags = TileFlag.Wearable | TileFlag.Weapon;
    public const Layer WornLayer = Layer.Earrings;
    public const int TileWeight = 1;
    public const int TileHeight = 1;
    public const string TileName = "fox ears and tail";

    [Constructible]
    public KitsuneEarsTail() : base(ItemIdValue)
    {
        Layer = WornLayer;
        Hue = 0; // natural colours; the grey hueable art is for later
    }

    public override string DefaultName => TileName;

    // Humans and elves, either sex. A gargoyle is told so and the item does not go on (header).
    public const string RefusalForGargoyles = "Only humans and elves can wear this.";

    public override bool OnEquip(Mobile from)
    {
        if (from.Race == Race.Gargoyle)
        {
            from.SendMessage(RefusalForGargoyles);
            return false;
        }

        return base.OnEquip(from);
    }

    public static void ApplyTileData() =>
        TileData.ItemTable[ItemIdValue] = new ItemData(
            TileName, TileFlags, TileWeight, (int)WornLayer, AnimationId, 0, 0, TileHeight
        );
}
