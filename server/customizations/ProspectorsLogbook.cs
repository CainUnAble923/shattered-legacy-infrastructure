using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Prospector's Logbook.
///
/// A Miners' Compact member item that displays the player's ore discovery records.
/// Discovery data is account-backed (ClusterFAccountData.OreDiscoveries) — the
/// physical item is a display device only.
///
/// Issued automatically when a player joins the Miners' Compact.
/// Opening the logbook opens ProspectorsLogbookGump.
///
/// Auto-routing hook (CompactOreSatchelRoutingHook) records discoveries whenever
/// colored ore is mined, regardless of whether the player carries a logbook.
/// The logbook simply provides the UI to view that data.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ProspectorsLogbook : Item
{
    [Constructible]
    public ProspectorsLogbook() : base(0x1C11) // brown book graphic
    {
        Name     = "Prospector's Logbook";
        Hue      = 0x0482; // earthy brown — matches Compact Ore Satchel
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

        pm.CloseGump<ProspectorsLogbookGump>();
        pm.SendGump(new ProspectorsLogbookGump(pm, ClusterFAccountPersistence.GetOrCreate(acct)));
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
