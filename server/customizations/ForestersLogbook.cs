using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Forester's Logbook — portable record of all timber discoveries.
///
/// Tracks both vanilla colored woods (Oak → Frostwood) and extended rare
/// timbers (Ironwood → Starwood). Discovery data is account-backed
/// (ClusterFAccountData.WoodDiscoveries) — the item is a display device.
///
/// Issued automatically when a player joins the Foresters' Union.
/// Double-click opens ForestersLogbookGump.
///
/// The Discovery Report view allows remote submission of unreported extended
/// timber finds directly from the logbook, identical to visiting Cedric
/// Rowanwood in person.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ForestersLogbook : Item
{
    [Constructible]
    public ForestersLogbook() : base(0x1C11) // book graphic
    {
        Name     = "Forester's Logbook";
        Hue      = 0x0060; // forest green
        LootType = LootType.Blessed;
        Weight   = 1.0;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendMessage("The logbook must be in your backpack to read.");
            return;
        }

        if (pm.Account is not IAccount acct) return;

        pm.CloseGump<ForestersLogbookGump>();
        pm.SendGump(new ForestersLogbookGump(pm, ClusterFAccountPersistence.GetOrCreate(acct)));
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
