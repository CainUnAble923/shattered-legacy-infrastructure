// cc-P82: where an imbue happens decides the highest tier it may reach and its success bonus (design: "Mule soul
// forge"). The places, best first:
//   Forge    within ForgeRange of a soul forge: a placed ArtificersSoulForge (the Artificers' hall forge, below), or a
//            soul forge item or map static with OSI's soul forge art (ServUO Imbuing.CheckSoulForge's IDs: 0x4263 to
//            0x4272, 0x4277 to 0x4286, 17607 to 17610). Overcharged, and the forge bonus; the Queen's own soulforge (in
//            the Queen's Palace region) gives the Queen's bonus instead (ServUO: 0.06 and 0.02 for the Royal City).
//   MuleIII  the owner within MuleRange of a pack mule with the Imbued Barding Socket at III: up to Full, forge bonus.
//   Anywhere the Artificers' Toolkit or the guildmaster: up to Full, no bonus (what everyone had before cc-P82).
//   MuleII   the socket at II: up to Refined, no bonus. With the defaults Anywhere is better, so II changes nothing for
//            a toolkit holder; see the cc-P82 notes (a decision for Chase).
// Every cap and bonus is in Configuration/imbuing.json (siteCaps, forgeBonusPercent, queensForgeBonusPercent).

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server;

public sealed record ImbueSite(string Name, string Key, ImbueTier Cap, int BonusPercent);

public static class ImbueSites
{
    public static bool IsSoulForgeArt(int itemId) =>
        itemId is >= 0x4263 and <= 0x4272 or >= 0x4277 and <= 0x4286 or >= 17607 and <= 17610;

    /// <summary>Whether a soul forge (item, addon part or map static) stands within range.</summary>
    public static bool NearSoulForge(Mobile m, int range)
    {
        var map = m.Map;
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var item in map.GetItemsInRange(m.Location, range))
        {
            if (item is ArtificersSoulForge || IsSoulForgeArt(item.ItemID))
            {
                return true;
            }
        }

        for (var x = m.X - range; x <= m.X + range; x++)
        {
            for (var y = m.Y - range; y <= m.Y + range; y++)
            {
                foreach (var tile in map.Tiles.GetStaticTiles(x, y))
                {
                    if (IsSoulForgeArt(tile.ID))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>The Imbued Barding Socket's tier on the owner's mule within range (0: none).</summary>
    public static int MuleSocketTier(Mobile m, int range) =>
        MuleModules.MuleOf(m, range) is { } mule ? MuleModules.TierOf(mule, MuleModuleId.ImbuedBardingSocket) : 0;

    /// <summary>The best place this character stands at now.</summary>
    public static ImbueSite Resolve(Mobile m)
    {
        var t = ImbuingTable.Current;
        var best = new ImbueSite("anywhere", "Anywhere", t.SiteCap("Anywhere"), 0);

        void consider(ImbueSite s)
        {
            if (s.Cap > best.Cap || s.Cap == best.Cap && s.BonusPercent > best.BonusPercent)
            {
                best = s;
            }
        }

        var socket = MuleSocketTier(m, t.MuleRange);
        if (socket >= 2)
        {
            consider(new ImbueSite("your mule's soul forge (II)", "MuleII", t.SiteCap("MuleII"), 0));
        }

        if (socket >= 3)
        {
            consider(new ImbueSite("your mule's soul forge (III)", "MuleIII", t.SiteCap("MuleIII"), t.ForgeBonusPercent));
        }

        if (NearSoulForge(m, t.ForgeRange))
        {
            var queens = m.Region?.IsPartOf("Queen's Palace") == true;
            consider(new ImbueSite(queens ? "the Queen's Soulforge" : "a soul forge", "Forge", t.SiteCap("Forge"),
                queens ? t.QueensForgeBonusPercent : t.ForgeBonusPercent));
        }

        return best;
    }
}

/// <summary>
/// cc-P82: the Artificers' hall soul forge. Staff place it ([add ArtificersSoulForge) in an Artificers' hall; standing
/// within ImbuingTable.ForgeRange of it lets the Imbuing Table reach Overcharged, with the forge success bonus.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ArtificersSoulForge : Item
{
    [Constructible]
    public ArtificersSoulForge() : base(0x4277)
    {
        Movable = false;
        Hue = 0x497; // the Artificers' arcane blue-purple (the toolkit's)
    }

    public override string DefaultName => "an Artificers' hall soul forge";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1049644, "Imbue here to reach Overcharged");
    }
}
