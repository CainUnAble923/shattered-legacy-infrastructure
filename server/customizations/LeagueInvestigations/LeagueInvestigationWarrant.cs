// cc-P629 (P565 design, stage 1): the League investigation warrant and the site marker. Notes: shard-migration
// notes/cc-P629-build-league-warrants.md.
//
// LeagueInvestigationWarrant  one use, blessed, bound to the buying account the way the loot boxes' goods are
//                             (AccountBinding, LootBoxes/LootBoxItems.cs). Saves its encounter key and a receipt id,
//                             which stage 2's runner reserves and escrows, so a restored warrant is the same item.
//                             cc-P647: it rests only at the top of its account's backpacks and bank boxes, never in a bag
//                             (a bag moves or trades without asking what is in it); one saved in a bag returns on load.
// LeagueInvestigationMarker   the fixed rune at a site (the League rune's art, 0x1F14). Says where it is and what the
//                             site calls; it never spawns anything itself.
//
// Stage 1 has no runner: a warrant that passes every check says investigations are not open yet. CheckUse is the order
// the refusals are given in, and stage 2 calls it again on the final confirmation.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.LeagueInvestigations;
using Server.Engines.LeagueRewards;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

public enum WarrantState
{
    Ready,
    Reserved
}

public enum WarrantUse
{
    Ok,
    NotOwner,
    NotInBackpack,
    UnknownEncounter,
    Reserved,
    SiteDisabled,
    NotRegistered,
    WrongFacet,
    TooFar,
    FeatureOff
}

[SerializationGenerator(0, false)]
public partial class LeagueInvestigationWarrant : Item
{
    public const int Art = 0x14F0;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _boundAccount;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _encounterKey;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private string _receiptId;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private WarrantState _state;

    [Constructible]
    public LeagueInvestigationWarrant() : this("", "")
    {
    }

    public LeagueInvestigationWarrant(string encounterKey, string boundAccount) : base(Art)
    {
        _encounterKey = encounterKey ?? "";
        _boundAccount = boundAccount ?? "";
        _receiptId = Guid.NewGuid().ToString("N");
        Hue = LeagueShop.LeagueHue;
        LootType = LootType.Blessed;
    }

    public override double DefaultWeight => 0.1;

    public LeagueInvestigationEncounter? Encounter => LeagueInvestigationCatalog.FindEncounter(_encounterKey);

    public override string DefaultName =>
        Encounter is { } e ? $"Investigation Warrant: {e.BossName}" : "an Investigation Warrant (withdrawn)";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var e = Encounter;
        var site = e == null ? null : LeagueInvestigationCatalog.SiteOf(e);
        if (e == null || site == null)
        {
            list.Add("This investigation is no longer offered. Take it to staff.");
        }
        else
        {
            list.Add($"Site: {site.Name}, {site.Map} {site.Center.X}, {site.Center.Y}");
        }

        list.Add("Bound to your account");
        list.Add("One use");
        list.Add($"{(int)LeagueInvestigationCatalog.Settings.Cooldown.TotalHours}-hour account investigation cooldown");
        list.Add("Use at the marked site. Solo or with consenting party members.");
        list.Add("One corpse's loot for the group. A failed investigation spends the warrant.");
        list.Add("Server interruption restores it.");
    }

    /// <summary>Why this character cannot call this warrant here now, or Ok. Changes nothing.</summary>
    public WarrantUse CheckUse(Mobile from)
    {
        if (!Owns(from))
        {
            return WarrantUse.NotOwner;
        }

        if (from.Backpack == null || !IsChildOf(from.Backpack))
        {
            return WarrantUse.NotInBackpack;
        }

        var e = Encounter;
        var site = e == null ? null : LeagueInvestigationCatalog.SiteOf(e);
        if (e == null || site == null)
        {
            return WarrantUse.UnknownEncounter;
        }

        if (_state != WarrantState.Ready)
        {
            return WarrantUse.Reserved;
        }

        if (!site.Enabled)
        {
            return WarrantUse.SiteDisabled;
        }

        // Get, not the shop's GetOrCreate: a check makes no account record.
        if (from.Account is not IAccount acct || ClusterFAccountPersistence.Get(acct) is not { } data ||
            !ClusterFLeagueSystem.IsJoined(data))
        {
            return WarrantUse.NotRegistered;
        }

        if (from.Map != site.Map)
        {
            return WarrantUse.WrongFacet;
        }

        if (!from.InRange(site.Center, LeagueInvestigationCatalog.Settings.CallerRange))
        {
            return WarrantUse.TooFar;
        }

        return LeagueInvestigationCatalog.Settings.Enabled ? WarrantUse.Ok : WarrantUse.FeatureOff;
    }

    /// <summary>The plain sentence for a refusal, naming the cause; directions when the caller is not at the site.</summary>
    public string Message(Mobile from, WarrantUse use)
    {
        var e = Encounter;
        var site = e == null ? null : LeagueInvestigationCatalog.SiteOf(e);
        return use switch
        {
            WarrantUse.NotOwner => AccountBinding.Refusal,
            WarrantUse.NotInBackpack => "The warrant must be in your backpack to use it.",
            WarrantUse.UnknownEncounter => "This investigation is no longer offered. Take the warrant to staff.",
            WarrantUse.Reserved => "This warrant is already being used.",
            WarrantUse.SiteDisabled => $"The {site!.Name} investigation site is closed for now. Your warrant is kept.",
            WarrantUse.NotRegistered => "Only a registered League member can call an investigation. Register at the Registrar.",
            WarrantUse.WrongFacet or WarrantUse.TooFar => Directions(from, site!),
            WarrantUse.FeatureOff => "League investigations are not open yet. Your warrant is kept.",
            _ => $"The League's investigation of {e!.BossName} can be called here."
        };
    }

    public static string Directions(Mobile from, LeagueInvestigationSite site)
    {
        var where = $"Investigation site: {site.Name}, {site.Map} {site.Center.X}, {site.Center.Y}.";
        if (from.Map != site.Map)
        {
            return $"{where} You are on {from.Map}.";
        }

        var distance = Math.Max(Math.Abs(from.X - site.Center.X), Math.Abs(from.Y - site.Center.Y));
        return $"{where} You are {distance} tiles away; stand within {LeagueInvestigationCatalog.Settings.CallerRange} to call it.";
    }

    public override void OnDoubleClick(Mobile from)
    {
        var use = CheckUse(from);
        from.SendMessage(use is WarrantUse.Ok or WarrantUse.FeatureOff or WarrantUse.TooFar or WarrantUse.WrongFacet ? 0x3B2 : 0x22,
            Message(from, use));
    }

    // -- Account binding (as ClimbingBoots) ----------------------------------------------------------------------

    // A warrant a GM made with [add carries no account until a player character lifts it; then it binds to that account.
    private bool Unbound => string.IsNullOrEmpty(_boundAccount);

    public bool Owns(Mobile m) => !Unbound && AccountBinding.SameAccount(m, _boundAccount);

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

    public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

    public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
        target == from && Owns(from) ? base.OnDroppedToMobile(from, target) : AccountBinding.Refuse(from);

    // cc-P647 (P641 finding 1): only the top of a backpack or bank box of a character of its account, never a bag inside
    // one. A bag is dropped on the ground or traded without asking the items in it, so a warrant in a bag would leave with it.
    public bool IsRestingPlace(Item? target) =>
        target is Container c && c.Parent is Mobile holder && AccountBinding.SameAccount(holder, _boundAccount) &&
        (c == holder.Backpack || c == holder.FindBankNoCreate());

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
        IsRestingPlace(target) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

    // Dropped on a container's icon it goes into that container; on anything else it would stack, and a warrant does not.
    public override bool OnDroppedOnto(Mobile from, Item target) =>
        IsRestingPlace(target) ? base.OnDroppedOnto(from, target) : AccountBinding.Refuse(from);

    /// <summary>
    /// A warrant saved inside a bag (before P647) goes back to the top of the owner's pack or bank it was in. Run after the
    /// world loads; a warrant anywhere else is left for staff.
    /// </summary>
    public void ReturnToRoot()
    {
        if (Deleted || Parent is not Container held || IsRestingPlace(held))
        {
            return;
        }

        Item root = held;
        while (root.Parent is Item up)
        {
            root = up;
        }

        if (root is Container top && IsRestingPlace(top))
        {
            top.DropItem(this);
        }
    }

    [AfterDeserialization]
    private void AfterLoad() => Timer.StartTimer(TimeSpan.Zero, ReturnToRoot);

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (from.AccessLevel < AccessLevel.GameMaster && !Owns(from))
        {
            if (Unbound && from.AccessLevel == AccessLevel.Player && from.Account != null)
            {
                BoundAccount = from.Account.Username;
                return base.CheckLift(from, item, ref reject);
            }

            reject = LRReason.CannotLift;
            return false;
        }

        return base.CheckLift(from, item, ref reject);
    }
}

[SerializationGenerator(0, false)]
public partial class LeagueInvestigationMarker : Item
{
    public const int Art = 0x1F14; // the League rune's art (LeagueShop "rune" row)

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _siteKey;

    [Constructible]
    public LeagueInvestigationMarker() : this("")
    {
    }

    public LeagueInvestigationMarker(string siteKey) : base(Art)
    {
        _siteKey = siteKey ?? "";
        Hue = LeagueShop.LeagueHue;
        Movable = false;
    }

    public LeagueInvestigationSite? Site => LeagueInvestigationCatalog.FindSite(_siteKey);

    public override string DefaultName => "a League investigation marker";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Site is { } site)
        {
            list.Add($"Investigation site: {site.Name}");
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Site is not { } site)
        {
            from.SendMessage(0x22, "This marker names no investigation site.");
            return;
        }

        foreach (var e in LeagueInvestigationCatalog.Encounters)
        {
            if (e.SiteKey == site.Key)
            {
                from.SendMessage($"The League investigates {e.BossName} here, at {site.Name}. Use its warrant from your backpack.");
            }
        }

        if (!site.Enabled || !LeagueInvestigationCatalog.Settings.Enabled)
        {
            from.SendMessage("League investigations are not open yet.");
        }
    }
}
