using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// League Member Pass — portable access to all guild services.
///
/// Double-click from anywhere to open the Guild Registry gump, which
/// shows every guild the player has joined with one-click access to:
///   • Contract ledger (work orders)
///   • Member dashboard / services gump
///
/// Issued to players as a convenience item; can also be obtained via
/// [AddItem LeagueMemberPass. Blessed — survives death.
/// </summary>
[SerializationGenerator(0, false)]
public partial class LeagueMemberPass : Item
{
    private const int PassItemID = 0x14F0; // Small book graphic
    private const int PassHue    = 1154;   // Society blue

    [Constructible]
    public LeagueMemberPass() : base(PassItemID)
    {
        Name     = "League Member Pass";
        Hue      = PassHue;
        LootType = LootType.Blessed;
        Weight   = 0.1;
        Stackable = false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack) && from.FindItemOnLayer(Layer.FirstValid) != this)
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack to use it.
            return;
        }

        if (from is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;

        pm.SendGump(new GuildProgressGump(pm, acct));
    }
}
