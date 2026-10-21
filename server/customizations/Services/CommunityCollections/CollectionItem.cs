// ServUO: Services/CommunityCollections/CollectionItem.cs (cc-P194, the two leaf classes a reward list needs:
// CollectionItem and CollectionHuedItem). ServUO's CollectionTitle, CollectionTreasureMap and CollectionSpellbook
// are the community collections' own (IComunityCollection, AddRewardTitle, AddCollectionPoints) and come with that
// system when it is ported.
//
// A row of a reward gump: the reward's type, picture, tooltip, hue and price, and the picture's bounds for layout.
// ServUO measures the bounds from the art when ItemBounds has none; pinned loads ItemBounds from Data/Items/
// ItemBounds.bin (Server/Items/ItemBounds.cs) and has no art reader, so a picture without bounds is taken as the
// 44 x 44 tile, which keeps the rows aligned.

using System;
using Server.Mobiles;

namespace Server;

public class CollectionItem
{
    public CollectionItem(Type type, int itemID, int tooltip, int hue, double points, bool questItem = false)
    {
        Type = type;
        ItemID = itemID;
        Tooltip = tooltip;
        Hue = hue;
        Points = points;
        QuestItem = questItem;

        var rec = Rectangle2D.Empty;
        var bounds = ItemBounds.Bounds;

        if (bounds != null && itemID >= 0 && itemID < bounds.Length)
        {
            rec = bounds[itemID];
        }

        if (rec.X == 0 && rec.Y == 0 && rec.Width == 0 && rec.Height == 0)
        {
            X = 0;
            Y = 0;
            Width = 44;
            Height = 44;
        }
        else
        {
            X = rec.X;
            Y = rec.Y;
            Width = rec.Width;
            Height = rec.Height;
        }
    }

    public Type Type { get; }
    public int ItemID { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public int Tooltip { get; }
    public int Hue { get; }
    public double Points { get; }
    public bool QuestItem { get; }

    public virtual bool Validate(PlayerMobile from, Item item) => true;

    public virtual bool CanSelect(PlayerMobile from) => true;
}

public class CollectionHuedItem : CollectionItem
{
    public CollectionHuedItem(Type type, int itemID, int tooltip, int hue, double points, int[] hues)
        : base(type, itemID, tooltip, hue, points) =>
        Hues = hues;

    public int[] Hues { get; }
}
