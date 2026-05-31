using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Artificers' Toolkit.
///
/// A portable tool issued to Artificers' Order members on joining.
/// Double-clicking opens the full Imbuing Table gump (imbue, disenchant,
/// craft essence) without needing to visit the Guildmaster NPC.
///
/// Requires active Artificers' Order membership to use.
/// Blessed — not dropped on death.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ArtificersToolkit : Item
{
    [Constructible]
    public ArtificersToolkit() : base(0x1EB8)  // gear/tool graphic
    {
        Hue      = 0x497;  // arcane blue-purple, matches the satchel
        Name     = "Artificers' Toolkit";
        Weight   = 1.0;
        LootType = LootType.Blessed;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendMessage("That must be in your backpack.");
            return;
        }

        if (!pm.CheckAlive()) return;

        // Membership check
        var acct = pm.Account as IAccount;
        if (acct == null)
        {
            pm.SendMessage(0x22, "You cannot use this item.");
            return;
        }

        var data     = ClusterFAccountPersistence.GetOrCreate(acct);
        var isMember = data.JoinedGuilds.Contains("artificers");

        if (!isMember)
        {
            pm.SendMessage(0x22, "Only members of the Artificers' Order can use this toolkit.");
            return;
        }

        pm.SendGump(new ArtificersImbueGump(pm));
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1049644, "Opens the Imbuing Table anywhere");
        list.Add(1049644, "Requires Artificers' Order membership");
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
