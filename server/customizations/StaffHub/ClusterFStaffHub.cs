// ClusterFStaffHub.cs
//
// cc-P51 (F-30, ours). [SL opens the Staff Hub, a tabbed gump for staff (StaffHubGump.cs). This file is what the gump
// shows and does, kept out of the gump so the facts can test it directly:
//
//   Commands  every command registered with [ShardCommand] outside the Player category, read at runtime from
//             CommandSystem.Entries, so a new command appears with no upkeep. Running one goes through
//             CommandSystem.Handle with the prefix, exactly as typing it: the same access check and the same output.
//   Player    a read-only panel over the existing data owners (no field, no version), the stock [Go, bring and
//             [Props, and the Grant commands aimed at the selected character.
//   Travel    the shard places, each read from its seeder's own member; saved spots (ClusterFStaffHubSpots); go to
//             x, y, z, facet.
//
// Access: GameMaster and up; a WorldRemoval command needs Administrator. Test or live is TestCenter.Enabled (as pinned
// TestCenterFillCommand reads it); on live a command declared Shard = TestOnly is shown greyed and refused.
// Notes: shard-migration/notes/cc-P51-staff-hub-and-raptor-pack.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;

namespace Server;

public static class ClusterFStaffHub
{
    public const string CommandName = "SL";
    public const AccessLevel Access = AccessLevel.GameMaster;
    public const AccessLevel RemovalAccess = AccessLevel.Administrator;

    public static void Configure()
    {
        CommandSystem.Register(CommandName, Access, SL_OnCommand);
    }

    [Usage("SL")]
    [Description("Opens the Shattered Legacy Staff Hub: shard commands by category, a player panel, and travel.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any, Summary = "Opens the Staff Hub gump.")]
    private static void SL_OnCommand(CommandEventArgs e) => Open(e.Mobile);

    public static void Open(Mobile from, StaffHubState state = null)
    {
        if (from == null || from.Deleted || from.AccessLevel < Access)
        {
            return;
        }

        from.CloseGump<StaffHubGump>();
        from.SendGump(new StaffHubGump(from, state ?? new StaffHubState()));
    }

    /// <summary>True on the test shard: TestCenter.Enabled, the setting both shards' configs set.</summary>
    public static bool IsTestShard => TestCenter.Enabled;

    // ---------------------------------------------------------------- Commands

    public static readonly CommandCategory[] Categories =
    [
        CommandCategory.WorldGeneration, CommandCategory.WorldSetup, CommandCategory.WorldRemoval,
        CommandCategory.DevTool, CommandCategory.Grant, CommandCategory.Diagnostic
    ];

    public static string CategoryLabel(CommandCategory c) => c switch
    {
        CommandCategory.WorldGeneration => "World generation",
        CommandCategory.WorldSetup      => "World setup",
        CommandCategory.WorldRemoval    => "World removal",
        CommandCategory.DevTool         => "Dev tools",
        CommandCategory.Grant           => "Grants",
        CommandCategory.Diagnostic      => "Diagnostics",
        _                               => c.ToString()
    };

    public sealed class HubCommand
    {
        public HubCommand(CommandEntry entry, ShardCommandAttribute declaration)
        {
            Entry = entry;
            Declaration = declaration;
            var method = entry.Handler.Method;
            Usage = method.GetCustomAttribute<UsageAttribute>()?.Usage ?? "";
            Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            Arguments = ArgumentsOf(entry.Command, Usage);
        }

        public CommandEntry Entry { get; }
        public ShardCommandAttribute Declaration { get; }
        public string Name => Entry.Command;
        public string Usage { get; }
        public string Description { get; }

        // What [Usage] shows after the command's name, e.g. "<guildKey> [amount]". Empty when it shows none.
        public string Arguments { get; }

        public CommandCategory Category => Declaration.Category;
        public string Summary => string.IsNullOrWhiteSpace(Declaration.Summary) ? Description : Declaration.Summary;
        public bool TakesArguments => Arguments.Length > 0;
        public bool HasDryRun => !string.IsNullOrEmpty(Declaration.DryRun);
        public bool IsTestOnly => Declaration.Shard == CommandShard.TestOnly;

        // Duplicates, deletes again, or removes: asks yes or no before it runs.
        public bool NeedsConfirm =>
            Declaration.Rerun is CommandRerun.Duplicates or CommandRerun.DeletesAgain ||
            Category == CommandCategory.WorldRemoval;
    }

    /// <summary>
    /// The part of a [Usage] after the command's name. Usage strings are written both with and without the prefix
    /// ("[CompactStanding [amount]", "GuildStanding &lt;guildKey&gt; [amount]").
    /// </summary>
    public static string ArgumentsOf(string name, string usage)
    {
        if (string.IsNullOrWhiteSpace(usage))
        {
            return "";
        }

        var u = usage.Trim();

        foreach (var prefix in new[] { CommandSystem.Prefix, "[" })
        {
            if (!string.IsNullOrEmpty(prefix) && u.StartsWith(prefix + name, StringComparison.OrdinalIgnoreCase))
            {
                u = u[prefix.Length..];
                break;
            }
        }

        if (u.StartsWith(name, StringComparison.OrdinalIgnoreCase) &&
            (u.Length == name.Length || char.IsWhiteSpace(u[name.Length])))
        {
            return u[name.Length..].Trim();
        }

        var space = u.IndexOf(' ');
        return space < 0 ? "" : u[(space + 1)..].Trim();
    }

    /// <summary>
    /// Every registered command whose handler carries [ShardCommand] and whose category is not Player, by name. An
    /// alias (a name in the handler's [Aliases]) is folded into its command. The hub does not list [SL itself.
    /// </summary>
    public static List<HubCommand> AllCommands()
    {
        var result = new List<HubCommand>();

        foreach (var entry in CommandSystem.Entries.Values)
        {
            var method = entry.Handler?.Method;
            var declaration = method?.GetCustomAttribute<ShardCommandAttribute>();

            if (declaration == null || declaration.Category == CommandCategory.Player ||
                entry.Command.InsensitiveEquals(CommandName))
            {
                continue;
            }

            var aliases = method.GetCustomAttribute<AliasesAttribute>()?.Aliases;
            if (aliases != null && aliases.Any(a => a.InsensitiveEquals(entry.Command)))
            {
                continue;
            }

            result.Add(new HubCommand(entry, declaration));
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Whether this staff member is shown the command at all: its own access, and Administrator for removal.</summary>
    public static bool IsVisibleTo(Mobile from, HubCommand c) =>
        from != null && from.AccessLevel >= c.Entry.AccessLevel &&
        (c.Category != CommandCategory.WorldRemoval || from.AccessLevel >= RemovalAccess);

    public static List<HubCommand> CommandsIn(Mobile from, CommandCategory category) =>
        AllCommands().Where(c => c.Category == category && IsVisibleTo(from, c)).ToList();

    public static HubCommand Find(string name) =>
        string.IsNullOrEmpty(name) ? null : AllCommands().FirstOrDefault(c => c.Name.InsensitiveEquals(name));

    public enum Refusal { None, Gone, NoAccess, NeedsAdministrator, TestOnlyOnLive }

    public static Refusal CanRun(Mobile from, HubCommand c)
    {
        if (c == null || !CommandSystem.Entries.TryGetValue(c.Name, out var live) || live.Handler != c.Entry.Handler)
        {
            return Refusal.Gone;
        }

        if (from.AccessLevel < c.Entry.AccessLevel)
        {
            return Refusal.NoAccess;
        }

        if (c.Category == CommandCategory.WorldRemoval && from.AccessLevel < RemovalAccess)
        {
            return Refusal.NeedsAdministrator;
        }

        if (c.IsTestOnly && !IsTestShard)
        {
            return Refusal.TestOnlyOnLive;
        }

        return Refusal.None;
    }

    public static string RefusalMessage(Refusal r, string name) => r switch
    {
        Refusal.Gone               => $"[{name} is no longer a registered command.",
        Refusal.NoAccess           => $"You do not have access to [{name}.",
        Refusal.NeedsAdministrator => $"[{name} removes world content and needs Administrator.",
        Refusal.TestOnlyOnLive     => $"[{name} is declared test shard only, and this is the live shard.",
        _                          => ""
    };

    /// <summary>
    /// Runs a command as if the staff member typed it: the hub's own refusals first (gone, removal below
    /// Administrator, test only on live), then CommandSystem.Handle with the prefix, which makes pinned's own access
    /// check and gives the command's own output. Returns whether the command was handed to the command system.
    /// </summary>
    public static bool Run(Mobile from, string name, string args)
    {
        var c = Find(name);
        var refusal = CanRun(from, c);

        if (refusal != Refusal.None)
        {
            from.SendMessage(0x20, RefusalMessage(refusal, c?.Name ?? name));
            return false;
        }

        var text = args?.Trim() ?? "";
        CommandSystem.Handle(from, CommandSystem.Prefix + c.Name + (text.Length > 0 ? " " + text : ""));
        return true;
    }

    /// <summary>Runs the command's declared dry run: exactly the word its [ShardCommand] DryRun names.</summary>
    public static bool DryRun(Mobile from, string name)
    {
        var c = Find(name);

        if (c?.HasDryRun != true)
        {
            from.SendMessage(0x20, $"[{name} declares no dry run.");
            return false;
        }

        return Run(from, c.Name, c.Declaration.DryRun);
    }

    /// <summary>
    /// A Grant aimed at the selected character. The command runs as typed; every Grant command today then asks for a
    /// target. When the selected character is online on the staff member's facet, the hub answers that cursor with
    /// them through pinned's Target.Invoke, which makes every check a click would (staff pass line of sight, and the
    /// grants use unlimited range). Otherwise the cursor stays up for the staff member to aim, with a line saying why.
    /// Returns true when the cursor was answered with the selected character.
    /// </summary>
    public static bool RunGrant(Mobile from, string name, string args, Mobile selected)
    {
        var before = from.Target;

        if (!Run(from, name, args))
        {
            return false;
        }

        var cursor = from.Target;

        if (cursor == null || cursor == before)
        {
            return false; // the command asked for no target (it reported, or refused its arguments)
        }

        if (selected == null || selected.Deleted)
        {
            from.SendMessage("No character is selected: target one.");
            return false;
        }

        if (selected.NetState == null || selected.Map == null || selected.Map == Map.Internal)
        {
            from.SendMessage($"{selected.Name} is offline. The grant commands take their character from a target cursor: target them when they are online, or press Escape.");
            return false;
        }

        if (selected.Map != from.Map)
        {
            from.SendMessage($"{selected.Name} is on {selected.Map}, you are on {from.Map}. Target them, or Go to them first.");
            return false;
        }

        cursor.Invoke(from, selected);
        return true;
    }

    // ---------------------------------------------------------------- Player

    public static int OnlineCount => NetState.Instances.Count(ns => ns.Mobile != null);

    /// <summary>Characters (online or not) whose name matches: exact matches first, then those containing it.</summary>
    public static List<Mobile> FindCharacters(string text, int max = 8)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var exact = new List<Mobile>();
        var partial = new List<Mobile>();

        foreach (var m in World.Mobiles.Values)
        {
            if (m is not PlayerMobile || m.Deleted || string.IsNullOrEmpty(m.Name))
            {
                continue;
            }

            if (m.Name.InsensitiveEquals(text))
            {
                exact.Add(m);
            }
            else if (m.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                partial.Add(m);
            }
        }

        partial.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return exact.Concat(partial).Take(max).ToList();
    }

    public static bool IsOnline(Mobile m) => m?.NetState != null && m.Map != null && m.Map != Map.Internal;

    public static string AccountName(Mobile m) => (m?.Account as IAccount)?.Username ?? "(none)";

    // Counted from CraftResource (ResourceInfo.cs): the post-Valorite ores, Platinum to Celestial, are the eight the
    // Prospector's Logbook gates until reported (CompactOreSatchelRoutingHook._extendedOreGateKeys); the extended
    // woods, Ironwood to Starwood, are the eight the Foresters' logbook gates (ForestersLogbookGump.ExtendedKeys).
    public static readonly string[] ExtendedOres = ResourceNames(CraftResource.Platinum, CraftResource.Celestial);
    public static readonly string[] ExtendedWoods = ResourceNames(CraftResource.Ironwood, CraftResource.Starwood);

    private static string[] ResourceNames(CraftResource first, CraftResource last)
    {
        var names = new List<string>();
        for (var r = first; r <= last; r++)
        {
            names.Add(r.ToString());
        }

        return names.ToArray();
    }

    public sealed record PlayerRow(string Label, string Value);

    /// <summary>
    /// The Player tab's grid, read from the data owners and nothing else. Read only: Get, never GetOrCreate, so looking
    /// at a character creates no record.
    /// </summary>
    public static List<PlayerRow> PlayerRows(Mobile m)
    {
        var data = m?.Account is IAccount acct ? ClusterFAccountPersistence.Get(acct) : null;
        var guild = m == null ? null : ClusterFAccountPersistence.GetGuild(m);

        var leagueRank = ClusterFLeagueRanks.GetRank(m);
        var mining = guild?.GetReputation("mining") ?? 0;
        var compact = guild != null ? ClusterFGuildSystem.GetRankName("mining", guild) : MinersCompactLiaisonGump.GetRankName(0);

        var unlocks = data?.RestorationRegistry.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        var ores = data == null ? 0 : ExtendedOres.Count(k => data.OreDiscoveries.ContainsKey(k));
        var woods = data == null ? 0 : ExtendedWoods.Count(k => data.WoodDiscoveries.ContainsKey(k));

        var bank = m?.FindItemOnLayer<BankBox>(Layer.Bank);

        return
        [
            new("Guild standing", GuildStanding(guild)),
            new("League rank", ClusterFLeagueRanks.RankName(leagueRank)),
            new("Renown", data == null ? "0" : $"{data.Renown:N0} (lifetime {data.LifetimeRenown:N0})"),
            new("Compact rank", $"{compact} ({mining:N0})"),
            new("Restoration unlocks", unlocks.Count == 0 ? "none" : $"{unlocks.Count}: {string.Join(", ", unlocks)}"),
            new("Ores discovered", $"{ores} of {ExtendedOres.Length}"),
            new("Woods discovered", $"{woods} of {ExtendedWoods.Length}"),
            new("Skill / stat caps", m == null ? "" :
                $"skills {m.Skills.Total / 10.0:0.#} of {m.Skills.Cap / 10.0:0.#}; stats {m.RawStatTotal} of {m.StatCap}, {ClusterFStatCaps.IndividualStatCap} each"),
            new("Bank items", bank == null ? $"no bank yet (limit {ClusterFBankLimit.GetMaxItems(m)})" : $"{bank.TotalItems} of {ClusterFBankLimit.GetMaxItems(m)}"),
        ];
    }

    // The guild the character stands highest in (rank index, then standing), plus how many more it has joined.
    private static string GuildStanding(CharacterGuildData guild)
    {
        if (guild == null || guild.JoinedGuilds.Count == 0)
        {
            return "no guild";
        }

        ClusterFGuildSystem.EnsureRegistered();

        var best = guild.JoinedGuilds
            .OrderByDescending(k => ClusterFGuildSystem.GuildRankIndex(k, guild))
            .ThenByDescending(guild.GetReputation)
            .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
            .First();

        var name = ClusterFGuildSystem.GetDef(best)?.Name ?? best;
        var more = guild.JoinedGuilds.Count - 1;
        return $"{name}: {ClusterFGuildSystem.GetRankName(best, guild)}" + (more > 0 ? $" and {more} more" : "");
    }

    /// <summary>The stock [Go to a character, by serial, as pinned's [Interface does (Interface.cs:560-565).</summary>
    public static bool GoToPlayer(Mobile from, Mobile m)
    {
        if (!IsOnline(m))
        {
            from.SendMessage("That character is offline.");
            return false;
        }

        CommandSystem.Handle(from, $"{CommandSystem.Prefix}Go {m.Serial}");
        return true;
    }

    /// <summary>The stock bring: pinned's [Interface "Bring them here" (Interface.cs:567-577), which has no command.</summary>
    public static bool BringPlayer(Mobile from, Mobile m)
    {
        if (!IsOnline(m))
        {
            from.SendMessage("That character is offline.");
            return false;
        }

        if (from.Map == null || from.Map == Map.Internal)
        {
            from.SendMessage("You cannot bring that person here.");
            return false;
        }

        m.MoveToWorld(from.Location, from.Map);
        return true;
    }

    /// <summary>The stock [Props on the character, by serial.</summary>
    public static void Props(Mobile from, Mobile m)
    {
        if (m != null && !m.Deleted)
        {
            CommandSystem.Handle(from, $"{CommandSystem.Prefix}Props {m.Serial}");
        }
    }

    // ---------------------------------------------------------------- Travel

    public sealed record ShardPlace(string Name, Map Map, Point3D Location, string Unavailable = null)
    {
        public bool Available => Unavailable == null && Map != null && Map != Map.Internal;
        public string Facet => Map?.Name ?? "";
    }

    public const string GuildHallsRow = "Guild halls";

    /// <summary>
    /// The shard places, each from its seeder's own member. "Guild halls" stands for ClusterFGuildHallSeeder.Anchors,
    /// listed by GuildHalls(). The dungeon entrance is cc-P36's and does not exist in code yet.
    /// </summary>
    public static List<ShardPlace> ShardPlaces() =>
    [
        new("New Haven bank", ClusterFNewHavenSeeder.Facet, ClusterFNewHavenSeeder.BankAnchor),
        new("Mine camp", ClusterFMineCampSeeder.Facet, ClusterFMineCampSeeder.Anchor),
        new("League Registrar office", ClusterFRegistrarOfficeSeeder.Facet, ClusterFRegistrarOfficeSeeder.Anchor),
        new(GuildHallsRow, ClusterFNewHavenSeeder.Facet, Point3D.Zero),
        new("Fountain of Fortune", ClusterFFountainOfFortuneSeeder.Facet, ClusterFFountainOfFortuneSeeder.Location),
        new("Royal City", ClusterFRoyalCitySeeder.Facet, ClusterFRoyalCitySeeder.MoongateLocation),
        new("Dungeon entrance", null, Point3D.Zero, "after P36"),
    ];

    // Named guild first: two guilds share the New Haven Magery School.
    public static List<ShardPlace> GuildHalls()
    {
        ClusterFGuildSystem.EnsureRegistered();
        return ClusterFGuildHallSeeder.Anchors
            .Select(l => new ShardPlace($"{ClusterFGuildSystem.GetDef(l.GuildKey)?.Name ?? l.GuildKey}, {l.Hall}", l.Map, l.Point))
            .ToList();
    }

    /// <summary>
    /// Moves the staff member to a map and location as pinned's [Go menu does (GoGump.cs:162, MoveToWorld), and
    /// remembers it in Recent.
    /// </summary>
    public static bool GoTo(Mobile from, string label, Map map, Point3D location)
    {
        if (map == null || map == Map.Internal)
        {
            from.SendMessage(0x20, $"{label}: that facet is not loaded.");
            return false;
        }

        from.MoveToWorld(location, map);
        ClusterFStaffHubSpots.AddRecent(from, label, map, location);
        return true;
    }

    /// <summary>A facet by name, as [Go matches one (Handlers.cs:456-470): any loaded map but Internal, case ignored.</summary>
    public static Map FindFacet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var map in Map.AllMaps)
        {
            if (map.MapIndex is 0x7F or 0xFF)
            {
                continue;
            }

            if (name.Trim().InsensitiveEquals(map.Name))
            {
                return map;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the Go to entries. z may be empty (the map's average Z there); facet may be empty (the staff member's
    /// own). Bad input gives one line and no move, never an exception.
    /// </summary>
    public static bool TryParseGoTo(
        Mobile from, string xText, string yText, string zText, string facetText,
        out Map map, out Point3D location, out string error)
    {
        location = Point3D.Zero;
        error = null;

        map = string.IsNullOrWhiteSpace(facetText) ? from.Map : FindFacet(facetText);

        if (map == null || map == Map.Internal)
        {
            error = string.IsNullOrWhiteSpace(facetText)
                ? "You are not on a facet; name one."
                : $"No facet is named '{facetText.Trim()}'. Try: {string.Join(", ", Map.AllMaps.Where(m => m.MapIndex is not (0x7F or 0xFF)).Select(m => m.Name))}.";
            map = null;
            return false;
        }

        if (!int.TryParse(xText?.Trim(), out var x) || !int.TryParse(yText?.Trim(), out var y))
        {
            error = "x and y must be whole numbers.";
            return false;
        }

        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            error = $"{x}, {y} is outside {map.Name} (0 to {map.Width - 1}, 0 to {map.Height - 1}).";
            return false;
        }

        int z;
        if (string.IsNullOrWhiteSpace(zText))
        {
            z = map.GetAverageZ(x, y);
        }
        else if (!int.TryParse(zText.Trim(), out z) || z < sbyte.MinValue || z > sbyte.MaxValue)
        {
            error = "z must be a whole number from -128 to 127, or empty.";
            return false;
        }

        location = new Point3D(x, y, z);
        return true;
    }

    // ---------------------------------------------------------------- shared

    /// <summary>Escapes text for a gump's HTML (usage strings carry angle brackets).</summary>
    public static string Html(string text) =>
        (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

/// <summary>What one Staff Hub gump shows: the tab, and each tab's selection. Passed from gump to gump.</summary>
public sealed class StaffHubState
{
    public StaffHubTab Tab { get; set; } = StaffHubTab.Commands;

    public CommandCategory Category { get; set; } = CommandCategory.WorldGeneration;
    public int CommandPage { get; set; }

    public Mobile Selected { get; set; }
    public List<Mobile> Matches { get; set; } = [];
    public string Search { get; set; } = "";

    public bool ShowGuildHalls { get; set; }
    public int SpotPage { get; set; }
}

public enum StaffHubTab
{
    Commands,
    Player,
    Travel,
    Events
}
