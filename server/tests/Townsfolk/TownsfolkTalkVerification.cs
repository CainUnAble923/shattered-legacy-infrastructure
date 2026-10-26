// TownsfolkTalkVerification.cs
//
// cc-P631 (P566 design, stage 1): the New Haven townsfolk's dialogue table, speech and topic menu. Notes in
// shard-migration notes/cc-P631-build-townsfolk-rest.md.
//
// Facts:
//   1. The shipped table loads from the builder's Distribution/Data/ShatteredLegacy (the Dockerfile's copy): three launch
//      profiles, every launch topic, two rumors or more each. Talk is off by default; the settings refuse bad values.
//   2. The parser refuses a bad table outright: unknown field, wrong schema, missing profile or topic, unknown tag or
//      phase, an alias claimed twice, a reserved or rest alias, an alias not written as matched, markup, a control
//      character, a template other than {name}, no fallback, one rumor, an oversized file.
//   3. A reload that fails keeps the table in use; with no valid table at all, talk is off.
//   4. Matching is exact words: punctuation and case fold, "forest" is not "rest", a sentence with "job" in it is not the
//      job topic, overlength speech is ignored, an NPC's name first addresses it.
//   5. Speech through the engine: one reply, from the nearest resident (ties to the lowest serial, a named one first),
//      to the speaker's own client only; yells, ghosts, unknown words and the switch off get nothing; the 3-second and
//      15-second cooldowns hold and lift on time.
//   6. The tutorial phase is read only: its own unfinished core gets the core line, another character or no record gets
//      the visitor line, a finished core falls back too; nothing is created or changed.
//   7. The topic menu: opening says nothing; a button answers with the same cooldowns; another client, a moved NPC and a
//      switched-off service get nothing.
//   8. A resident's identity comes from its saved tag and survives a save; it is invulnerable and does not walk.
//   9. P647 (review fixes): an unreadable file is a failed load; repeated responses, stock pet orders (multiword too),
//      a wrong region and a non-identifier topic id are refused; off the test shard talk stays off; "Mara move" in a
//      guarded town leaves the residents where they stand; speech a client tags as a pet order reaches the pet.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Engines.Townsfolk;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TownsfolkTalkVerification : IDisposable
{
    private static readonly Point3D Spot = new(1800, 800, 0);

    public TownsfolkTalkVerification()
    {
        ShardTestHost.EnsureAccounts();
        ClusterFTalkRules.Reload();
        ClusterFTownsfolkService.IsTestShard = () => true; // stage 1 is test-shard only (P647)
    }

    public void Dispose()
    {
        ClusterFTownsfolkService.IsTestShard = () => ClusterFStaffHub.IsTestShard;
        ClusterFTownsfolkService.EnabledOverride = null;
        ClusterFTalkRules.Reload();
    }

    // ---------------------------------------------------------------- helpers

    private sealed class Town : IDisposable
    {
        public readonly BaseRegion Region;
        public readonly List<Mobile> Made = new();
        public readonly List<Account> Accounts = new();
        public readonly List<(Mobile Npc, Mobile To, NetState Ns, string Text)> Replies = new();

        public Town(bool guarded = false)
        {
            // guarded: a TownRegion, as New Haven is, so the stock "<name> move" and pet-order speech paths are live (P647).
            var area = new Rectangle3D(new Point3D(Spot.X - 20, Spot.Y - 20, -128), new Point3D(Spot.X + 20, Spot.Y + 20, 127));
            Region = guarded ? new TownRegion("New Haven", Map.Trammel, 50, area) : new BaseRegion("New Haven", Map.Trammel, 50, area);
            Region.Register();
            ClusterFTownsfolkService.EnabledOverride = true;
            ClusterFTownsfolkService.Replied += OnReplied;
            ShardTestClock.Arm();
        }

        private void OnReplied(Mobile npc, Mobile to, NetState ns, string text) => Replies.Add((npc, to, ns, text));

        public ClusterFTownsfolk Resident(string tag, int dx, int dy)
        {
            var npc = new ClusterFTownsfolk(tag);
            npc.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y + dy, Spot.Z), Map.Trammel);
            Made.Add(npc);
            return npc;
        }

        public PlayerMobile Player(int dx = 0, int dy = 0, Account account = null, int slot = 0)
        {
            if (account == null)
            {
                account = new Account($"p631{Guid.NewGuid():N}"[..16], "p631-test-only");
                Accounts.Add(account);
            }

            var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
            pm.AddItem(new Server.Items.Backpack());
            account[slot] = pm;
            pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y + dy, Spot.Z), Map.Trammel);
            Made.Add(pm);
            return pm;
        }

        public void Dispose()
        {
            ClusterFTownsfolkService.Replied -= OnReplied;
            ClusterFTownsfolkService.EnabledOverride = null;
            foreach (var m in Made)
            {
                ClusterFTownsfolkService.ResetPlayer(m);
                m.Delete();
            }

            foreach (var a in Accounts)
            {
                Server.Accounting.Accounts.Remove(a);
            }

            Region.Unregister();
        }
    }

    private static string ShippedJson() => File.ReadAllText(ClusterFTalkRules.FindDataFile()!);

    private static List<string> ErrorsAfter(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(ShippedJson())!.AsObject();
        change(root);
        var (table, errors) = ClusterFTalkRules.Parse(root.ToJsonString());
        Assert.Null(table);
        return errors;
    }

    private static JsonObject Topic(JsonObject root, string profile, string topic) =>
        root["profiles"]!.AsArray().First(p => (string)p!["tag"]! == profile)!["topics"]!.AsArray()
            .First(t => (string)t!["id"]! == topic)!.AsObject();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheShippedTableLoadsFromTheBuildersDataFolder()
    {
        var path = ClusterFTalkRules.FindDataFile();
        Assert.NotNull(path);
        // The Dockerfile's copy: in Distribution/Data, which the build also copies beside the test host (and the runtime
        // stage to /modernuo/Data); either way it is Data/ShatteredLegacy/townsfolk-talk.json.
        Assert.EndsWith(Path.Combine("Data", "ShatteredLegacy", "townsfolk-talk.json"), path);

        Assert.True(ClusterFTalkRules.Reload());
        var table = ClusterFTalkRules.Current!;
        Assert.Equal(new[] { "innkeeper", "plaza", "trades" }, table.Profiles.Keys.Order().ToArray());
        foreach (var profile in table.Profiles.Values)
        {
            foreach (var id in ClusterFTalkRules.LaunchTopics)
            {
                Assert.NotNull(profile.Topic(id));
            }

            Assert.True(profile.Topic("rumors")!.Responses.Count >= 2);
        }

        Assert.Equal("the New Haven Inn", table.Profile("innkeeper")!.Region);
        Assert.Equal("New Haven", table.Profile("plaza")!.Region);

        Assert.False(ClusterFTownsfolkService.SettingEnabled);
        Assert.Equal(3, ClusterFTownsfolkService.TalkRange);
        Assert.Equal(3, ClusterFTownsfolkService.TalkCooldownSeconds);
        Assert.Equal(15, ClusterFTownsfolkService.RepeatCooldownSeconds);
        Assert.False(ClusterFTownsfolkService.Enabled);
        Assert.Null(ClusterFTownsfolkService.SettingsProblem());
        Assert.NotNull(ClusterFTownsfolkService.SettingsProblem(0, 3, 15));
        Assert.NotNull(ClusterFTownsfolkService.SettingsProblem(4, 3, 15));
        Assert.NotNull(ClusterFTownsfolkService.SettingsProblem(3, 0, 15));
        Assert.NotNull(ClusterFTownsfolkService.SettingsProblem(3, 3, 0));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheParserRefusesABadTableOutright()
    {
        var (good, none) = ClusterFTalkRules.Parse(ShippedJson());
        Assert.NotNull(good);
        Assert.Empty(none);

        Assert.Contains(ErrorsAfter(r => r["action"] = "give gold"), e => e.Contains("unknown field \"action\""));
        Assert.Contains(ErrorsAfter(r => r["schemaVersion"] = 2), e => e.Contains("schemaVersion"));
        Assert.Contains(ErrorsAfter(r => r["profiles"]!.AsArray().RemoveAt(1)), e => e.Contains("launch profile trades is missing"));
        Assert.Contains(ErrorsAfter(r => r["profiles"]![0]!["tag"] = "banker"), e => e.Contains("not a known profile tag"));
        Assert.Contains(ErrorsAfter(r =>
        {
            var topics = r["profiles"]![0]!["topics"]!.AsArray();
            topics.Remove(topics.First(t => (string)t!["id"]! == "inn"));
        }), e => e.Contains("launch topic inn is missing"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add("hello")), e => e.Contains("claimed twice"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add("rest")), e => e.Contains("reserved"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add("take a rest")), e => e.Contains("reserved"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add("vendor buy")), e => e.Contains("reserved"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add("Your Job?")), e => e.Contains("must be written as it is matched"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["responses"]![0]!["text"] = "<b>Work</b>"), e => e.Contains("markup"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["responses"]![0]!["text"] = "Work\u0007"), e => e.Contains("control"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["responses"]![0]!["text"] = "I am {type}"), e => e.Contains("template"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["responses"]![0]!["phase"] = "night"), e => e.Contains("unknown phase"));
        Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["responses"]![0]!["phase"] = "core"), e => e.Contains("needs a response for phase any"));
        Assert.Contains(ErrorsAfter(r =>
        {
            var rumors = Topic(r, "trades", "rumors")["responses"]!.AsArray();
            while (rumors.Count > 1)
            {
                rumors.RemoveAt(1);
            }
        }), e => e.Contains("at least 2"));

        var (big, bigErrors) = ClusterFTalkRules.Parse(ShippedJson() + new string(' ', ClusterFTalkRules.MaxFileBytes));
        Assert.Null(big);
        Assert.Contains(bigErrors, e => e.Contains("bytes"));
        Assert.Null(ClusterFTalkRules.Parse("{ not json").Table);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AFailedReloadKeepsTheTableInUse()
    {
        var bad = Path.Combine(Path.GetTempPath(), $"p631-{Guid.NewGuid():N}.json");
        File.WriteAllText(bad, "{\"schemaVersion\": 1, \"profiles\": []}");
        try
        {
            Assert.True(ClusterFTalkRules.Reload());
            var inUse = ClusterFTalkRules.Current;
            Assert.NotNull(inUse);

            Assert.False(ClusterFTalkRules.Reload(bad));
            Assert.Same(inUse, ClusterFTalkRules.Current);
            Assert.NotEmpty(ClusterFTalkRules.LastErrors);

            Assert.False(ClusterFTalkRules.Reload(bad + ".missing"));
            Assert.Same(inUse, ClusterFTalkRules.Current);

            ClusterFTalkRules.Unload();
            Assert.False(ClusterFTalkRules.Reload(bad));
            Assert.Null(ClusterFTalkRules.Current);
            ClusterFTownsfolkService.EnabledOverride = true;
            Assert.False(ClusterFTownsfolkService.Enabled);
        }
        finally
        {
            ClusterFTownsfolkService.EnabledOverride = null;
            File.Delete(bad);
            ClusterFTalkRules.Reload();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void MatchingIsExactWords()
    {
        var plaza = ClusterFTalkRules.Current!.Profile("plaza")!;
        Assert.Equal("hello there", ClusterFTalkRules.Normalize("  Hello,   THERE! "));
        Assert.Equal("hello", ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("Hello, there!"), "Mara")!.Id);
        Assert.Equal("job", ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("What is your job?"), "Mara")!.Id);
        Assert.Equal("hello", ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("MARA, hello"), "Mara")!.Id);
        Assert.Null(ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("Ellis, hello"), "Mara")); // another's name

        Assert.Null(ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("forest"), "Mara"));
        Assert.Null(ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("rest"), "Mara"));
        Assert.Null(ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("i need a job today"), "Mara"));
        Assert.Null(ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize("vendor buy"), "Mara"));
        Assert.Null(ClusterFTalkRules.Normalize(new string('a', ClusterFTalkRules.MaxSpeechLength + 1)));
        Assert.Null(ClusterFTalkRules.Match(plaza, ClusterFTalkRules.Normalize(new string('a', 161)), "Mara"));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void SpeechGetsOneReplyFromTheRightResidentOnlyToTheSpeaker()
    {
        using var t = new Town();
        var mara = t.Resident(ClusterFTownsfolk.PlazaTag, 1, 0);
        var ellis = t.Resident(ClusterFTownsfolk.TradesTag, 0, 2);
        var pm = t.Player();
        var other = t.Player(-1, 0);
        using var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        pm.DoSpeech("Hello!", [], MessageType.Regular, 0x3B2);
        Assert.Single(t.Replies);
        Assert.Same(mara, t.Replies[0].Npc);
        Assert.Same(pm, t.Replies[0].To);
        Assert.Same(ns, t.Replies[0].Ns);
        Assert.DoesNotContain(t.Replies, r => r.To == other);

        // The 3-second cooldown, then the 15-second repeat for the same topic from the same NPC.
        pm.DoSpeech("goodbye", [], MessageType.Regular, 0x3B2);
        Assert.Single(t.Replies);
        ShardTestClock.AdvanceSeconds(3);
        pm.DoSpeech("hello", [], MessageType.Regular, 0x3B2);
        Assert.Single(t.Replies);
        pm.DoSpeech("Ellis, hello", [], MessageType.Regular, 0x3B2);
        Assert.Equal(2, t.Replies.Count);
        Assert.Same(ellis, t.Replies[1].Npc);
        ShardTestClock.AdvanceSeconds(12.1);
        pm.DoSpeech("hello", [], MessageType.Regular, 0x3B2);
        Assert.Equal(3, t.Replies.Count);
        Assert.Same(mara, t.Replies[2].Npc);
        Assert.Contains(t.Replies[2].Text, ClusterFTalkRules.Current!.Profile("plaza")!.Topic("hello")!.Responses.Select(r => r.Text));

        // The name response uses the NPC's own name.
        ShardTestClock.AdvanceSeconds(3);
        pm.DoSpeech("what is your name", [], MessageType.Whisper, 0x3B2);
        Assert.Equal(4, t.Replies.Count);
        Assert.StartsWith("Mara.", t.Replies[3].Text);

        // Nothing for a yell, unknown words, a ghost, or the switch off.
        ShardTestClock.AdvanceSeconds(3);
        pm.DoSpeech("job", [], MessageType.Yell, 0x3B2);
        pm.DoSpeech("forest", [], MessageType.Regular, 0x3B2);
        pm.DoSpeech("buy", [], MessageType.Regular, 0x3B2);
        Assert.Equal(4, t.Replies.Count);
        ClusterFTownsfolkService.EnabledOverride = false;
        pm.DoSpeech("job", [], MessageType.Regular, 0x3B2);
        Assert.Equal(4, t.Replies.Count);
        ClusterFTownsfolkService.EnabledOverride = true;
        other.Kill();
        other.DoSpeech("job", [], MessageType.Regular, 0x3B2);
        Assert.Equal(4, t.Replies.Count);

        // Equally close: the lower serial answers.
        var a = t.Resident(ClusterFTownsfolk.PlazaTag, 10, 1);
        var b = t.Resident(ClusterFTownsfolk.PlazaTag, 10, -1);
        var mid = t.Player(10, 0);
        mid.DoSpeech("job", [], MessageType.Regular, 0x3B2);
        Assert.Same(a.Serial < b.Serial ? a : b, t.Replies[^1].Npc);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheTutorialPhaseIsReadOnly()
    {
        using var t = new Town();
        var mara = t.Resident(ClusterFTownsfolk.PlazaTag, 1, 0);
        var pm = t.Player();
        var acct = (Account)pm.Account;
        var coreLine = ClusterFTalkRules.Current!.Profile("plaza")!.Topic("help")!.Responses.First(r => r.Phase == TalkPhase.Core).Text;

        // No account data at all: the visitor line, and none is created.
        Assert.Null(ClusterFAccountPersistence.Get(acct));
        Assert.Equal(TalkPhase.Any, ClusterFTownsfolkService.PhaseOf(pm));
        Assert.NotEqual(coreLine, ClusterFTownsfolkService.Answer(mara, pm, "help"));
        Assert.Null(ClusterFAccountPersistence.Get(acct));

        var record = ClusterFAccountPersistence.GetOrCreate(acct).Tutorial;
        record.State = TutorialState.InCore;
        record.CharacterSerial = pm.Serial.Value;
        Assert.Equal(TalkPhase.Core, ClusterFTownsfolkService.PhaseOf(pm));
        ShardTestClock.AdvanceSeconds(20);
        Assert.Equal(coreLine, ClusterFTownsfolkService.Answer(mara, pm, "help"));

        var alt = t.Player(0, 1, acct, 1);
        Assert.Equal(TalkPhase.Any, ClusterFTownsfolkService.PhaseOf(alt));

        foreach (var s in TutorialStations.Core)
        {
            record.StationsDone.Add(s.Key);
        }

        Assert.Equal(TalkPhase.AfterCore, ClusterFTownsfolkService.PhaseOf(pm));
        ShardTestClock.AdvanceSeconds(20);
        Assert.NotEqual(coreLine, ClusterFTownsfolkService.Answer(mara, pm, "help"));

        Assert.Equal(TutorialState.InCore, record.State);
        Assert.Equal(TutorialStations.Core.Count(), record.StationsDone.Count);
    }

    // ---------------------------------------------------------------- 7

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        GumpRemove?.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void TheTopicMenuSharesTheRulesAndGrantsNothing()
    {
        using var t = new Town();
        var mara = t.Resident(ClusterFTownsfolk.PlazaTag, 1, 0);
        var pm = t.Player();
        var other = t.Player(0, 1);
        using var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        using var otherNs = PacketTestUtilities.CreateTestNetState();
        other.NetState = otherNs;
        otherNs.Mobile = other;

        mara.OnDoubleClick(pm);
        var gump = pm.FindGump<TownsfolkTalkGump>();
        Assert.NotNull(gump);
        Assert.Empty(t.Replies);

        var topics = ClusterFTalkRules.Current!.Profile("plaza")!.Topics.Select(x => x.Id).ToList();
        var job = topics.IndexOf("job") + 1;

        Press(gump, otherNs, job); // another client's response
        Assert.Empty(t.Replies);

        Press(gump, ns, job);
        Assert.Single(t.Replies);
        Assert.Same(pm, t.Replies[0].To);

        gump = pm.FindGump<TownsfolkTalkGump>();
        Assert.NotNull(gump);
        Press(gump, ns, job); // inside the cooldown
        Assert.Single(t.Replies);

        ShardTestClock.AdvanceSeconds(20);
        mara.MoveToWorld(new Point3D(Spot.X + 8, Spot.Y, Spot.Z), Map.Trammel);
        Press(gump, ns, job); // out of range now
        Assert.Single(t.Replies);

        mara.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        ClusterFTownsfolkService.EnabledOverride = false;
        Assert.False(ClusterFTownsfolkService.OpenMenu(mara, pm));
        Press(gump, ns, job);
        Assert.Single(t.Replies);
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void AResidentsIdentityComesFromItsSavedTag()
    {
        var ellis = new ClusterFTownsfolk(ClusterFTownsfolk.TradesTag);
        var stray = new ClusterFTownsfolk("banker");
        try
        {
            Assert.Equal("Ellis", ellis.Name);
            Assert.Equal("the workshop runner", ellis.Title);
            Assert.Equal("trades", ellis.TalkProfile);
            Assert.True(ellis.IsInvulnerable);
            Assert.True(ellis.CantWalk);
            Assert.Equal("Mara", stray.Name);
            Assert.Equal("plaza", stray.TalkProfile);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            ellis.Serialize(writer);
            writer.Flush();
            var copy = new ClusterFTownsfolk(ellis.Serial);
            ellis.Delete();
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal("trades", copy.TalkProfile);
            Assert.Equal("Ellis", copy.Name);
            copy.Delete();
        }
        finally
        {
            ellis.Delete();
            stray.Delete();
        }
    }

    // ---------------------------------------------------------------- 9 (P647: review fixes)

    private static string TempTable(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(ShippedJson())!.AsObject();
        change(root);
        var path = Path.Combine(Path.GetTempPath(), $"p647-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, root.ToJsonString());
        return path;
    }

    private static JsonObject Profile(JsonObject root, string tag) =>
        root["profiles"]!.AsArray().First(p => (string)p!["tag"]! == tag)!.AsObject();

    // P631-a: a table file that cannot be read (held open, unreadable, gone after the check) is a failed load, not a
    // thrown startup or reload.
    [Fact]
    public void AnUnreadableTableFileIsAFailedLoadThatKeepsTheTable()
    {
        var path = TempTable(_ => { });
        try
        {
            Assert.True(ClusterFTalkRules.Reload());
            var inUse = ClusterFTalkRules.Current;
            bool reloaded;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                reloaded = ClusterFTalkRules.Reload(path);
            }

            Assert.False(reloaded);
            Assert.Same(inUse, ClusterFTalkRules.Current);
            Assert.Contains(ClusterFTalkRules.LastErrors, e => e.Contains("unreadable"));
            Assert.True(ClusterFTalkRules.Reload(path)); // the same file, readable again, loads
        }
        finally
        {
            File.Delete(path);
            ClusterFTalkRules.Reload();
        }
    }

    // P631-b: two identical responses in a topic would leave nothing to choose once that line was last said.
    [Fact]
    public void RepeatedIdenticalResponsesAreRefused()
    {
        Assert.Contains(ErrorsAfter(r =>
        {
            var responses = Topic(r, "plaza", "job")["responses"]!.AsArray();
            responses.Add(responses[0]!.DeepClone());
        }), e => e.Contains("listed twice"));
        Assert.Contains(ErrorsAfter(r =>
        {
            var responses = Topic(r, "plaza", "help")["responses"]!.AsArray();
            var copy = responses.First(x => (string)x!["phase"]! == "core")!.DeepClone();
            responses.Add(copy);
        }), e => e.Contains("listed twice"));
    }

    // P631-c: off the test shard neither the switch nor the setting turns talk on.
    [Fact]
    public void TalkCannotBeSwitchedOnOffTheTestShard()
    {
        if (!CommandSystem.Entries.ContainsKey("ClusterFTownsfolk"))
        {
            ClusterFTownsfolkService.Configure();
        }

        var admin = new PlayerMobile { Player = true, AccessLevel = AccessLevel.Owner, Name = "P647 admin" };
        try
        {
            ClusterFTownsfolkService.IsTestShard = () => false;
            CommandSystem.Handle(admin, $"{CommandSystem.Prefix}ClusterFTownsfolk on");
            Assert.NotEqual(true, ClusterFTownsfolkService.EnabledOverride);
            ClusterFTownsfolkService.EnabledOverride = true; // however it got set
            Assert.False(ClusterFTownsfolkService.Enabled);

            ClusterFTownsfolkService.EnabledOverride = null;
            ClusterFTownsfolkService.IsTestShard = () => true;
            CommandSystem.Handle(admin, $"{CommandSystem.Prefix}ClusterFTownsfolk on");
            Assert.True(ClusterFTownsfolkService.Enabled);
        }
        finally
        {
            ClusterFTownsfolkService.EnabledOverride = null;
            admin.Delete();
        }
    }

    // P643-F1: "Mara move" in a guarded town reaches the stock AI, which would shift a CantWalk resident a tile.
    [Fact]
    public void MoveSpeechDoesNotShiftTheStationaryResidents()
    {
        using var t = new Town(guarded: true);
        var mara = t.Resident(ClusterFTownsfolk.PlazaTag, 1, 0);
        var ellis = t.Resident(ClusterFTownsfolk.TradesTag, -1, 0);
        var pm = t.Player();
        Assert.True(mara.IsHumanInTown());
        var maraAt = mara.Location;
        var ellisAt = ellis.Location;

        foreach (var on in new[] { true, false })
        {
            ClusterFTownsfolkService.EnabledOverride = on;
            for (var i = 0; i < 6; i++)
            {
                ShardTestClock.AdvanceSeconds(6); // past the stock five-second order throttle
                pm.DoSpeech("Mara move", [0x9D], MessageType.Regular, 0x3B2);
                pm.DoSpeech("Ellis move", [0x9D], MessageType.Regular, 0x3B2);
                Assert.Equal(maraAt, mara.Location);
                Assert.Equal(ellisAt, ellis.Location);
            }
        }
    }

    // P643-F2: the stock pet orders are reserved, multiword ones too.
    [Fact]
    public void StockPetOrdersAreReserved()
    {
        foreach (var order in new[] { "all follow me", "all guard me", "follow me", "guard me", "drop", "friend", "move" })
        {
            Assert.Contains(ErrorsAfter(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add(order)), e => e.Contains("reserved"));
        }
    }

    // P643-F2 (dispatch): a client tags an order by its keyword, whatever the exact words; a table alias that matches
    // those words cannot outrank the pet, even with the resident standing closer.
    [Fact]
    public void SpeechTaggedAsAPetOrderReachesThePet()
    {
        var path = TempTable(r => Topic(r, "plaza", "job")["aliases"]!.AsArray().Add("lead on"));
        try
        {
            Assert.True(ClusterFTalkRules.Reload(path));
            using var t = new Town(guarded: true);
            var mara = t.Resident(ClusterFTownsfolk.PlazaTag, 1, 0);
            var pm = t.Player();
            var horse = new Horse();
            horse.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y + 1, Spot.Z), Map.Trammel);
            t.Made.Add(horse);
            Assert.True(horse.SetControlMaster(pm));
            horse.ControlOrder = OrderType.Stay;

            pm.DoSpeech("lead on", [0x16C], MessageType.Regular, 0x3B2); // all follow me
            Assert.Empty(t.Replies);
            Assert.Equal(OrderType.Follow, horse.ControlOrder);
            Assert.Same(pm, horse.ControlTarget);

            ShardTestClock.AdvanceSeconds(20);
            pm.DoSpeech("lead on", [], MessageType.Regular, 0x3B2); // the same words untagged are a topic
            Assert.Single(t.Replies);
            Assert.Same(mara, t.Replies[0].Npc);
        }
        finally
        {
            File.Delete(path);
            ClusterFTalkRules.Reload();
        }
    }

    // P643-F3: each launch profile has its region; a typo or a wider region is refused and the old table stays.
    [Fact]
    public void ARegionTypoOrWideningKeepsThePreviousTable()
    {
        Assert.Contains(ErrorsAfter(r => Profile(r, "plaza")["region"] = "New Havn"), e => e.Contains("region"));
        Assert.Contains(ErrorsAfter(r => Profile(r, "trades")["region"] = "Britain"), e => e.Contains("region"));
        Assert.Contains(ErrorsAfter(r => Profile(r, "innkeeper")["region"] = "New Haven"), e => e.Contains("region"));

        var path = TempTable(r => Profile(r, "plaza")["region"] = "New Havn");
        try
        {
            Assert.True(ClusterFTalkRules.Reload());
            var inUse = ClusterFTalkRules.Current;
            Assert.False(ClusterFTalkRules.Reload(path));
            Assert.Same(inUse, ClusterFTalkRules.Current);
            Assert.Equal("New Haven", ClusterFTalkRules.Current!.Profile("plaza")!.Region);
        }
        finally
        {
            File.Delete(path);
            ClusterFTalkRules.Reload();
        }
    }

    // P643-F4: an extra topic's id is a short plain identifier, never markup, space or control characters.
    [Fact]
    public void ExtraTopicIdsArePlainIdentifiers()
    {
        static Action<JsonObject> Extra(string id) => r => Profile(r, "plaza")["topics"]!.AsArray().Add(new JsonObject
        {
            ["id"] = id,
            ["aliases"] = new JsonArray("extra topic"),
            ["responses"] = new JsonArray(new JsonObject { ["phase"] = "any", ["text"] = "Fine weather." })
        });

        foreach (var id in new[] { "<BASEFONT COLOR=#000000>Extra topic", "   ", "extra\u0007", new string('a', 40) })
        {
            Assert.Contains(ErrorsAfter(Extra(id)), e => e.Contains("plain identifier"));
        }

        var root = JsonNode.Parse(ShippedJson())!.AsObject();
        Extra("weather")(root);
        Assert.NotNull(ClusterFTalkRules.Parse(root.ToJsonString()).Table);
    }
}
