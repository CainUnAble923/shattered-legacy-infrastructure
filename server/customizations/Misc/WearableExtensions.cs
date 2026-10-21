// ServUO: Server/Mobile.cs SetWearable (cc-P194). ServUO's Mobile has SetWearable(item, hue, dropChance): hue the
// item, make it unmovable unless the drop roll says otherwise, and AddItem it. Pinned ModernUO's Mobile has no
// such helper (its creatures write the three lines by hand), so ported creatures call this extension instead and
// read the same as their source. Shared by the Covetous Void Spawn creatures (Cora, Vela, the revenant) and anything
// ported after them.

namespace Server;

public static class WearableExtensions
{
    public static void SetWearable(this Mobile m, Item item, int hue = -1, double dropChance = 0.0)
    {
        if (item == null)
        {
            return;
        }

        if (hue > -1)
        {
            item.Hue = hue;
        }

        if (dropChance <= 0 || Utility.RandomDouble() > dropChance)
        {
            item.Movable = false;
        }

        m.AddItem(item);
    }
}
