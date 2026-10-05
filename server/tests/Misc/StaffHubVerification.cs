// StaffHubVerification.cs
//
// cc-P51 Parts B to E (F-30, ours). The Staff Hub: customizations StaffHub/ClusterFStaffHub.cs (what it shows and does),
// StaffHubGump.cs (the gump), ClusterFStaffHubSpots.cs (saved spots). Notes in shard-migration
// notes/cc-P51-staff-hub-and-raptor-pack.md.
//
// The Commands facts run against probe commands this file registers (names starting AAP51, so they sort first in their
// category) and removes again, so no fact runs a real world command. One Grant fact uses the real [CompactGivePickaxe.
//
// Facts, by part:
//   B1 every registered command with [ShardCommand] outside Player is listed under its category; a stock command and a
//      registered command without the attribute are not.
//   B2 a TestOnly row cannot run when TestCenter.Enabled is false (and can when it is true), and draws no Run button.
//   B3 a WorldRemoval row is hidden from, and refused to, a GameMaster; an Administrator sees and runs it.
//   B4 Run on a confirm row does nothing until confirmed (through the argument prompt and the confirm gump).
//   B5 Dry run passes exactly the declared word.
//   B6 a Counselor gets no gump from [SL; a GameMaster does.
//   B7 a command whose [Usage] shows arguments opens the prompt, prefilled with them; an unedited template is refused.
//   C1 the panel shows the fixture character's values, and reading it creates no account record.
//   C2 search finds an offline character.
//   C3 a Grant button applies to the selected character, not to the staff member (the real CompactGivePickaxe).
//   C4 offline Go is disabled (no button) and refused.
//   D1 every shard place resolves to its seeder's member, and each anchor is the seeder's own data.
//   D2 Save here then a save and load keeps the spot; delete removes it; another staff account does not see it.
//   D3 Go to with a bad facet name refuses cleanly, as does bad x/y/z; a good one moves the staff member.
//   D4 Recent caps at 5, newest first.
//   E1 no button ID means two things: each tab's own IDs are disjoint from every other tab's, none repeats in a gump.
//   E2 a stale gump fails safely: a command removed after drawing, a character who logged out or was deleted.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Engines.MLQuests.Definitions;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StaffHubVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<Account> _accounts = [];

    public StaffHubVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        // The test host runs no Configure of ours: register what the facts invoke (each Register is idempotent).
        ClusterFStaffHub.Configure();
        ClusterFCompactAdminTools.Configure();
        RegisterProbes();
        Invoked.Clear();
    }

    public void Dispose()
    {
        foreach (var name in ProbeNames)
        {
            CommandSystem.Entries.Remove(name);
        }

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                if (account[i] is { } m)
                {
                    if (m.NetState is { } ns)
                    {
                        m.NetState = null;
                        ns.Mobile = null;
                        ns.Dispose();
                    }

                    m.Delete();
                }
            }

            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- probes

    private static readonly List<(string Name, string Args, Mobile Target)> Invoked = [];

    private const string ProbeRemoval = "AAP51ProbeRemoval";
    private const string ProbeTestOnly = "AAP51ProbeTestOnly";
    private const string ProbeArgs = "AAP51ProbeArgs";
    private const string ProbeGrant = "AAP51ProbeGrant";
    private const string ProbeUndeclared = "AAP51ProbeUndeclared";

    private static readonly string[] ProbeNames = [ProbeRemoval, ProbeTestOnly, ProbeArgs, ProbeGrant, ProbeUndeclared];

    private static void RegisterProbes()
    {
        // Registered at GameMaster so the hub's own Administrator rule for removal is what refuses a GameMaster.
        CommandSystem.Register(ProbeRemoval, AccessLevel.GameMaster, Removal_OnCommand);
        CommandSystem.Register(ProbeTestOnly, AccessLevel.GameMaster, TestOnly_OnCommand);
        CommandSystem.Register(ProbeArgs, AccessLevel.GameMaster, Args_OnCommand);
        CommandSystem.Register(ProbeGrant, AccessLevel.GameMaster, Grant_OnCommand);
        CommandSystem.Register(ProbeUndeclared, AccessLevel.GameMaster, Undeclared_OnCommand);
    }

    [Usage("AAP51ProbeRemoval [dryrun]")]
    [Description("cc-P51 probe: a world removal.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.TestFirst,
        DryRun = "dryrun", Summary = "Probe removal.")]
    private static void Removal_OnCommand(CommandEventArgs e) => Invoked.Add((e.Command, e.ArgString, null));

    [Usage("AAP51ProbeTestOnly")]
    [Description("cc-P51 probe: test shard only.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void TestOnly_OnCommand(CommandEventArgs e) => Invoked.Add((e.Command, e.ArgString, null));

    [Usage("AAP51ProbeArgs <word> [count]")]
    [Description("cc-P51 probe: takes arguments.")]
    [ShardCommand(CommandCategory.Diagnostic)]
    private static void Args_OnCommand(CommandEventArgs e) => Invoked.Add((e.Command, e.ArgString, null));

    [Usage("AAP51ProbeGrant")]
    [Description("cc-P51 probe: a grant that targets.")]
    [ShardCommand(CommandCategory.Grant)]
    private static void Grant_OnCommand(CommandEventArgs e) =>
        e.Mobile.BeginTarget(-1, false, Server.Targeting.TargetFlags.None,
            (from, targeted) => Invoked.Add((ProbeGrant, "", targeted as Mobile)));

    [Usage("AAP51ProbeUndeclared")]
    [Description("cc-P51 probe: no [ShardCommand].")]
    private static void Undeclared_OnCommand(CommandEventArgs e) => Invoked.Add((e.Command, e.ArgString, null));

    private static int Calls(string name) => Invoked.Count(i => i.Name.InsensitiveEquals(name));

    // ---------------------------------------------------------------- helpers

    private sealed class TestCenterSwitch : IDisposable
    {
        private static readonly MethodInfo Setter =
            typeof(TestCenter).GetProperty(nameof(TestCenter.Enabled))!.GetSetMethod(true)!;

        private readonly bool _was;

        public TestCenterSwitch(bool on)
        {
            _was = TestCenter.Enabled;
            Setter.Invoke(null, [on]);
        }

        public void Dispose() => Setter.Invoke(null, [_was]);
    }

    private Account NewAccount()
    {
        var a = new Account($"p51h{Guid.NewGuid():N}"[..16], "p51-test-only");
        _accounts.Add(a);
        return a;
    }

    // A character on its own account, standing on Trammel, optionally online (a test NetState with the account).
    private PlayerMobile NewCharacter(AccessLevel level = AccessLevel.Player, bool online = true, string name = null,
        Account account = null)
    {
        account ??= NewAccount();
        var pm = new PlayerMobile { Player = true, Race = Race.Human, AccessLevel = level, Name = name ?? $"P51 {Guid.NewGuid():N}"[..12] };
        pm.AddItem(new Backpack());
        var slot = 0;
        while (account[slot] != null)
        {
            slot++;
        }

        account[slot] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);

        if (online)
        {
            var ns = PacketTestUtilities.CreateTestNetState();
            pm.NetState = ns;
            ns.Mobile = pm;
            ns.Account = account; // a connection with no account has a 4 KiB send ring (cc-P30)
        }
        else
        {
            pm.Internalize();
        }

        return pm;
    }

    private static StaffHubGump Hub(Mobile staff, StaffHubState state) => new(staff, state);

    private static IEnumerable<int> Buttons(Gump g) => g.Entries.OfType<GumpButton>().Select(b => b.ButtonID);

    // ---------------------------------------------------------------- B1

    [Fact]
    public void EveryShardCommandIsListedUnderItsCategoryAndOthersAreNot()
    {
        var owner = NewCharacter(AccessLevel.Owner, online: false);

        // The test host runs only a few Configure calls, so most of our commands are not registered here. Register every
        // handler in UOContent that carries [ShardCommand] and is not registered yet, under a synthetic name, so the fact
        // covers all of them (CommandDeclarationVerification proves every real registration carries one).
        var synthetic = new List<string>();
        var registered = CommandSystem.Entries.Values.Select(e => e.Handler?.Method).Where(m => m != null).ToHashSet();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var handlers = typeof(ClusterFStaffHub).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(all))
            .Where(m => m.GetCustomAttribute<ShardCommandAttribute>() != null)
            .ToList();

        foreach (var m in handlers.Where(m => !registered.Contains(m)))
        {
            var name = $"P51All{synthetic.Count}{m.Name}";
            CommandSystem.Register(name, AccessLevel.GameMaster, (CommandEventHandler)Delegate.CreateDelegate(typeof(CommandEventHandler), m));
            synthetic.Add(name);
        }

        _out.WriteLine($"[ShardCommand] handlers in UOContent: {handlers.Count} " +
                       $"({handlers.Count(m => m.GetCustomAttribute<ShardCommandAttribute>().Category == CommandCategory.Player)} Player); " +
                       $"registered here by the fact: {synthetic.Count}");

        try
        {
            AssertEveryShardCommandListed(owner);
        }
        finally
        {
            foreach (var name in synthetic)
            {
                CommandSystem.Entries.Remove(name);
            }
        }
    }

    private void AssertEveryShardCommandListed(Mobile owner)
    {
        var listed = ClusterFStaffHub.AllCommands();
        var missing = new List<string>();
        var checkedCount = 0;

        foreach (var entry in CommandSystem.Entries.Values)
        {
            var method = entry.Handler?.Method;
            var decl = method?.GetCustomAttribute<ShardCommandAttribute>();
            if (decl == null || decl.Category == CommandCategory.Player || entry.Command.InsensitiveEquals(ClusterFStaffHub.CommandName))
            {
                continue;
            }

            if (method.GetCustomAttribute<AliasesAttribute>()?.Aliases.Any(a => a.InsensitiveEquals(entry.Command)) == true)
            {
                continue;
            }

            checkedCount++;
            if (!ClusterFStaffHub.CommandsIn(owner, decl.Category).Any(c => c.Name == entry.Command))
            {
                missing.Add($"{entry.Command} ({decl.Category})");
            }
        }

        _out.WriteLine($"commands with [ShardCommand] outside Player (and not [SL): {checkedCount}; hub lists {listed.Count}");
        foreach (var cat in ClusterFStaffHub.Categories)
        {
            var names = ClusterFStaffHub.CommandsIn(owner, cat).Select(c => c.Name).ToList();
            _out.WriteLine($"  {ClusterFStaffHub.CategoryLabel(cat)} ({names.Count}): {string.Join(", ", names)}");
        }

        Assert.Empty(missing);
        Assert.Equal(checkedCount, listed.Count);

        // A stock command without the attribute, a registered one without it, and the Player category are not listed.
        Assert.True(CommandSystem.Entries.ContainsKey(ProbeUndeclared));
        Assert.DoesNotContain(listed, c => c.Name == ProbeUndeclared);
        Assert.DoesNotContain(listed, c => c.Category == CommandCategory.Player);
        if (CommandSystem.Entries.ContainsKey("Props"))
        {
            Assert.DoesNotContain(listed, c => c.Name == "Props");
        }

        Assert.DoesNotContain(listed, c => c.Name.InsensitiveEquals(ClusterFStaffHub.CommandName));
        Assert.Contains(listed, c => c.Name == "CompactGivePickaxe" && c.Category == CommandCategory.Grant);
    }

    // ---------------------------------------------------------------- B2

    [Fact]
    public void ATestOnlyRowCannotRunOnLive()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var state = new StaffHubState { Category = CommandCategory.DevTool };

        using (new TestCenterSwitch(false))
        {
            Assert.False(ClusterFStaffHub.Run(gm, ProbeTestOnly, ""));
            Assert.Equal(0, Calls(ProbeTestOnly));

            var g = Hub(gm, state);
            var row = g.Rows.ToList().IndexOf(ProbeTestOnly);
            Assert.True(row >= 0, "the probe is listed on DevTool's first page");
            Assert.DoesNotContain(StaffHubGump.BtnRunBase + row, Buttons(g));

            g.Respond(gm, StaffHubGump.BtnRunBase + row); // a stale or forged press is refused too
            Assert.Equal(0, Calls(ProbeTestOnly));
        }

        using (new TestCenterSwitch(true))
        {
            var g = Hub(gm, state);
            var row = g.Rows.ToList().IndexOf(ProbeTestOnly);
            Assert.Contains(StaffHubGump.BtnRunBase + row, Buttons(g));
            Assert.True(ClusterFStaffHub.Run(gm, ProbeTestOnly, ""));
            Assert.Equal(1, Calls(ProbeTestOnly));
        }
    }

    // ---------------------------------------------------------------- B3

    [Fact]
    public void AWorldRemovalRowIsHiddenAndRefusedBelowAdministrator()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var admin = NewCharacter(AccessLevel.Administrator);

        Assert.DoesNotContain(ClusterFStaffHub.CommandsIn(gm, CommandCategory.WorldRemoval), c => c.Name == ProbeRemoval);
        Assert.Empty(ClusterFStaffHub.CommandsIn(gm, CommandCategory.WorldRemoval));
        Assert.False(ClusterFStaffHub.Run(gm, ProbeRemoval, ""));
        Assert.False(ClusterFStaffHub.DryRun(gm, ProbeRemoval));
        Assert.Equal(0, Calls(ProbeRemoval));

        Assert.Contains(ClusterFStaffHub.CommandsIn(admin, CommandCategory.WorldRemoval), c => c.Name == ProbeRemoval);
        Assert.True(ClusterFStaffHub.DryRun(admin, ProbeRemoval));
        Assert.Equal(1, Calls(ProbeRemoval));
    }

    // ---------------------------------------------------------------- B4

    [Fact]
    public void RunOnAConfirmRowDoesNothingUntilConfirmed()
    {
        var admin = NewCharacter(AccessLevel.Administrator);
        var g = Hub(admin, new StaffHubState { Category = CommandCategory.WorldRemoval });
        var row = g.Rows.ToList().IndexOf(ProbeRemoval);
        Assert.True(row >= 0);

        // Its usage shows "[dryrun]", so Run asks for arguments first.
        g.Respond(admin, StaffHubGump.BtnRunBase + row);
        Assert.Equal(0, Calls(ProbeRemoval));
        var prompt = admin.FindGump<StaffHubPromptGump>();
        Assert.NotNull(prompt);
        Assert.Equal(ProbeRemoval, prompt.Command);

        // No arguments: the confirm comes up, still nothing run.
        prompt.Respond(admin, true, "");
        Assert.Equal(0, Calls(ProbeRemoval));
        var confirm = admin.FindGump<StaffHubConfirmGump>();
        Assert.NotNull(confirm);

        // No: nothing.
        confirm.Respond(admin, false);
        Assert.Equal(0, Calls(ProbeRemoval));

        // Yes: it runs, with no arguments.
        new StaffHubConfirmGump(new StaffHubState(), StaffHubPrompt.Run, ProbeRemoval, "").Respond(admin, true);
        Assert.Equal(1, Calls(ProbeRemoval));
        Assert.Equal("", Invoked.Single(i => i.Name.InsensitiveEquals(ProbeRemoval)).Args);
    }

    // ---------------------------------------------------------------- B5

    [Fact]
    public void DryRunPassesExactlyTheDeclaredWord()
    {
        var admin = NewCharacter(AccessLevel.Administrator);
        var g = Hub(admin, new StaffHubState { Category = CommandCategory.WorldRemoval });
        var row = g.Rows.ToList().IndexOf(ProbeRemoval);
        Assert.Contains(StaffHubGump.BtnDryRunBase + row, Buttons(g));

        g.Respond(admin, StaffHubGump.BtnDryRunBase + row);

        var call = Invoked.Single(i => i.Name.InsensitiveEquals(ProbeRemoval));
        Assert.Equal("dryrun", call.Args);
        Assert.Null(admin.FindGump<StaffHubConfirmGump>()); // a dry run needs no confirm

        // A row with no declared dry run draws no Dry run button and refuses one.
        var d = Hub(admin, new StaffHubState { Category = CommandCategory.Diagnostic });
        var argsRow = d.Rows.ToList().IndexOf(ProbeArgs);
        Assert.DoesNotContain(StaffHubGump.BtnDryRunBase + argsRow, Buttons(d));
        Assert.False(ClusterFStaffHub.DryRun(admin, ProbeArgs));
    }

    // ---------------------------------------------------------------- B6

    [Fact]
    public void ACounselorGetsNoGumpAndAGameMasterDoes()
    {
        var counselor = NewCharacter(AccessLevel.Counselor);
        var gm = NewCharacter(AccessLevel.GameMaster);

        CommandSystem.Handle(counselor, $"{CommandSystem.Prefix}SL");
        ClusterFStaffHub.Open(counselor);
        Assert.False(counselor.HasGump<StaffHubGump>());

        CommandSystem.Handle(gm, $"{CommandSystem.Prefix}SL");
        Assert.True(gm.HasGump<StaffHubGump>());
    }

    // ---------------------------------------------------------------- B7

    [Fact]
    public void ACommandWithArgumentsOpensThePromptPrefilledWithThem()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var c = ClusterFStaffHub.Find(ProbeArgs);
        Assert.Equal("<word> [count]", c.Arguments);
        Assert.Equal("[amount]", ClusterFStaffHub.ArgumentsOf("CompactStanding", "[CompactStanding [amount]"));
        Assert.Equal("", ClusterFStaffHub.ArgumentsOf("CompactGivePickaxe", "[CompactGivePickaxe"));

        var g = Hub(gm, new StaffHubState { Category = CommandCategory.Diagnostic });
        g.Respond(gm, StaffHubGump.BtnRunBase + g.Rows.ToList().IndexOf(ProbeArgs));
        var prompt = gm.FindGump<StaffHubPromptGump>();
        Assert.NotNull(prompt);
        Assert.Equal("<word> [count]", prompt.Prefill);
        Assert.Contains(prompt.Entries.OfType<GumpTextEntry>(), t => t.InitialText == "<word> [count]");

        prompt.Respond(gm, true, "<word> [count]"); // unedited template: refused, prompt again
        Assert.Equal(0, Calls(ProbeArgs));

        gm.FindGump<StaffHubPromptGump>().Respond(gm, true, "hello 3");
        Assert.Equal("hello 3", Invoked.Single(i => i.Name.InsensitiveEquals(ProbeArgs)).Args);
    }

    // ---------------------------------------------------------------- C1

    [Fact]
    public void ThePanelShowsTheFixtureCharactersValuesAndCreatesNothing()
    {
        var pm = NewCharacter(online: false, name: "Panel Fixture");
        var acct = (Account)pm.Account;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);

        guild.JoinedGuilds.Add("smithing");
        guild.JoinedGuilds.Add("mining");
        guild.GuildReputation["smithing"] = 5_000;
        guild.GuildReputation["mining"] = 15_000;
        data.SetFlag(ClusterFLeagueSystem.FlagJoined);
        ClusterFLeagueRanks.SetRank(pm, 3);
        data.Renown = 1_234;
        data.LifetimeRenown = 2_000;
        ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe", "test");
        data.OreDiscoveries["Platinum"] = new OreDiscoveryEntry("Platinum");
        data.OreDiscoveries["Toxic"] = new OreDiscoveryEntry("Toxic");
        data.OreDiscoveries["DullCopper"] = new OreDiscoveryEntry("DullCopper"); // a vanilla ore: not one of the eight
        data.WoodDiscoveries["Ironwood"] = new WoodDiscoveryEntry("Ironwood");
        data.WoodDiscoveries["OakWood"] = new WoodDiscoveryEntry("OakWood");     // a vanilla wood: not one of the eight
        var bank = pm.BankBox;
        bank.DropItem(new Gold(10));
        bank.DropItem(new IronIngot(5));
        bank.DropItem(new Bag());

        var rows = ClusterFStaffHub.PlayerRows(pm).ToDictionary(r => r.Label, r => r.Value);
        foreach (var (label, value) in rows)
        {
            _out.WriteLine($"{label,-20} {value}");
        }

        Assert.Equal($"{ClusterFGuildSystem.GetDef("mining")?.Name ?? "mining"}: Surveyor and 1 more", rows["Guild standing"]);
        Assert.Equal(ClusterFLeagueRanks.RankName(3), rows["League rank"]);
        Assert.NotEqual("Unregistered", rows["League rank"]);
        Assert.Equal("1,234 (lifetime 2,000)", rows["Renown"]);
        Assert.Equal("Surveyor (15,000)", rows["Compact rank"]);
        Assert.Equal("1: legacy.jacobs_pickaxe", rows["Restoration unlocks"]);
        Assert.Equal("2 of 8", rows["Ores discovered"]);
        Assert.Equal("1 of 8", rows["Woods discovered"]);
        Assert.StartsWith("skills ", rows["Skill / stat caps"]);
        Assert.Equal($"{bank.TotalItems} of {ClusterFBankLimit.GetMaxItems(pm)}", rows["Bank items"]);
        Assert.Equal(3, bank.TotalItems);
        Assert.Equal(9, rows.Count);

        // Read only: a character with no record still has none after the panel reads it.
        var fresh = NewCharacter(online: false);
        Assert.Null(ClusterFAccountPersistence.Get(fresh.Account));
        var freshRows = ClusterFStaffHub.PlayerRows(fresh);
        Assert.Null(ClusterFAccountPersistence.Get(fresh.Account));
        Assert.Equal("no guild", freshRows.Single(r => r.Label == "Guild standing").Value);
        Assert.Equal("0 of 8", freshRows.Single(r => r.Label == "Ores discovered").Value);
    }

    // ---------------------------------------------------------------- C2

    [Fact]
    public void SearchFindsAnOfflineCharacter()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var unique = $"Zq{Guid.NewGuid():N}"[..10];
        var offline = NewCharacter(online: false, name: unique);
        Assert.False(ClusterFStaffHub.IsOnline(offline));

        var found = ClusterFStaffHub.FindCharacters(unique);
        Assert.Single(found);
        Assert.Same(offline, found[0]);

        // Through the gump: one match selects it.
        var state = new StaffHubState { Tab = StaffHubTab.Player };
        Hub(gm, state).Respond(gm, StaffHubGump.BtnPlayerSearch, search: unique.ToLowerInvariant());
        Assert.Same(offline, state.Selected);

        // Two matches list for picking.
        var twin = NewCharacter(online: false, name: unique + "b");
        state = new StaffHubState { Tab = StaffHubTab.Player };
        Hub(gm, state).Respond(gm, StaffHubGump.BtnPlayerSearch, search: unique);
        Assert.Null(state.Selected);
        Assert.Equal(new Mobile[] { offline, twin }, state.Matches);
        var picker = Hub(gm, state);
        Assert.Equal(2, picker.Matches.Count);
        picker.Respond(gm, StaffHubGump.BtnPickBase + 1);
        Assert.Same(twin, state.Selected);
    }

    // ---------------------------------------------------------------- C3

    [Fact]
    public void AGrantButtonAppliesToTheSelectedCharacterNotTheStaffMember()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var player = NewCharacter();
        var state = new StaffHubState { Tab = StaffHubTab.Player, Selected = player };

        var g = Hub(gm, state);
        _out.WriteLine($"grant buttons: {string.Join(", ", g.Grants)}");
        var row = g.Grants.ToList().IndexOf("CompactGivePickaxe");
        Assert.True(row >= 0);
        Assert.Contains(StaffHubGump.BtnGrantBase + row, Buttons(g));

        g.Respond(gm, StaffHubGump.BtnGrantBase + row);

        Assert.NotNull(player.Backpack.FindItemByType<JacobsPickaxe>());
        Assert.Null(gm.Backpack.FindItemByType<JacobsPickaxe>());
        Assert.Null(gm.Target); // the cursor was answered, not left up

        // The probe grant reports the character its cursor received.
        Assert.True(ClusterFStaffHub.RunGrant(gm, ProbeGrant, "", player));
        Assert.Same(player, Invoked.Single(i => i.Name == ProbeGrant).Target);

        // Offline: the cursor stays up for the staff member, aimed at nobody.
        var away = NewCharacter(online: false);
        Assert.False(ClusterFStaffHub.RunGrant(gm, ProbeGrant, "", away));
        Assert.NotNull(gm.Target);
        Assert.Single(Invoked, i => i.Name == ProbeGrant);
        gm.Target.Cancel(gm, Server.Targeting.TargetCancelType.Canceled);
        gm.ClearTarget();
    }

    // ---------------------------------------------------------------- C4

    [Fact]
    public void OfflineGoIsDisabled()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var offline = NewCharacter(online: false);
        var online = NewCharacter();

        var off = Buttons(Hub(gm, new StaffHubState { Tab = StaffHubTab.Player, Selected = offline })).ToList();
        Assert.DoesNotContain(StaffHubGump.BtnPlayerGo, off);
        Assert.DoesNotContain(StaffHubGump.BtnPlayerBring, off);
        Assert.Contains(StaffHubGump.BtnPlayerProps, off);

        var on = Buttons(Hub(gm, new StaffHubState { Tab = StaffHubTab.Player, Selected = online })).ToList();
        Assert.Contains(StaffHubGump.BtnPlayerGo, on);
        Assert.Contains(StaffHubGump.BtnPlayerBring, on);

        var before = gm.Location;
        Assert.False(ClusterFStaffHub.GoToPlayer(gm, offline));
        Assert.False(ClusterFStaffHub.BringPlayer(gm, offline));
        Assert.Equal(before, gm.Location);
        Assert.Same(Map.Internal, offline.Map);
    }

    // ---------------------------------------------------------------- D1

    [Fact]
    public void EveryShardPlaceResolvesToItsSeedersMember()
    {
        var places = ClusterFStaffHub.ShardPlaces().ToDictionary(p => p.Name);
        foreach (var p in places.Values)
        {
            _out.WriteLine($"{p.Name,-24} {p.Facet,-8} {p.Location} {(p.Available ? "" : p.Unavailable ?? "not loaded")}");
        }

        Assert.Equal(ClusterFNewHavenSeeder.BankAnchor, places["New Haven bank"].Location);
        Assert.Same(ClusterFNewHavenSeeder.Facet, places["New Haven bank"].Map);
        Assert.Equal(ClusterFMineCampSeeder.Anchor, places["Mine camp"].Location);
        Assert.Same(ClusterFMineCampSeeder.Facet, places["Mine camp"].Map);
        Assert.Equal(ClusterFRegistrarOfficeSeeder.Anchor, places["League Registrar office"].Location);
        Assert.Same(ClusterFRegistrarOfficeSeeder.Facet, places["League Registrar office"].Map);
        Assert.Equal(ClusterFFountainOfFortuneSeeder.Location, places["Fountain of Fortune"].Location);
        Assert.Same(ClusterFFountainOfFortuneSeeder.Facet, places["Fountain of Fortune"].Map);
        Assert.Equal(ClusterFRoyalCitySeeder.MoongateLocation, places["Royal City"].Location);
        Assert.Same(ClusterFRoyalCitySeeder.Facet, places["Royal City"].Map);
        Assert.False(places["Dungeon entrance"].Available);
        Assert.Equal("after P36", places["Dungeon entrance"].Unavailable);
        Assert.Contains(ClusterFStaffHub.GuildHallsRow, places.Keys);

        // Each anchor is the seeder's own data, not a copy of its numbers.
        Assert.Equal(ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(SarsmeaSmythe)).GetLocation(), ClusterFNewHavenSeeder.BankAnchor);
        Assert.Equal(ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(LeagueRegistrar)).GetLocation(), ClusterFRegistrarOfficeSeeder.Anchor);
        Assert.Equal(ClusterFMineCampLayout.Npcs.Single(n => n.TypeName == nameof(MinersCompactLiaison)).To, ClusterFMineCampSeeder.Anchor);

        var halls = ClusterFStaffHub.GuildHalls();
        var seeded = GuildLocations.All.Where(l => l.Seeded).ToList();
        Assert.Equal(seeded.Count, halls.Count);
        Assert.Equal(seeded.Select(l => l.Point), halls.Select(h => h.Location));
        Assert.True(halls.Count > 0);
        _out.WriteLine($"guild halls: {halls.Count}: {string.Join("; ", halls.Select(h => $"{h.Name} {h.Location}"))}");

        // The gump's Go on a place moves the staff member there, on its facet.
        var gm = NewCharacter(AccessLevel.GameMaster);
        var g = Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel });
        var bankRow = g.Places.ToList().FindIndex(p => p.Name == "New Haven bank");
        g.Respond(gm, StaffHubGump.BtnPlaceBase + bankRow);
        Assert.Equal(ClusterFNewHavenSeeder.BankAnchor, gm.Location);
        Assert.Same(Map.Trammel, gm.Map);
    }

    // ---------------------------------------------------------------- D2

    private static byte[] SaveSpots()
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        ClusterFStaffHubSpots.WriteTo(writer);
        writer.Flush();
        return buffer;
    }

    [Fact]
    public void ASavedSpotSurvivesASaveAndLoadAndBelongsToItsAccount()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var other = NewCharacter(AccessLevel.GameMaster);
        gm.MoveToWorld(new Point3D(1400, 1600, 10), Map.Felucca);

        Assert.True(ClusterFStaffHubSpots.SaveHere(gm, "  Pit stop ", out var message), message);
        Assert.False(ClusterFStaffHubSpots.SaveHere(gm, "   ", out _));
        Assert.False(ClusterFStaffHubSpots.SaveHere(gm, new string('x', ClusterFStaffHubSpots.MaxNameLength + 1), out _));

        var saved = SaveSpots();
        Assert.True(ClusterFStaffHubSpots.Delete(gm, 0, "Pit stop"));
        Assert.Empty(ClusterFStaffHubSpots.SpotsOf(gm));

        ClusterFStaffHubSpots.ReadFrom(new BufferReader(saved));
        var spot = Assert.Single(ClusterFStaffHubSpots.SpotsOf(gm));
        Assert.Equal("Pit stop", spot.Name);
        Assert.Equal(new Point3D(1400, 1600, 10), spot.Location);
        Assert.Same(Map.Felucca, spot.Map);

        // Another staff account does not see it.
        Assert.Empty(ClusterFStaffHubSpots.SpotsOf(other));
        Assert.Empty(Hub(other, new StaffHubState { Tab = StaffHubTab.Travel }).Spots);

        // A second character on the same account does.
        var alt = NewCharacter(AccessLevel.GameMaster, account: (Account)gm.Account);
        Assert.Single(ClusterFStaffHubSpots.SpotsOf(alt));

        // Through the gump: its delete removes it; a stale delete (the name changed underneath) removes nothing.
        var g = Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel });
        Assert.Single(g.Spots);
        Assert.False(ClusterFStaffHubSpots.Delete(gm, 0, "Not its name"));
        g.Respond(gm, StaffHubGump.BtnSpotDeleteBase);
        Assert.Empty(ClusterFStaffHubSpots.SpotsOf(gm));

        // An unknown version fails loudly.
        var bad = new byte[16];
        var w = new BufferWriter(bad, true);
        w.WriteEncodedInt(ClusterFStaffHubSpots.CurrentVersion + 1);
        w.Flush();
        Assert.Throws<System.IO.InvalidDataException>(() => ClusterFStaffHubSpots.ReadFrom(new BufferReader(bad)));
    }

    // ---------------------------------------------------------------- D3

    [Fact]
    public void GoToRefusesBadInputCleanlyAndMovesOnGoodInput()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var g = Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel });
        var start = gm.Location;

        Assert.False(ClusterFStaffHub.TryParseGoTo(gm, "100", "100", "", "Narnia", out _, out _, out var error));
        _out.WriteLine(error);
        Assert.Contains("Narnia", error);

        Assert.False(ClusterFStaffHub.TryParseGoTo(gm, "a", "100", "", "", out _, out _, out _));
        Assert.False(ClusterFStaffHub.TryParseGoTo(gm, "100", "100", "500", "", out _, out _, out _));
        Assert.False(ClusterFStaffHub.TryParseGoTo(gm, "-1", "100", "", "", out _, out _, out _));
        Assert.False(ClusterFStaffHub.TryParseGoTo(gm, "999999", "100", "", "trammel", out _, out _, out _));

        g.Respond(gm, StaffHubGump.BtnGoTo, goX: "100", goY: "100", goZ: "", goFacet: "Narnia");
        g.Respond(gm, StaffHubGump.BtnGoTo, goX: null, goY: null, goZ: null, goFacet: null);
        Assert.Equal(start, gm.Location);

        g.Respond(gm, StaffHubGump.BtnGoTo, goX: "1500", goY: "1610", goZ: "5", goFacet: "felucca");
        Assert.Equal(new Point3D(1500, 1610, 5), gm.Location);
        Assert.Same(Map.Felucca, gm.Map);

        // No z: the map's average Z there; no facet: the staff member's own.
        Assert.True(ClusterFStaffHub.TryParseGoTo(gm, "1501", "1611", "", "", out var map, out var loc, out _));
        Assert.Same(Map.Felucca, map);
        Assert.Equal(Map.Felucca.GetAverageZ(1501, 1611), loc.Z);
    }

    // ---------------------------------------------------------------- D4

    [Fact]
    public void RecentCapsAtFiveNewestFirst()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        for (var i = 0; i < 7; i++)
        {
            ClusterFStaffHub.GoTo(gm, $"spot {i}", Map.Trammel, new Point3D(1000 + i, 1000, 0));
        }

        var recent = ClusterFStaffHubSpots.RecentOf(gm);
        Assert.Equal(ClusterFStaffHubSpots.RecentCap, recent.Count);
        Assert.Equal(5, recent.Count);
        Assert.Equal(new[] { "spot 6", "spot 5", "spot 4", "spot 3", "spot 2" }, recent.Select(r => r.Name));
        Assert.Equal(5, Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel }).Recent.Count);
    }

    // ---------------------------------------------------------------- E1

    [Fact]
    public void NoButtonIdMeansTwoThings()
    {
        var admin = NewCharacter(AccessLevel.Administrator);
        var player = NewCharacter();
        ClusterFStaffHubSpots.SaveHere(admin, "E1 spot", out _);
        ClusterFStaffHub.GoTo(admin, "E1 recent", Map.Trammel, admin.Location);

        int[] chrome = [StaffHubGump.BtnClose, StaffHubGump.BtnTabBase, StaffHubGump.BtnTabBase + 1, StaffHubGump.BtnTabBase + 2];

        var gumps = new Dictionary<string, StaffHubGump>();
        foreach (var cat in ClusterFStaffHub.Categories)
        {
            gumps[$"Commands/{cat}"] = Hub(admin, new StaffHubState { Category = cat });
        }

        gumps["Player/selected"] = Hub(admin, new StaffHubState { Tab = StaffHubTab.Player, Selected = player });
        gumps["Player/matches"] = Hub(admin, new StaffHubState { Tab = StaffHubTab.Player, Matches = [player, admin] });
        gumps["Travel/places"] = Hub(admin, new StaffHubState { Tab = StaffHubTab.Travel });
        gumps["Travel/halls"] = Hub(admin, new StaffHubState { Tab = StaffHubTab.Travel, ShowGuildHalls = true });

        var byTab = new Dictionary<string, HashSet<int>>();
        foreach (var (name, g) in gumps)
        {
            var ids = Buttons(g).ToList();
            var repeated = ids.GroupBy(i => i).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
            Assert.True(repeated.Count == 0, $"{name} repeats button ids {string.Join(", ", repeated)}");

            var tab = name.Split('/')[0];
            if (!byTab.TryGetValue(tab, out var set))
            {
                byTab[tab] = set = [];
            }

            set.UnionWith(ids.Except(chrome));
            _out.WriteLine($"{name}: {ids.Count} buttons");
        }

        var tabs = byTab.Keys.ToList();
        for (var a = 0; a < tabs.Count; a++)
        {
            for (var b = a + 1; b < tabs.Count; b++)
            {
                var shared = byTab[tabs[a]].Intersect(byTab[tabs[b]]).ToList();
                Assert.True(shared.Count == 0, $"{tabs[a]} and {tabs[b]} share button ids {string.Join(", ", shared)}");
            }
        }
    }

    // ---------------------------------------------------------------- E2

    [Fact]
    public void AStaleGumpFailsSafely()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);

        // A command removed after the gump drew it.
        var g = Hub(gm, new StaffHubState { Category = CommandCategory.Diagnostic });
        var row = g.Rows.ToList().IndexOf(ProbeArgs);
        Assert.True(row >= 0);
        CommandSystem.Entries.Remove(ProbeArgs);
        g.Respond(gm, StaffHubGump.BtnRunBase + row);
        Assert.Null(gm.FindGump<StaffHubPromptGump>());
        Assert.Equal(0, Calls(ProbeArgs));

        // A character who logged out after the gump drew them online.
        var player = NewCharacter();
        var state = new StaffHubState { Tab = StaffHubTab.Player, Selected = player };
        var p = Hub(gm, state);
        Assert.Contains(StaffHubGump.BtnPlayerGo, Buttons(p));
        var ns = player.NetState;
        player.NetState = null;
        ns.Mobile = null;
        ns.Dispose();
        player.Internalize();
        var before = gm.Location;
        p.Respond(gm, StaffHubGump.BtnPlayerGo);
        p.Respond(gm, StaffHubGump.BtnPlayerBring);
        Assert.Equal(before, gm.Location);

        // A character deleted after the gump drew them.
        player.Delete();
        p.Respond(gm, StaffHubGump.BtnPlayerProps);
        p.Respond(gm, StaffHubGump.BtnGrantBase);
        Assert.Null(state.Selected);

        // A button no gump drew (out of range) does nothing.
        Hub(gm, new StaffHubState()).Respond(gm, StaffHubGump.BtnRunBase + 99);
        Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel }).Respond(gm, StaffHubGump.BtnSpotDeleteBase + 3);
    }
}
