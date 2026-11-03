// cc-P150 (F-47, F-48): what one character's requisitions board holds. Notes in shard-migration
// notes/cc-P150-requisitions-and-leads.md.
//
// A requisition is a guild's standing request for one magic item: a kind of item (RequisitionCategories) with a named
// property at a minimum intensity (a row of the Artificers' catalogue, ImbueCatalogue), or a slayer family, or just
// "anything magic with N properties". Each character holds PerGuild (3) open requests per guild, rolled from the guild's
// theme rows (RequisitionTable) at the tier the character's rank in that guild allows, capped by the League rank gate.
// They are PER CHARACTER because guild standing is per character (CharacterGuildData, cc-P18); the record is kept in
// ClusterFAccountData v20 keyed by character serial like CharacterGuildData and CharacterLeagueData.
//
// A request may carry a Lead (F-48): a rumor naming an area and a creature group. While the Lead is live (3 days), a
// qualifying kill there gets an extra roll for the item and a pity count that guarantees it by about the 40th kill
// (RequisitionLeads). A request with a live Lead survives the daily refresh; every other open request is replaced then.
// Nothing here loses Renown or standing when a request or a Lead lapses: there is no penalty of any kind.
//
// cc-P780 (F-24): the record also holds the character's League hunts: the daily hunts (HuntDay, Hunts) and the weekly
// great hunt (GreatHuntWeek, GreatHunt), each a HuntEntry (kill N of a League Bestiary family; LeagueHunts.cs). Version
// 1 of CharacterRequisitionData adds them; a version 0 record reads with no hunts and rolls them at the next read.
//
// Every record carries its own version and fails loudly on an unknown one (the D40 rule).

using System;
using System.Collections.Generic;
using Server.Items;

namespace Server;

public enum RequisitionKind
{
    Property, // an item kind with a named property at or above MinIntensity, and MinProperties properties in all
    Slayer,   // a slayer item of a family (the family itself, or its group's super slayer)
    AnyMagic, // any magic item of the kind with MinProperties properties ("no questions asked")
}

public sealed class RequisitionEntry
{
    public const int CurrentVersion = 0;

    public Guid            Id            { get; private set; }
    public string          GuildKey      { get; set; } = "";
    public RequisitionKind Kind          { get; set; }
    public string          Category      { get; set; } = "any";  // RequisitionCategories key
    public string          Property      { get; set; } = "";     // ImbueCatalogue row name (Property kind)
    public int             MinIntensity  { get; set; }           // percent of the row's Full value (Property kind)
    public int             MinProperties { get; set; }           // properties the item must have in all
    public SlayerName      Slayer        { get; set; }           // Slayer kind
    public int             Tier          { get; set; }           // 0 (Initiate) to 5 (top rank)
    public int             Renown        { get; set; }
    public int             Reputation    { get; set; }
    public DateTime        RolledAt      { get; set; }

    // -- The Lead (F-48) ------------------------------------------------------------------------------
    public string   LeadArea    { get; set; } = "";  // RequisitionTable area key
    public string   LeadGroup   { get; set; } = "";  // a SlayerName, or "any"
    public DateTime LeadExpires { get; set; } = DateTime.MinValue;
    public int      LeadKills   { get; set; }        // qualifying kills since the Lead was taken or last paid (the pity count)

    public bool HasLead => LeadExpires > DateTime.MinValue;

    public bool LeadLive(DateTime now) => HasLead && now < LeadExpires;

    public void ClearLead()
    {
        LeadArea    = "";
        LeadGroup   = "";
        LeadExpires = DateTime.MinValue;
        LeadKills   = 0;
    }

    public RequisitionEntry() => Id = Guid.NewGuid();

    public RequisitionEntry(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"RequisitionEntry version {version} is not one this build reads (0 to {CurrentVersion}).");

        Id            = r.ReadGuid();
        GuildKey      = r.ReadString();
        Kind          = (RequisitionKind)r.ReadInt();
        Category      = r.ReadString();
        Property      = r.ReadString();
        MinIntensity  = r.ReadInt();
        MinProperties = r.ReadInt();
        Slayer        = (SlayerName)r.ReadInt();
        Tier          = r.ReadInt();
        Renown        = r.ReadInt();
        Reputation    = r.ReadInt();
        RolledAt      = r.ReadDateTime();
        LeadArea      = r.ReadString();
        LeadGroup     = r.ReadString();
        LeadExpires   = r.ReadDateTime();
        LeadKills     = r.ReadInt();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(Id);
        w.Write(GuildKey);
        w.Write((int)Kind);
        w.Write(Category);
        w.Write(Property);
        w.Write(MinIntensity);
        w.Write(MinProperties);
        w.Write((int)Slayer);
        w.Write(Tier);
        w.Write(Renown);
        w.Write(Reputation);
        w.Write(RolledAt);
        w.Write(LeadArea);
        w.Write(LeadGroup);
        w.Write(LeadExpires);
        w.Write(LeadKills);
    }
}

/// <summary>
/// One character's requisitions (cc-P150): the day they were last rolled (ClusterFRequisitions.DayIndex) and the open
/// requests. A filled request leaves the list until the next refresh puts a new one in its place, so "3 per guild" is
/// also "3 fills per guild per day". Stored in ClusterFAccountData v20 keyed by character serial.
/// </summary>
public sealed class CharacterRequisitionData
{
    // 0: cc-P150. 1: cc-P780, the League hunts (HuntDay, Hunts, GreatHuntWeek, GreatHunt).
    public const int CurrentVersion = 1;

    /// <summary>The refresh day the open requests were rolled on; -1 before the first roll.</summary>
    public int RolledDay { get; set; } = -1;

    public List<RequisitionEntry> Requests { get; } = new();

    /// <summary>cc-P780: the refresh day the daily hunts were rolled on; -1 before the first roll.</summary>
    public int HuntDay { get; set; } = -1;

    /// <summary>cc-P780: today's hunts. A finished one stays (shown done) until the next refresh.</summary>
    public List<HuntEntry> Hunts { get; } = new();

    /// <summary>cc-P780: the hunt week the great hunt was rolled on; -1 before the first roll.</summary>
    public int GreatHuntWeek { get; set; } = -1;

    /// <summary>cc-P780: this week's great hunt, or null (none rolled, or no family open to the character).</summary>
    public HuntEntry GreatHunt { get; set; }

    public bool IsEmpty => RolledDay < 0 && Requests.Count == 0 && HuntDay < 0 && Hunts.Count == 0 &&
                           GreatHuntWeek < 0 && GreatHunt == null;

    public CharacterRequisitionData() { }

    public CharacterRequisitionData(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"CharacterRequisitionData version {version} is not one this build reads (0 to {CurrentVersion}).");

        RolledDay = r.ReadInt();

        var count = r.ReadInt();
        for (var i = 0; i < count; i++)
            Requests.Add(new RequisitionEntry(r));

        if (version < 1)
            return;

        HuntDay = r.ReadInt();
        var hunts = r.ReadInt();
        for (var i = 0; i < hunts; i++)
            Hunts.Add(new HuntEntry(r));

        GreatHuntWeek = r.ReadInt();
        GreatHunt = r.ReadBool() ? new HuntEntry(r) : null;
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(RolledDay);
        w.Write(Requests.Count);
        foreach (var e in Requests)
            e.Serialize(w);

        // v1
        w.Write(HuntDay);
        w.Write(Hunts.Count);
        foreach (var h in Hunts)
            h.Serialize(w);

        w.Write(GreatHuntWeek);
        w.Write(GreatHunt != null);
        GreatHunt?.Serialize(w);
    }

    public RequisitionEntry Find(Guid id) => Requests.Find(e => e.Id == id);

    public int CountFor(string guildKey)
    {
        var n = 0;
        foreach (var e in Requests)
            if (e.GuildKey.Equals(guildKey, StringComparison.OrdinalIgnoreCase))
                n++;
        return n;
    }
}

/// <summary>
/// cc-P780 (F-24): one League hunt: kill <see cref="Needed"/> of a League Bestiary family (LeagueHuntTable.Families) for a
/// guild, at a tier. Kills count until it is finished; it pays once, when the last kill lands. The pay is fixed when the
/// hunt is rolled, so a later edit to league-hunts.json changes new hunts only.
/// </summary>
public sealed class HuntEntry
{
    public const int CurrentVersion = 0;

    public Guid     Id         { get; private set; }
    public string   FamilyKey  { get; set; } = "";  // LeagueHuntFamily.Key
    public string   GuildKey   { get; set; } = "";  // the guild whose standing it pays
    public int      Tier       { get; set; }        // 0 to 5, as a requisition's
    public bool     Great      { get; set; }        // the weekly great hunt
    public int      Needed     { get; set; }
    public int      Kills      { get; set; }
    public int      Renown     { get; set; }
    public int      Reputation { get; set; }
    public int      Gold       { get; set; }
    public string   LootBox    { get; set; } = "";  // a LeagueShop.BoxTier name, or empty for none
    public bool     Paid       { get; set; }
    public DateTime RolledAt   { get; set; }

    public bool Done => Kills >= Needed;

    public HuntEntry() => Id = Guid.NewGuid();

    public HuntEntry(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"HuntEntry version {version} is not one this build reads (0 to {CurrentVersion}).");

        Id         = r.ReadGuid();
        FamilyKey  = r.ReadString();
        GuildKey   = r.ReadString();
        Tier       = r.ReadInt();
        Great      = r.ReadBool();
        Needed     = r.ReadInt();
        Kills      = r.ReadInt();
        Renown     = r.ReadInt();
        Reputation = r.ReadInt();
        Gold       = r.ReadInt();
        LootBox    = r.ReadString();
        Paid       = r.ReadBool();
        RolledAt   = r.ReadDateTime();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(Id);
        w.Write(FamilyKey);
        w.Write(GuildKey);
        w.Write(Tier);
        w.Write(Great);
        w.Write(Needed);
        w.Write(Kills);
        w.Write(Renown);
        w.Write(Reputation);
        w.Write(Gold);
        w.Write(LootBox);
        w.Write(Paid);
        w.Write(RolledAt);
    }
}
