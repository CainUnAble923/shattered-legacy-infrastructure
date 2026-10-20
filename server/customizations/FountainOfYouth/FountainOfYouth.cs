// cc-P192 (Chase 2026-10-19 15:01: "we need a 'fountain of youth' in haven to reset an account to young for testing").
// SECRET on live: never document the roaming fountain, its pool or its rules on the wiki, the website, the player patch
// notes or any player text. Admin notes: shard-migration notes/admin/secrets.md. Notes: notes/cc-P192-cont-fountain-of-youth.md.
//
// Drinking (double-click within 2 tiles, or say "drink" within 2 tiles; a confirm gump first) makes the drinker's account
// Young again (MakeYoung):
//   - the account's Young flag (pinned Account.Young, Account.cs:252-262; flag 1 means "not young");
//   - the Young clock: Young ends at ClusterFYoungPlayer.Duration of play counted from the account's clock start, a saved
//     account tag (ClusterFYoungPlayer.YoungPlayed; patched into pinned's CheckYoung and login line,
//     server/patches/Account-young-duration.patch). TotalGameTime itself is never changed;
//   - every character on the account is Young again (PlayerMobile.Young; pinned clears them all in RemoveYoungStatus,
//     Account.cs:830-853, and sets them only at creation, CharacterCreation.cs:242);
//   - the New Haven tutorial (ClusterFTutorial, once per account): back to the start, with the drinker as its character,
//     so the Rootdeep Vault lets them in again (setting fountainOfYouth.resetTutorial);
//   - the New Haven trainer quests (the 26 in ApprenticeTraining.Areas, OneTimeOnly): their done records are cleared for
//     every character on the account, so each can be taken again, and with it P189's Old Haven boost
//     (setting fountainOfYouth.resetApprenticeQuests).
// It keeps skills, stats, items, gold, the bank, guilds, the League, achievements, loops and quests other than those 26
// (P18's reset stone is the tool for those). There is no saved "grown up" state in pinned to clear: renouncing ("I
// renounce my young player status", or "I want to grow up!") only calls RemoveYoungStatus (Gumps/YoungGumps.cs).
// Pinned's Young timer (Account.cs:779-799) starts at the next login; until then the 2 weeks simply start counting then.
//
// Two placements (fountainOfYouth.mode, "auto" by default: Fixed on a Test Center shard, Roaming otherwise):
//   - Fixed: New Haven, centred on 3497,2583,14 (open ground at Z 14 west of the plaza, 9 tiles from the arrival tile;
//     notes section 2). It stays.
//   - Roaming: a random valid spot from the pool (FountainPool), and after every drink, and every moveEveryDays, it
//     vanishes with an effect and appears elsewhere, never in the same pool entry twice in a row. Around it a sanctum
//     region refuses Mark, recall and gate in, houses and boats (FountainSanctumRegion).
// Staff: [WhereIsFountain, [GoFountain, [MoveFountain (all logged); [ClusterFSeedFountainOfYouth [dryrun] places it.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Commands;
using Server.Engines.MLQuests;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tutorial;

namespace Server.Engines.FountainOfYouth;

public enum FountainMode
{
    Fixed,
    Roaming
}

[SerializationGenerator(0, false)]
public partial class FountainOfYouthAddon : BaseAddon
{
    public const int FountainHue = 0x0480; // pale blue
    public const int UseRange = 2;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _roaming;

    // The pool entry it stands in (Roaming), so the next move goes elsewhere.
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string? _poolKey;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _placedAt;

    private FountainSanctumRegion? _sanctum;
    private TimerExecutionToken _sparkle;

    [Constructible]
    public FountainOfYouthAddon() : this(false)
    {
    }

    public FountainOfYouthAddon(bool roaming)
    {
        _roaming = roaming;

        // Pinned StoneFountainAddon's pieces (Items/Addons/StoneFountainAddon.cs), same order and offsets.
        var itemID = 0x1731;
        int[][] offsets =
        [
            [-2, 1], [-1, 1], [0, 1], [1, 1], [1, 0], [1, -1], [1, -2], [0, -2], [0, -1], [0, 0], [-1, 0], [-2, 0],
            [-2, -1], [-1, -1], [-1, -2]
        ];

        foreach (var o in offsets)
        {
            AddComponent(new AddonComponent(itemID++), o[0], o[1], 0);
        }

        AddComponent(new AddonComponent(++itemID), -2, -2, 0);

        Hue = FountainHue;
        FountainOfYouthSystem.Register(this);
    }

    public override bool Decays => false;

    public FountainMode Mode => _roaming ? FountainMode.Roaming : FountainMode.Fixed;

    public override void OnComponentUsed(AddonComponent c, Mobile from) => FountainOfYouthSystem.Offer(this, from);

    /// <summary>True when <paramref name="m"/> stands within <see cref="UseRange"/> of any piece.</summary>
    public bool InUseRange(Mobile m) =>
        m.Map == Map && Components.Any(c => m.InRange(c.Location, UseRange));

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);
        RefreshSanctum();
    }

    public override void OnMapChange()
    {
        base.OnMapChange();
        RefreshSanctum();
    }

    /// <summary>A roaming fountain has a sanctum around it where it stands now; a fixed one has none.</summary>
    public void RefreshSanctum()
    {
        _sanctum?.Unregister();
        _sanctum = null;

        if (!Deleted && _roaming && Map != null && Map != Map.Internal)
        {
            _sanctum = FountainSanctumRegion.Create(Map, Location, FountainOfYouthSystem.NoMarkRadius);
        }

        _sparkle.Cancel();

        if (!Deleted && Map != null && Map != Map.Internal)
        {
            Timer.StartTimer(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4), Sparkle, out _sparkle);
        }
    }

    public FountainSanctumRegion? Sanctum => _sanctum;

    // A sparkle over the basin while a player is near, so it reads as special.
    private void Sparkle()
    {
        if (Deleted || Map == null || Map == Map.Internal)
        {
            _sparkle.Cancel();
            return;
        }

        foreach (var m in Map.GetMobilesInRange(Location, 12))
        {
            if (m.Player)
            {
                Effects.SendLocationParticles(EffectItem.Create(new Point3D(X, Y, Z + 10), Map, EffectItem.DefaultDuration), 0x376A, 9, 20, 5042);
                return;
            }
        }
    }

    public void MarkPlaced(string? poolKey)
    {
        PoolKey = poolKey;
        PlacedAt = Core.Now;
    }

    public override void OnDelete()
    {
        base.OnDelete();
        _sparkle.Cancel();
        _sanctum?.Unregister();
        _sanctum = null;
        FountainOfYouthSystem.Unregister(this);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        FountainOfYouthSystem.Register(this);
        RefreshSanctum();
    }
}

public static class FountainOfYouthSystem
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(FountainOfYouthSystem));

    public static readonly Point3D NewHavenSpot = new(3497, 2583, 14);

    public const string ClockTag = ClusterFYoungPlayer.ClockStartTag;
    public const string LastDrinkTag = "clusterf.fountainOfYouth.lastDrink";
    public const string AchievementKey = ClusterFAchievementSystem.FountainOfYouthAchievementKey;

    // Settings (Configure). The live abuse limits are off by default; Chase sets them on live (notes).
    public static bool Enabled { get; set; } = true;
    public static string ModeSetting { get; set; } = "auto";
    public static double MoveEveryDays { get; set; } = 7.0;
    public static int NoMarkRadius { get; set; } = 20;
    public static double MisclickCooldownMinutes { get; set; } = 5.0;
    public static double AccountCooldownDays { get; set; }
    public static int MaxSkillTotal { get; set; }
    public static double MaxAccountAgeDays { get; set; }
    public static bool ResetTutorial { get; set; } = true;
    public static bool ResetApprenticeQuests { get; set; } = true;
    public static bool SeedOnWorldLoad { get; set; }

    private static readonly HashSet<FountainOfYouthAddon> _fountains = [];

    public static IReadOnlyCollection<FountainOfYouthAddon> Fountains => _fountains;

    public static void Register(FountainOfYouthAddon f) => _fountains.Add(f);

    public static void Unregister(FountainOfYouthAddon f) => _fountains.Remove(f);

    /// <summary>The mode a new fountain gets: the setting, or for "auto" Fixed on a Test Center shard and Roaming elsewhere.</summary>
    public static FountainMode DefaultMode =>
        ModeSetting.ToLowerInvariant() switch
        {
            "fixed"   => FountainMode.Fixed,
            "roaming" => FountainMode.Roaming,
            _         => Server.Misc.TestCenter.Enabled ? FountainMode.Fixed : FountainMode.Roaming
        };

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.enabled", true);
        ModeSetting = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.mode", "auto");
        MoveEveryDays = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.moveEveryDays", 7.0);
        NoMarkRadius = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.noMarkRadius", 20);
        MisclickCooldownMinutes = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.misclickCooldownMinutes", 5.0);
        AccountCooldownDays = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.accountCooldownDays", 0.0);
        MaxSkillTotal = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.maxSkillTotal", 0);
        MaxAccountAgeDays = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.maxAccountAgeDays", 0.0);
        ResetTutorial = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.resetTutorial", true);
        ResetApprenticeQuests = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.resetApprenticeQuests", true);
        SeedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("fountainOfYouth.seedOnWorldLoad", false);

        EventSink.Speech += OnSpeech;
        EventSink.WorldLoad += OnWorldLoad;

        CommandSystem.Register("ClusterFSeedFountainOfYouth", AccessLevel.Administrator, OnSeedCommand);
        CommandSystem.Register("WhereIsFountain", AccessLevel.GameMaster, OnWhereCommand);
        CommandSystem.Register("GoFountain", AccessLevel.GameMaster, OnGoCommand);
        CommandSystem.Register("MoveFountain", AccessLevel.GameMaster, OnMoveCommand);
    }

    public static void Initialize()
    {
        // The weekly move: checked hourly, so a fountain never sits forever.
        Timer.StartTimer(TimeSpan.FromHours(1), TimeSpan.FromHours(1), CheckMoveDue);
    }

    private static void OnWorldLoad()
    {
        if (SeedOnWorldLoad)
        {
            Console.WriteLine(Seed(false));
        }
    }

    public static void CheckMoveDue()
    {
        foreach (var f in _fountains.ToList())
        {
            if (f.Mode == FountainMode.Roaming && MoveEveryDays > 0 && Core.Now >= f.PlacedAt + TimeSpan.FromDays(MoveEveryDays))
            {
                Move(f, "timer");
            }
        }
    }

    // ---------------------------------------------------------------- placing and moving

    /// <summary>Places a fountain if there is none. Fixed: New Haven. Roaming: a pool spot.</summary>
    public static string Seed(bool dryRun, FountainMode? mode = null)
    {
        var m = mode ?? DefaultMode;

        if (_fountains.FirstOrDefault(f => !f.Deleted) is { } existing)
        {
            return $"Fountain of Youth: one is already placed ({existing.Mode}); nothing placed. [WhereIsFountain finds it.";
        }

        if (m == FountainMode.Fixed)
        {
            if (dryRun)
            {
                return $"Fountain of Youth (dry run): would place it in New Haven at {NewHavenSpot}.";
            }

            var fixedOne = new FountainOfYouthAddon(false);
            fixedOne.MoveToWorld(NewHavenSpot, Map.Trammel);
            fixedOne.MarkPlaced(null);
            logger.Information("Fountain of Youth placed in New Haven at {Spot}", NewHavenSpot);
            return $"Fountain of Youth: placed in New Haven at {NewHavenSpot}.";
        }

        if (!FountainPool.TryPick(null, out var entry, out var map, out var spot))
        {
            return "Fountain of Youth: no valid spot found in the pool; nothing placed.";
        }

        if (dryRun)
        {
            return "Fountain of Youth (dry run): would place it at a random pool spot (staff see it with [WhereIsFountain once placed).";
        }

        var roaming = new FountainOfYouthAddon(true);
        roaming.MoveToWorld(spot, map);
        roaming.MarkPlaced(entry.Key);
        logger.Information("Fountain of Youth placed at {Spot} on {Map} ({Key})", spot, map, entry.Key);
        return "Fountain of Youth: placed at a pool spot ([WhereIsFountain shows it).";
    }

    /// <summary>Moves a roaming fountain to a valid spot in another pool entry. False when none was found (it stays).</summary>
    public static bool Move(FountainOfYouthAddon f, string why)
    {
        if (!FountainPool.TryPick(f.PoolKey, out var entry, out var map, out var spot))
        {
            logger.Warning("Fountain of Youth: no valid spot in another pool entry ({Why}); it stays", why);
            return false;
        }

        if (f.Map != null && f.Map != Map.Internal)
        {
            Effects.SendLocationParticles(EffectItem.Create(f.Location, f.Map, EffectItem.DefaultDuration), 0x3709, 10, 30, 5052);
            Effects.PlaySound(f.Location, f.Map, 0x1FE);
        }

        var from = f.PoolKey;
        f.MoveToWorld(spot, map);
        f.MarkPlaced(entry.Key);
        logger.Information("Fountain of Youth moved ({Why}) from {From} to {Key} at {Spot} on {Map}", why, from, entry.Key, spot, map);
        return true;
    }

    // ---------------------------------------------------------------- drinking

    private static void OnSpeech(SpeechEventArgs e)
    {
        if (e.Handled || e.Mobile is not PlayerMobile pm || !string.Equals(e.Speech?.Trim().TrimEnd('.', '!'), "drink", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var f in _fountains)
        {
            if (!f.Deleted && f.InUseRange(pm))
            {
                Offer(f, pm);
                return;
            }
        }
    }

    /// <summary>Why <paramref name="pm"/> may not drink now, or null when they may.</summary>
    public static string? Refusal(PlayerMobile pm)
    {
        if (!Enabled)
        {
            return "The water is still and does nothing.";
        }

        if (pm.Account is not Account acct)
        {
            return "The water does nothing for you.";
        }

        var last = LastDrink(acct);

        if (last != null && Core.Now < last.Value + TimeSpan.FromMinutes(MisclickCooldownMinutes))
        {
            return "You have only just drunk from it. Give it a few minutes.";
        }

        if (AccountCooldownDays > 0 && last != null && Core.Now < last.Value + TimeSpan.FromDays(AccountCooldownDays))
        {
            return "The water tastes of nothing. It will not work for you again so soon.";
        }

        if (MaxSkillTotal > 0 && pm.SkillsTotal > MaxSkillTotal * 10)
        {
            return "The water tastes of nothing. You are too set in your ways for it.";
        }

        if (MaxAccountAgeDays > 0 && Core.Now - acct.Created > TimeSpan.FromDays(MaxAccountAgeDays))
        {
            return "The water tastes of nothing. Some things cannot be undone.";
        }

        return null;
    }

    public static DateTime? LastDrink(Account acct) =>
        long.TryParse(acct.GetTag(LastDrinkTag), out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : null;

    public static void Offer(FountainOfYouthAddon f, Mobile from)
    {
        if (from is not PlayerMobile pm || !f.InUseRange(pm))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (Refusal(pm) is { } refusal)
        {
            pm.SendMessage(refusal);
            return;
        }

        pm.CloseGump<FountainOfYouthGump>();
        pm.SendGump(new FountainOfYouthGump(f));
    }

    /// <summary>The drink, after the confirm. Returns what was reset (for the log and the tests), or null when refused.</summary>
    public static List<string>? Drink(FountainOfYouthAddon f, PlayerMobile pm)
    {
        if (f.Deleted || !f.InUseRange(pm) || Refusal(pm) != null || pm.Account is not Account acct)
        {
            return null;
        }

        var reset = MakeYoung(pm);
        acct.SetTag(LastDrinkTag, Core.Now.Ticks.ToString());

        pm.PlaySound(0x4E);
        pm.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
        pm.SendMessage(0x59, "The water is cold and sweet. You feel young again.");
        pm.SendAsciiMessage(ClusterFYoungPlayer.LoginMessage(ClusterFYoungPlayer.Duration));

        logger.Information(
            "Fountain of Youth: {Character} (account {Account}) drank ({Mode}); reset: {Reset}",
            pm.Name, acct.Username, f.Mode, string.Join(", ", reset)
        );

        if (f.Mode == FountainMode.Roaming)
        {
            ClusterFAchievementSystem.TryGrant(acct, AchievementKey);
            Move(f, "drink");
        }

        return reset;
    }

    /// <summary>Makes the account Young again (see the header). Returns what it reset.</summary>
    public static List<string> MakeYoung(PlayerMobile drinker)
    {
        var reset = new List<string>();

        if (drinker.Account is not Account acct)
        {
            return reset;
        }

        acct.Young = true;
        acct.SetTag(ClockTag, acct.TotalGameTime.Ticks.ToString());
        reset.Add("account Young");
        reset.Add("Young clock");

        var characters = new List<PlayerMobile>();

        for (var i = 0; i < acct.Length; i++)
        {
            if (acct[i] is PlayerMobile pm)
            {
                characters.Add(pm);
                pm.Young = true;
            }
        }

        reset.Add($"Young on {characters.Count} character(s)");

        if (ResetTutorial && ClusterFTutorial.RecordOf(drinker) is { } record)
        {
            record.State = TutorialState.NotStarted;
            record.CharacterSerial = 0;
            record.StationsDone.Clear();
            record.StartedAt = default;
            record.FinishedAt = default;

            if (ClusterFTutorial.Enabled)
            {
                ClusterFTutorial.TryEnter(drinker);
            }

            reset.Add("tutorial");
        }

        if (ResetApprenticeQuests)
        {
            var cleared = 0;

            foreach (var pm in characters)
            {
                if (MLQuestSystem.GetContext(pm) is not { } context)
                {
                    continue;
                }

                foreach (var area in ApprenticeTraining.Areas)
                {
                    if (MLQuestSystem.Quests.TryGetValue(area.Quest, out var quest) && context.HasDoneQuest(quest))
                    {
                        context.RemoveDoneQuest(quest);
                        cleared++;
                    }
                }
            }

            reset.Add($"New Haven trainer quests ({cleared} done record(s))");
        }

        return reset;
    }

    // ---------------------------------------------------------------- staff commands

    [Usage("ClusterFSeedFountainOfYouth [dryrun]")]
    [Description("Places the Fountain of Youth if there is none: in New Haven on a Test Center shard, at a secret roaming spot otherwise.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the Fountain of Youth if none is placed. Moves and deletes nothing.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 && e.GetString(0).Equals("dryrun", StringComparison.OrdinalIgnoreCase);
        var result = Seed(dryRun);
        logger.Information("{Staff} ran [ClusterFSeedFountainOfYouth{Dry}: {Result}", e.Mobile.Name, dryRun ? " dryrun" : "", result);
        e.Mobile.SendMessage(result);
    }

    public static FountainOfYouthAddon? Current => _fountains.FirstOrDefault(f => !f.Deleted);

    [Usage("WhereIsFountain")]
    [Description("Staff: where the Fountain of Youth is, how long it has been there, with a Go there button.")]
    [ShardCommand(CommandCategory.Diagnostic)]
    private static void OnWhereCommand(CommandEventArgs e)
    {
        logger.Information("{Staff} ran [WhereIsFountain", e.Mobile.Name);

        if (Current is not { } f)
        {
            e.Mobile.SendMessage("No Fountain of Youth is placed.");
            return;
        }

        e.Mobile.SendGump(new WhereIsFountainGump(f));
    }

    [Usage("GoFountain")]
    [Description("Staff: teleports you beside the Fountain of Youth.")]
    [ShardCommand(CommandCategory.Diagnostic)]
    private static void OnGoCommand(CommandEventArgs e) => GoTo(e.Mobile);

    public static void GoTo(Mobile staff)
    {
        logger.Information("{Staff} went to the Fountain of Youth", staff.Name);

        if (Current is not { } f)
        {
            staff.SendMessage("No Fountain of Youth is placed.");
            return;
        }

        staff.MoveToWorld(new Point3D(f.X + 2, f.Y + 2, f.Z), f.Map);
    }

    [Usage("MoveFountain")]
    [Description("Staff: moves the roaming Fountain of Youth to a new secret spot now.")]
    [ShardCommand(CommandCategory.Diagnostic)]
    private static void OnMoveCommand(CommandEventArgs e)
    {
        logger.Information("{Staff} ran [MoveFountain", e.Mobile.Name);

        if (Current is not { } f)
        {
            e.Mobile.SendMessage("No Fountain of Youth is placed.");
            return;
        }

        if (f.Mode != FountainMode.Roaming)
        {
            e.Mobile.SendMessage("The Fountain of Youth here is the fixed one (New Haven); it does not move.");
            return;
        }

        e.Mobile.SendMessage(Move(f, $"[MoveFountain by {e.Mobile.Name}]")
            ? "The Fountain of Youth has moved. [WhereIsFountain shows where."
            : "No valid spot was found in another pool entry; it stays.");
    }

    public static string Describe(FountainOfYouthAddon f)
    {
        var region = Region.Find(f.Location, f.Map);
        var parent = region is FountainSanctumRegion ? region.Parent : region;
        var since = Core.Now - f.PlacedAt;
        return $"{f.Mode} fountain on {f.Map} at {f.X},{f.Y},{f.Z}; region {parent?.Name ?? "(none)"}; pool entry {f.PoolKey ?? "(none)"}; " +
               $"there {(int)since.TotalDays}d {since.Hours}h.";
    }
}

/// <summary>The confirm: what it resets and what it keeps. No word of where it is.</summary>
public class FountainOfYouthGump : Gump
{
    /// <summary>What it resets and what it keeps. Never where it is (W1 checks).</summary>
    public const string Body =
        "Drink, and your account is Young again: every character on it, with two more weeks of play before Young ends. " +
        "The tutorial starts over with this character, and the trainers' first quests can be taken again.<br><br>" +
        "It keeps everything else: skills, stats, items, gold, your bank, guilds, the League, achievements and loops.";

    private readonly FountainOfYouthAddon _fountain;

    public FountainOfYouthGump(FountainOfYouthAddon fountain) : base(150, 150)
    {
        _fountain = fountain;

        AddPage(0);
        AddBackground(0, 0, 400, 300, 9270);
        AddAlphaRegion(8, 8, 384, 284);
        AddLabel(120, 16, 1153, "The Fountain of Youth");
        AddHtml(20, 44, 360, 190, $"<BASEFONT COLOR=#DDDDDD>{Body}</BASEFONT>", false, true);
        AddButton(60, 250, 4005, 4007, 1);
        AddLabel(95, 252, 1152, "Drink");
        AddButton(240, 250, 4017, 4019, 0);
        AddLabel(275, 252, 1152, "Not now");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID != 1 || sender.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (FountainOfYouthSystem.Drink(_fountain, pm) == null)
        {
            pm.SendMessage(FountainOfYouthSystem.Refusal(pm) ?? "You are too far away to drink.");
        }
    }
}

/// <summary>[WhereIsFountain: staff only.</summary>
public class WhereIsFountainGump : Gump
{
    public WhereIsFountainGump(FountainOfYouthAddon fountain) : base(150, 150)
    {
        AddPage(0);
        AddBackground(0, 0, 420, 150, 9270);
        AddAlphaRegion(8, 8, 404, 134);
        AddLabel(130, 16, 1153, "Fountain of Youth (staff)");
        AddHtml(20, 44, 380, 60, $"<BASEFONT COLOR=#DDDDDD>{FountainOfYouthSystem.Describe(fountain)}</BASEFONT>", false, false);
        AddButton(20, 112, 4005, 4007, 1);
        AddLabel(55, 114, 1152, "Go there");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1 && sender.Mobile is { AccessLevel: >= AccessLevel.GameMaster } staff)
        {
            FountainOfYouthSystem.GoTo(staff);
        }
    }
}
