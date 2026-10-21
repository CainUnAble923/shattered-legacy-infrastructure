// cc-P280: the League Register, P179's "book of books", built on Chase's answers of 2026-10-21 (shard-migration
// notes/cc-P179-league-design-pass.md, notes/cc-P280-league-register-build.md).
//
//   - Issued free by the League Registrar from Dull Copper (answer 2), one per character, blessed and bound to that
//     character. A re-issue replaces it: the older copy stops working and deletes itself (P179's rule for guild books).
//   - Guilds are bound in (answer 1). A guild that has a guild book today (the Society of Smiths' Smith Guild Book, the
//     Miners' Compact's Dispatch Ledger) binds by consuming that book from the backpack, and its key is recorded; a guild
//     with no book yet (P178 gives every guild one; it is held) binds by membership alone. Unbinding is free and gives the
//     book back, a fresh one, when the guild has a book. The keys live on CharacterLeagueData (v2).
//   - Capacity by League rank (answer 2): 2 at Dull Copper, 4 Bronze, 6 Verite, 9 Blaze, 12 Mythril, all 18 at Celestial.
//   - Membership is live (answer 9): a bound guild the character has left shows greyed out at the next open and is
//     dropped then, its slot freed, nothing refunded, no book issued.
//   - League actions by League rank (answer 4): Dull Copper the bound guilds' status (the Ledger); Bronze open a bound
//     guild's contracts; Verite turn in every ready order of the bound guilds at once; Blaze one repair pass (LeagueRepair).
//     Mythril and up: reserved. A guild's own actions are still gated by guild rank where they already are.
//
// Every number is a setting (LeagueRegisterSettings).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.LeagueRegister;

public enum LeagueAction
{
    Status,
    Contracts,
    TurnIn,
    Repair
}

public static class LeagueRegisterSystem
{
    // -- Guild books that bind (the guilds with a book today) -------------------------------------------------------

    /// <summary>The guild books there are today: the guild, the book's type, and how to make a fresh one.</summary>
    public static readonly IReadOnlyList<(string Key, Type Book, Func<Item> Make)> Books =
    [
        ("smithing", typeof(SmithGuildBook), () => new SmithGuildBook()),
        ("mining", typeof(CompactDispatchLedger), () => new CompactDispatchLedger())
    ];

    public static Type BookTypeFor(string key) => Books.FirstOrDefault(b => b.Key == key).Book;

    // -- Data -------------------------------------------------------------------------------------------------------

    private static CharacterLeagueData Data(Mobile m) =>
        m?.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateLeagueData(m.Serial) : null;

    public static IReadOnlyCollection<string> Bound(Mobile m) =>
        (IReadOnlyCollection<string>)Data(m)?.RegisterGuilds ?? Array.Empty<string>();

    public static int Rank(Mobile m) => ClusterFLeagueRanks.GetRank(m);

    public static int Capacity(Mobile m) => LeagueRegisterSettings.Capacity(m);

    /// <summary>Bound guilds the character still belongs to (the ones that count against capacity after the next open).</summary>
    public static int UsedSlots(Mobile m) => Bound(m).Count;

    // -- Issue -------------------------------------------------------------------------------------------------------

    public static string RefuseIssue(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct || !ClusterFLeagueSystem.IsJoined(ClusterFAccountPersistence.GetOrCreate(acct)))
        {
            return "Only citizens of the League are issued a Register.";
        }

        var need = LeagueRegisterSettings.IssueRank;
        return Rank(pm) < need ? $"The Register is issued at {ClusterFLeagueRanks.RankName(need)}." : null;
    }

    /// <summary>The Registrar's issue: a new Register into the pack; any older copy stops working and is deleted.</summary>
    public static LeagueRegister Issue(PlayerMobile pm, out string message)
    {
        message = RefuseIssue(pm);
        if (message != null || pm.Backpack == null)
        {
            message ??= "You have no backpack.";
            return null;
        }

        var data = Data(pm);
        if (data.RegisterSerial != 0 && World.FindItem((Serial)data.RegisterSerial) is LeagueRegister old)
        {
            old.Delete();
        }

        var register = new LeagueRegister(pm);
        data.RegisterSerial = (uint)register.Serial;
        pm.AddToBackpack(register);
        message = "The Registrar hands you your League Register.";
        return register;
    }

    public static bool IsCurrent(LeagueRegister register, Mobile m) =>
        register.Owner == m && Data(m)?.RegisterSerial == (uint)register.Serial;

    // -- Live membership (answer 9) ----------------------------------------------------------------------------------

    /// <summary>
    /// At each open: bound guilds the character has left. They are dropped from the Register now, freeing their slots,
    /// and the open that finds them shows them greyed out; the next open no longer lists them. Nothing is refunded.
    /// </summary>
    public static List<string> DropLeft(Mobile m)
    {
        var data = Data(m);
        var left = data?.RegisterGuilds.Where(k => !ClusterFGuildSystem.IsJoined(m, k)).ToList() ?? new List<string>();

        foreach (var key in left)
        {
            data.RegisterGuilds.Remove(key);
        }

        return left;
    }

    // -- Bind and unbind (answer 1) ----------------------------------------------------------------------------------

    public static string RefuseBind(PlayerMobile pm, string key)
    {
        var data = Data(pm);
        if (data == null)
        {
            return "Account data unavailable.";
        }

        if (ClusterFGuildSystem.GetDef(key) == null)
        {
            return "There is no such guild.";
        }

        if (!ClusterFGuildSystem.IsJoined(pm, key))
        {
            return "You are not a member of that guild.";
        }

        if (data.RegisterGuilds.Contains(key))
        {
            return "That guild is already bound.";
        }

        if (data.RegisterGuilds.Count >= Capacity(pm))
        {
            return $"Your Register holds {Capacity(pm)} guild{(Capacity(pm) == 1 ? "" : "s")} at your League rank.";
        }

        if (BookTypeFor(key) is { } book && FindBook(pm, book) == null)
        {
            return "Bring that guild's book in your backpack: binding takes it.";
        }

        return null;
    }

    private static Item FindBook(PlayerMobile pm, Type book) =>
        pm.Backpack?.Items.FirstOrDefault(i => !i.Deleted && book.IsInstanceOfType(i));

    /// <summary>Binds the guild: its book (when it has one) is taken from the pack and deleted, and its key recorded.</summary>
    public static bool Bind(PlayerMobile pm, string key, out string message)
    {
        message = RefuseBind(pm, key);
        if (message != null)
        {
            return false;
        }

        if (BookTypeFor(key) is { } type)
        {
            var book = FindBook(pm, type);
            if (book is Container { TotalItems: > 0 })
            {
                message = "Empty the book first: its deeds would be lost.";
                return false;
            }

            book.Delete();
        }

        Data(pm).RegisterGuilds.Add(key);
        message = $"{ClusterFGuildSystem.GetDef(key).Name} is bound into your Register.";
        return true;
    }

    /// <summary>Unbinds the guild, free: its slot is freed and, when the guild has a book, a fresh one comes back.</summary>
    public static bool Unbind(PlayerMobile pm, string key, out string message)
    {
        var data = Data(pm);
        if (data == null || !data.RegisterGuilds.Remove(key))
        {
            message = "That guild is not bound.";
            return false;
        }

        var name = ClusterFGuildSystem.GetDef(key)?.Name ?? key;
        var make = Books.FirstOrDefault(b => b.Key == key).Make;
        if (make != null && ClusterFGuildSystem.IsJoined(pm, key))
        {
            pm.AddToBackpack(make());
            message = $"{name} is taken out of your Register, and its book is back in your pack.";
        }
        else
        {
            message = $"{name} is taken out of your Register.";
        }

        return true;
    }

    // -- League actions (answer 4) -----------------------------------------------------------------------------------

    public static int ActionRank(LeagueAction action) => action switch
    {
        LeagueAction.Status    => LeagueRegisterSettings.StatusRank,
        LeagueAction.Contracts => LeagueRegisterSettings.ContractsRank,
        LeagueAction.TurnIn    => LeagueRegisterSettings.TurnInRank,
        _                      => LeagueRegisterSettings.RepairRank
    };

    public static string ActionName(LeagueAction action) => action switch
    {
        LeagueAction.Status    => "Status of every bound guild",
        LeagueAction.Contracts => "Open a bound guild's contracts",
        LeagueAction.TurnIn    => "Turn in every ready order",
        _                      => "League repair"
    };

    public static string RefuseAction(Mobile m, LeagueAction action)
    {
        var need = ActionRank(action);
        return Rank(m) < need ? $"{ActionName(action)} needs {ClusterFLeagueRanks.RankName(need)}." : null;
    }

    /// <summary>Bronze: a bound guild's contract ledger, from anywhere.</summary>
    public static bool OpenContracts(PlayerMobile pm, string key, out string message)
    {
        message = RefuseAction(pm, LeagueAction.Contracts);
        if (message == null && !Bound(pm).Contains(key))
        {
            message = "That guild is not bound into your Register.";
        }

        if (message == null && !ClusterFGuildSystem.IsJoined(pm, key))
        {
            message = "You are no longer a member of that guild.";
        }

        if (message != null)
        {
            return false;
        }

        pm.SendGump(new GuildContractLedgerGump(pm, key));
        return true;
    }

    /// <summary>
    /// Verite: every ready order of every bound guild at once: each active work order whose materials are in the pack and
    /// bank (GuildContractLedgerGump.TurnIn, the ledger's own turn-in), and, with the Society of Smiths bound, every
    /// complete smith deed in the backpack, paid the Society's way (ClusterFSmithBODPayout.Pay, by the member's setting;
    /// "ask" banks). Returns how many were turned in.
    /// </summary>
    public static int TurnInAll(PlayerMobile pm, out string message)
    {
        message = RefuseAction(pm, LeagueAction.TurnIn);
        if (message != null || pm.Account is not IAccount acct)
        {
            return 0;
        }

        var guild = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial);
        var done = 0;

        foreach (var key in Bound(pm).Where(k => ClusterFGuildSystem.IsJoined(pm, k)).ToList())
        {
            var entries = guild.ActiveWorkOrders
                .Where(e => e.GuildKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var entry in entries)
            {
                if (GuildContractLedgerGump.TurnIn(pm, guild, key, entry, false))
                {
                    done++;
                }
            }

            if (key == "smithing" && pm.Backpack != null)
            {
                var cashOut = ClusterFSmithBODPayout.GetMode(pm) == SmithTurnInMode.CashOut;
                var deeds = pm.Backpack.Items
                    .Where(i => i is SmallSmithBOD { Complete: true } or LargeSmithBOD { Complete: true })
                    .ToList();

                foreach (var deed in deeds)
                {
                    if (ClusterFSmithBODPayout.Pay(pm, deed, cashOut, null))
                    {
                        done++;
                    }
                }
            }
        }

        message = done == 0 ? "No bound guild has an order ready to turn in." : $"Turned in {done} order{(done == 1 ? "" : "s")}.";
        return done;
    }

    /// <summary>Blaze: one repair pass (LeagueRepair.RepairAll), worn, carried and banked.</summary>
    public static int RepairAll(PlayerMobile pm, out string message)
    {
        message = RefuseAction(pm, LeagueAction.Repair);
        if (message != null)
        {
            return 0;
        }

        var count = LeagueRepair.RepairAll(pm);
        message = LeagueRepair.Describe(count);
        return count;
    }
}

[SerializationGenerator(0, false)]
public partial class LeagueRegister : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    [Constructible]
    public LeagueRegister() : this(null)
    {
    }

    public LeagueRegister(Mobile owner) : base(0x0FF1)
    {
        _owner = owner;
        Name = "a League Register";
        Hue = 0x8A5;
        LootType = LootType.Blessed;
        Weight = 1.0;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (_owner != null)
        {
            list.Add($"Bound to {_owner.Name}");
        }
    }

    public override bool OnDragLift(Mobile from) => from == _owner || from.AccessLevel > AccessLevel.Player;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || from != _owner)
        {
            from.SendMessage(0x22, "This Register belongs to someone else.");
            return;
        }

        if (!LeagueRegisterSystem.IsCurrent(this, from))
        {
            from.SendMessage(0x22, "This Register has been replaced by a newer copy, and crumbles.");
            Delete();
            return;
        }

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        LeagueRegisterGump.Open(pm, LeagueRegisterGump.Page.Guilds);
    }
}
