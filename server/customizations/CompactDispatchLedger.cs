using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Compact Dispatch Ledger — issued to every Miners' Compact member on join.
///
/// Double-clicking this item opens the Guild Contract Ledger gump (work orders)
/// without requiring proximity to the Miners' Compact Liaison NPC.
///
/// The ledger effectively replaces the need to travel to Garrett Ashveil every
/// time a miner wants to check, accept, or turn in a work order. Turn-ins still
/// require the player to have the required materials in their backpack — only the
/// location restriction is lifted.
///
/// Properties:
///   - Blessed: not dropped on death
///   - Weight: 1 stone
///   - Guild membership required: Miners' Compact
///
/// Acquisition:
///   Issued automatically by ClusterFGuildSystem when joining the Miners' Compact.
///   Restorable via the Liaison's Restoration menu if lost or deleted.
/// </summary>
[SerializationGenerator(0, false)]
public partial class CompactDispatchLedger : Item
{
    [Constructible]
    public CompactDispatchLedger() : base(0xFBD) // Book graphic
    {
        Name     = "Compact Dispatch Ledger";
        Hue      = 0x8A4;     // Earthy brown — matches liaison's attire
        Weight   = 1.0;
        LootType = LootType.Blessed;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
            return;

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var acct = pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        if (data == null || !data.JoinedGuilds.Contains("mining"))
        {
            pm.SendMessage(0x22, "This ledger is issued to members of the Miners' Compact only.");
            return;
        }

        pm.SendGump(new GuildContractLedgerGump(pm, "mining"));
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Opens the Miners' Compact work order ledger from anywhere.");
        list.Add("Must be a Compact member. Materials still required for turn-ins.");
    }
}
