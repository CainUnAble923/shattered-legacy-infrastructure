// cc-P150 (F-48): Leads. A request may roll with a Lead: a rumor naming an area and a creature group, personal to the
// holder. While the Lead is live (Days, 3): a qualifying kill in the area gets an extra roll for an item that fills the
// request, and each qualifying kill counts toward a pity guarantee (by about the PityKills-th kill, 40).
//
//   Qualifying kill   a creature that is not a pet or a summon, with Fame at or above leads.qualifyingFameMin, standing in
//                     one of the area's regions (Region.IsPartOf, so sub-regions count), of the Lead's group (a slayer
//                     family's entry Slays it, or "any"). This is CHANGED from the brief's "whose loot table can roll that
//                     item type": pinned's creatures do not keep their loot packs (BaseCreature.AddLoot generates at once),
//                     and every AOS pack from Meager up rolls weapons, armour, shields and jewelry alike (LootPack.cs:188-
//                     258), so a fame floor is the honest proxy. Noted for Chase.
//   The 2x            the extra roll's chance is the table's extraRollChance for the creature's fame band, sized to be
//                     about what a creature of that fame rolls for a magic item of a kind in pinned's packs; so a Lead
//                     holder gets that chance twice over.
//   The pity          chance on the k-th qualifying kill = extra + (100 - extra) x (k - 1) / (PityKills - 1), so the 40th
//                     kill is 100 percent. A drop resets the count; the Lead runs on until it expires.
//   The drop          RequisitionItemFactory.Create: a normal item that fills the request, into the corpse (or the
//                     backpack when there is none), with a line to the player. No tag.
//   Quiet expiry      an expired Lead is just cleared; the request it rode rolls over at the next refresh. No Renown or
//                     standing change, no Infamy.
//   Better at rank    the chance a request rolls a Lead rises by League band, and richer areas unlock by League rank
//                     (areas[].minLeagueRank).
//   Seams             Sources: anything that wants to hand out its own Leads (treasure maps and Cartography, cc-P127;
//                     stolen family parts, cc-P142; exploration achievements) registers an IRequisitionLeadSource and is
//                     asked, in order, before the area table. A slayer request's Lead always points at its family (the
//                     area must list it). Nothing registers today.

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Items;
using Server.Mobiles;

namespace Server;

public readonly record struct RequisitionLeadPick(string AreaKey, string Group);

public interface IRequisitionLeadSource
{
    bool TryPick(PlayerMobile pm, RequisitionEntry request, int leagueRank, out RequisitionLeadPick pick);
}

public static class RequisitionLeads
{
    /// <summary>Other systems' Lead givers, asked in order before the table's areas.</summary>
    public static readonly List<IRequisitionLeadSource> Sources = new();

    // -- Rolling a Lead ---------------------------------------------------------------------------------

    /// <summary>Rolls whether this new request carries a Lead (the band's chance), and attaches one when it does.</summary>
    public static bool TryRoll(PlayerMobile pm, RequisitionEntry e, int leagueRank, DateTime now)
    {
        var chance = RequisitionTable.Current.LeadChancePercent(leagueRank);
        if (chance <= 0 || ClusterFRequisitions.Chance() * 100.0 >= chance)
            return false;

        return Attach(pm, e, leagueRank, now);
    }

    /// <summary>Attaches a Lead now, whatever the chance: a registered source's pick, else an area the rank may go to.</summary>
    public static bool Attach(PlayerMobile pm, RequisitionEntry e, int leagueRank, DateTime now)
    {
        var table = RequisitionTable.Current;
        var pick = default(RequisitionLeadPick);
        var found = false;

        foreach (var source in Sources)
        {
            if (source.TryPick(pm, e, leagueRank, out pick) && table.Area(pick.AreaKey) != null)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            var group = e.Kind == RequisitionKind.Slayer ? e.Slayer.ToString() : null;
            var areas = table.AreasFor(leagueRank, group);
            if (areas.Count == 0)
                return false;

            var area = areas[Utility.Random(areas.Count)];
            var g = group ?? (area.Groups.Count > 0 ? area.Groups[Utility.Random(area.Groups.Count)] : "any");
            pick = new RequisitionLeadPick(area.Key, g);
        }

        e.LeadArea = pick.AreaKey;
        e.LeadGroup = pick.Group;
        e.LeadExpires = now + TimeSpan.FromDays(Math.Max(1, table.Leads.Days));
        e.LeadKills = 0;
        return true;
    }

    /// <summary>Clears every Lead that has run out. Nothing else changes: quiet expiry.</summary>
    public static int Sweep(CharacterRequisitionData data, DateTime now)
    {
        var cleared = 0;
        foreach (var e in data.Requests)
        {
            if (e.HasLead && !e.LeadLive(now))
            {
                e.ClearLead();
                cleared++;
            }
        }

        return cleared;
    }

    // -- Words ------------------------------------------------------------------------------------------

    public static string AreaName(string key) => RequisitionTable.Current.Area(key)?.Name ?? key;

    public static string GroupNoun(string group) =>
        string.IsNullOrEmpty(group) || group.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? "the creatures"
            : Enum.TryParse<SlayerName>(group, true, out var name) ? FamilyNoun(name) : group;

    public static string FamilyNoun(SlayerName name) => name switch
    {
        SlayerName.Silver             => "the undead",
        SlayerName.OrcSlaying         => "orcs",
        SlayerName.TrollSlaughter     => "trolls",
        SlayerName.OgreTrashing       => "ogres",
        SlayerName.Repond             => "humanoids",
        SlayerName.DragonSlaying      => "dragons",
        SlayerName.Terathan           => "terathans",
        SlayerName.SnakesBane         => "snakes",
        SlayerName.LizardmanSlaughter => "lizardmen",
        SlayerName.ReptilianDeath     => "reptiles",
        SlayerName.DaemonDismissal    => "daemons",
        SlayerName.GargoylesFoe       => "gargoyles",
        SlayerName.BalronDamnation    => "balrons",
        SlayerName.Exorcism           => "demons",
        SlayerName.Ophidian           => "ophidians",
        SlayerName.SpidersDeath       => "spiders",
        SlayerName.ScorpionsBane      => "scorpions",
        SlayerName.ArachnidDoom       => "arachnids",
        SlayerName.FlameDousing       => "fire elementals",
        SlayerName.WaterDissipation   => "water elementals",
        SlayerName.Vacuum             => "air elementals",
        SlayerName.ElementalHealth    => "elementals",
        SlayerName.EarthShatter       => "earth elementals",
        SlayerName.BloodDrinking      => "blood elementals",
        SlayerName.SummerWind         => "snow elementals",
        SlayerName.ElementalBan       => "elementals",
        SlayerName.Fey                => "the fey",
        _                             => "creatures",
    };

    public static string Text(RequisitionEntry e, DateTime now)
    {
        if (!e.LeadLive(now)) return null;
        var left = e.LeadExpires - now;
        var when = left.TotalDays >= 1 ? $"{(int)Math.Ceiling(left.TotalDays)} days left" : $"{Math.Max(1, (int)Math.Ceiling(left.TotalHours))} hours left";
        return $"Lead: {GroupNoun(e.LeadGroup)} in {AreaName(e.LeadArea)} are said to carry such things ({when}; {e.LeadKills} kills so far).";
    }

    // -- Kills ------------------------------------------------------------------------------------------

    public static bool Qualifies(RequisitionEntry e, BaseCreature bc, out string why)
    {
        why = null;
        var table = RequisitionTable.Current;
        var area = table.Area(e.LeadArea);
        if (area == null) { why = "no such area"; return false; }
        if (bc == null) { why = "nowhere"; return false; }

        // At the death event the creature is already deleted (pinned Mobile.OnDeath deletes a non-player before
        // BaseCreature.OnDeath raises CreatureDeathEvent, Mobile.cs:4899-4902, BaseCreature.cs:3669), so the kill's
        // place is the corpse's.
        var map = bc.Map;
        var location = bc.Location;
        if (bc.Corpse is { Deleted: false } corpse)
        {
            map = corpse.Map;
            location = corpse.Location;
        }

        if (map == null || map == Map.Internal) { why = "nowhere"; return false; }
        if (bc.Controlled || bc.Summoned) { why = "a pet or a summon"; return false; }
        if (bc.Fame < table.Leads.QualifyingFameMin) { why = $"fame {bc.Fame} under {table.Leads.QualifyingFameMin}"; return false; }
        if (!string.IsNullOrEmpty(area.Facet) && !area.Facet.Equals(map.Name, StringComparison.OrdinalIgnoreCase)) { why = "another facet"; return false; }

        var region = Region.Find(location, map);
        var inArea = false;
        foreach (var name in area.Regions)
        {
            if (region != null && region.IsPartOf(name))
            {
                inArea = true;
                break;
            }
        }

        if (!inArea) { why = $"not in {area.Name}"; return false; }

        if (!string.IsNullOrEmpty(e.LeadGroup) && !e.LeadGroup.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<SlayerName>(e.LeadGroup, true, out var family) || SlayerGroup.GetEntryByName(family)?.Slays(bc) != true)
            {
                why = $"not {GroupNoun(e.LeadGroup)}";
                return false;
            }
        }

        return true;
    }

    /// <summary>The chance, in percent, that the k-th qualifying kill drops the item: the extra roll, plus the pity.</summary>
    public static int DropChancePercent(int fame, int kills)
    {
        var table = RequisitionTable.Current;
        var extra = table.ExtraRollPercent(fame);
        var pity = Math.Max(2, table.Leads.PityKills);
        if (kills >= pity) return 100;
        if (kills < 1) return extra;
        return Math.Min(100, extra + (100 - extra) * (kills - 1) / (pity - 1));
    }

    public static PlayerMobile KillerOf(BaseCreature bc) => bc?.LastKiller switch
    {
        PlayerMobile pm => pm,
        BaseCreature pet => pet.GetMaster() as PlayerMobile,
        _ => null,
    };

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        var pm = KillerOf(bc);
        if (pm != null)
            HandleKill(pm, bc);
    }

    /// <summary>
    /// A kill by this character: every open request with a live Lead the kill qualifies for counts it, rolls, and may
    /// drop its item. Returns what dropped (a fact reads it).
    /// </summary>
    public static List<Item> HandleKill(PlayerMobile pm, BaseCreature bc)
    {
        var dropped = new List<Item>();
        var data = ClusterFRequisitions.DataOf(pm, false);
        if (data == null || data.Requests.Count == 0)
            return dropped;

        var now = ClusterFRequisitions.Clock();
        foreach (var e in data.Requests)
        {
            if (!e.LeadLive(now) || !Qualifies(e, bc, out _))
                continue;

            e.LeadKills++;
            var chance = DropChancePercent(bc.Fame, e.LeadKills);
            if (ClusterFRequisitions.Chance() * 100.0 >= chance)
                continue;

            var item = RequisitionItemFactory.Create(e);
            if (item == null)
                continue;

            Deliver(pm, bc, item);
            e.LeadKills = 0;
            dropped.Add(item);
            pm.SendMessage(0x3B2, $"The rumor held: {ClusterFRequisitions.Describe(e)} was on the {bc.Name}.");
        }

        return dropped;
    }

    private static void Deliver(PlayerMobile pm, BaseCreature bc, Item item)
    {
        if (bc.Corpse is Container corpse && !corpse.Deleted)
            corpse.DropItem(item);
        else
            pm.AddToBackpack(item);
    }
}
