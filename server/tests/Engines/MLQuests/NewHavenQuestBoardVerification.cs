// The New Haven training board (D-90) and its quest menu (Q-058). See
// shard-migration/notes/new-haven-quest-board.md and notes/new-haven-quest-picker.md.
//
// Every gump here is asserted on the wire. The player is attached to a real test NetState, the
// board's menu and the stock quest gumps are compiled and sent to it, and each fact reads the
// 0xDD packet back off ns.SendBuffer and inflates it with the managed ZLibStream (the shape
// GumpOnTheWireVerification proves). Buttons go back in the way a client's do: a 0xB1 body
// handed to GumpSystem.DisplayGumpResponse, so the gump system's own lookup (by serial and
// type id, and the removal of a gump once it is answered) is part of what is tested.
//
// DoubleClickFromTheGroundOpensTheMenu is the fact that goes red if the one override regresses:
// stock QuestGiverItem.OnDoubleClick answers 1042593 "That is not in your backpack".
// AButtonIsJudgedByTheWorldAtTheClickNotByTheMenu is the one that goes red if the menu's
// response handler starts trusting the snapshot it sent.
//
// SkillAtFiftyLocksTheQuestOnTheBoardToo pins the stock lockout, which the board does NOT
// lift (Q-059).

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Gumps;
using Server.Engines.MLQuests.Items;
using Server.Engines.MLQuests.Objectives;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;
using RowState = Server.Engines.MLQuests.Items.NewHavenQuestBoard.RowState;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewHavenQuestBoardVerification
{
    private const int NotInBackpack = 1042593;
    private const int CannotReach = 1019045;
    private const int KnowAllICanTeach = 1077772;
    private const int NothingForYou = 1080107;

    // NewHavenQuestBoard's serialized bytes at HEAD 9d47298, before the menu existed: the board
    // at HomeLocation on Trammel, LastMoved = now. Captured from that tree's own code in this
    // task's build 0 (notes/new-haven-quest-picker.md). A new field or a version bump changes
    // these, and every player's held board quest is saved against a board's serial.
    private const string SaveShapeAtHead = "090000001010160400AE0D0D0A0EC5C10201010000000000000000";

    private static readonly Point3D BoardSpot = new(1000, 1000, 0);

    private readonly ITestOutputHelper _out;

    public NewHavenQuestBoardVerification(ITestOutputHelper output)
    {
        _out = output;

        // The server calls MLQuestSystem.Configure; the test host calls no Configure at all.
        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        NewHavenQuestBoard.Register();
    }

    // ---------------------------------------------------------------- helpers

    private static MLQuest Quest<T>() where T : MLQuest => MLQuestSystem.FindQuest(typeof(T));

    private static SkillName SkillOf(MLQuest quest) =>
        Assert.IsType<GainSkillObjective>(Assert.Single(quest.Objectives)).Skill;

    private static int Button<T>() where T : MLQuest => Array.IndexOf(NewHavenQuestBoard.QuestTypes, typeof(T)) + 1;

    private static NewHavenQuestBoard PlaceBoard()
    {
        var board = new NewHavenQuestBoard();
        board.MoveToWorld(BoardSpot, Map.Trammel);
        return board;
    }

    // A player one tile from the board with every board skill at `lockedFixed` except the
    // `open` ones, which sit at 30.0 like a new character's.
    private static PlayerMobile NewPlayer(int lockedFixed, params SkillName[] open)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());

        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var skill = SkillOf(MLQuestSystem.FindQuest(type));
            pm.Skills[skill].BaseFixedPoint = Array.IndexOf(open, skill) >= 0 ? 300 : lockedFixed;
        }

        pm.MoveToWorld(new Point3D(BoardSpot.X + 1, BoardSpot.Y, BoardSpot.Z), Map.Trammel);
        return pm;
    }

    private static PlayerMobile Online(PlayerMobile pm, out NetState ns)
    {
        ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm; // login does this; Mobile.NetState does not, and a gump reply reads it
        return pm;
    }

    // A facet change sends the account's feature flags, and a test NetState has no account, so
    // the player changes map with the connection set aside.
    private static void MoveMap(PlayerMobile pm, Point3D where, Map map)
    {
        var ns = pm.NetState;
        pm.NetState = null;
        pm.MoveToWorld(where, map);
        pm.NetState = ns;
    }

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    // Every 0xC1 localized message sent on `ns` since `from`, as (speaker serial, cliloc).
    private static List<(uint, int)> Messages(NetState ns, int from)
    {
        var span = ns.SendBuffer.GetReadSpan();
        var found = new List<(uint, int)>();

        for (var i = from; i + 18 <= span.Length; i++)
        {
            if (span[i] != 0xC1)
            {
                continue;
            }

            var len = (span[i + 1] << 8) | span[i + 2];
            if (len < 48 || i + len > span.Length)
            {
                continue;
            }

            var serial = (uint)((span[i + 3] << 24) | (span[i + 4] << 16) | (span[i + 5] << 8) | span[i + 6]);
            var number = (span[i + 14] << 24) | (span[i + 15] << 16) | (span[i + 16] << 8) | span[i + 17];
            found.Add((serial, number));
            i += len - 1;
        }

        return found;
    }

    // Whether `text` went out on `ns` since `from`, in either encoding a text packet uses.
    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0 ||
               span.IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
    }

    private static T Open<T>(PlayerMobile pm) where T : BaseGump => pm.GetGumps().Find<T>();

    private static int CountOpen<T>(PlayerMobile pm) where T : BaseGump
    {
        var count = 0;
        foreach (var gump in pm.GetGumps())
        {
            if (gump is T)
            {
                count++;
            }
        }

        return count;
    }

    private record WireGump(int Length, string Layout, List<string> Strings);

    private static byte[] Inflate(ReadOnlySpan<byte> packed, int expectedLength)
    {
        using var input = new MemoryStream(packed.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        Assert.Equal(expectedLength, (int)output.Length);
        return output.ToArray();
    }

    // The 0xDD for `gump` on `ns`, found by its serial and type id and decoded. Fails if the gump
    // never reached the wire.
    private static WireGump OnTheWire(NetState ns, BaseGump gump)
    {
        Assert.NotNull(gump);
        var span = ns.SendBuffer.GetReadSpan();

        for (var i = 0; i + 27 <= span.Length; i++)
        {
            if (span[i] != 0xDD ||
                BinaryPrimitives.ReadUInt32BigEndian(span[(i + 3)..]) != (uint)gump.Serial ||
                BinaryPrimitives.ReadInt32BigEndian(span[(i + 7)..]) != gump.TypeID)
            {
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(span[(i + 1)..]);
            var wire = span.Slice(i, length);

            var pos = 19;
            var layoutPacked = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
            var layoutLength = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
            pos += 8;
            var layout = Encoding.ASCII.GetString(Inflate(wire.Slice(pos, layoutPacked), layoutLength));
            pos += layoutPacked;

            var count = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]);
            pos += 4;
            var strings = new List<string>();

            if (count > 0)
            {
                var stringsPacked = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
                var stringsLength = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
                pos += 8;
                var raw = Inflate(wire.Slice(pos, stringsPacked), stringsLength);
                pos += stringsPacked;

                for (var s = 0; s < raw.Length;)
                {
                    var chars = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(s));
                    strings.Add(Encoding.BigEndianUnicode.GetString(raw, s + 2, chars * 2));
                    s += 2 + chars * 2;
                }
            }
            else
            {
                pos += 4;
            }

            Assert.Equal(length, pos); // the parse used exactly the packet
            Assert.Equal(count, strings.Count);
            return new WireGump(length, layout, strings);
        }

        Assert.Fail($"{gump.GetType().Name} {gump.Serial} never reached the wire");
        return null;
    }

    // A button press, the way the client sends one: a 0xB1 body through the gump system.
    private static void Press(NetState ns, BaseGump gump, int buttonId)
    {
        var body = new byte[20];
        BinaryPrimitives.WriteUInt32BigEndian(body, (uint)gump.Serial);
        BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(4), gump.TypeID);
        BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(8), buttonId);
        // no switches, no text entries
        GumpSystem.DisplayGumpResponse(ns, new SpanReader(body));
    }

    // The menu's rows as the client receives them: (title cliloc, state label, reply button or 0).
    private static List<(int Title, string Label, int Button)> Rows(WireGump gump)
    {
        var titles = Regex.Matches(gump.Layout, @"\{ xmfhtmlgumpcolor 180 (\d+) 195 20 (\d+) ")
            .Select(m => (Y: int.Parse(m.Groups[1].Value), Number: int.Parse(m.Groups[2].Value))).ToList();
        var labels = Regex.Matches(gump.Layout, @"\{ text 385 (\d+) \d+ (\d+) \}")
            .Select(m => gump.Strings[int.Parse(m.Groups[2].Value)]).ToList();
        var buttons = Regex.Matches(gump.Layout, @"\{ button 15 \d+ 4005 4007 1 0 (\d+) \}")
            .Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

        Assert.Equal(titles.Count, labels.Count);

        return titles.Select(
                (t, i) =>
                {
                    var button = Array.FindIndex(
                        NewHavenQuestBoard.QuestTypes,
                        type => MLQuestSystem.FindQuest(type).Title.Number == t.Number
                    ) + 1;
                    return (t.Number, labels[i], buttons.Contains(button) ? button : 0);
                }
            )
            .ToList();
    }

    // cc-P15: a double-click opens the Guild Directory, the board's first page; the menu is its
    // "Training quests" tab, pressed here the way a client presses it.
    private static NewHavenQuestPickerGump OpenMenu(NewHavenQuestBoard board, PlayerMobile pm)
    {
        board.OnDoubleClick(pm);
        var directory = Open<GuildProgressGump>(pm);
        Assert.NotNull(directory);

        if (pm.NetState != null)
        {
            Press(pm.NetState, directory, GuildProgressGump.BtnTrainingQuests);
        }
        else
        {
            board.OpenTrainingQuests(pm);
        }

        return Open<NewHavenQuestPickerGump>(pm);
    }

    private static void Cleanup(PlayerMobile pm, params Item[] items)
    {
        MLQuestSystem.HandleDeletion(pm);
        var ns = pm.NetState;
        pm.NetState = null;

        if (ns != null)
        {
            ns.Mobile = null;
            ns.Dispose();
        }
        pm.Delete();

        foreach (var item in items)
        {
            item.Delete();
        }
    }

    // ---------------------------------------------------------------- facts

    [Fact]
    public void BoardIsQuesterForExactlyTheTwentySixNewHavenTrainingQuests()
    {
        var board = new NewHavenQuestBoard();

        Assert.Equal(26, NewHavenQuestBoard.QuestTypes.Length);
        Assert.Equal(26, new HashSet<Type>(NewHavenQuestBoard.QuestTypes).Count);
        Assert.Equal(26, board.MLQuests.Count);
        Assert.True(board.CanGiveMLQuest);
        Assert.False(board.Movable);
        Assert.Equal(0xA0C5, board.ItemID);

        var skills = new HashSet<SkillName>();

        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var quest = MLQuestSystem.FindQuest(type);

            // The same instance the board holds, so trainer and board share one quest object.
            Assert.Contains(quest, board.MLQuests);
            Assert.True(quest.Activated);
            Assert.True(quest.OneTimeOnly);

            // The menu replaces FindQuest's steps 2-4. Step 3 (chain offers) and the escort
            // branch of step 1 can never apply to these 26; step 2 (deliveries) would need a
            // stock quest to name this board, which no stock quest can.
            Assert.False(quest.IsChainTriggered);
            Assert.Null(quest.NextQuest);
            Assert.False(quest.IsEscort);

            var objective = Assert.IsType<GainSkillObjective>(Assert.Single(quest.Objectives));
            Assert.Equal(500, objective.ThresholdFixed);
            Assert.True(objective.UseReal);
            Assert.True(skills.Add(objective.Skill)); // one quest per skill
            Assert.True(quest.Title.Number > 0);
        }

        // Registering twice must not append a second copy of every quest.
        NewHavenQuestBoard.Register();
        Assert.Equal(26, MLQuestSystem.FindQuestList(typeof(NewHavenQuestBoard)).Count);

        board.Delete();
    }

    [Fact]
    public void SaveShapeIsUnchanged()
    {
        var board = new NewHavenQuestBoard();
        board.MoveToWorld(NewHavenQuestBoard.HomeLocation, Map.Trammel);
        board.LastMoved = Core.Now;

        var writer = new BufferWriter(new byte[256], true, new ConcurrentQueue<Type>());
        board.Serialize(writer);
        var hex = Convert.ToHexString(writer.Buffer, 0, (int)writer.Position);
        _out.WriteLine($"board save: {hex} ({writer.Position} bytes)");
        board.Delete();

        Assert.Equal(SaveShapeAtHead, hex);
    }

    [Fact]
    public void DoubleClickFromTheGroundOpensTheMenu()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(300), out var ns);

        Assert.False(board.IsChildOf(pm.Backpack));

        var before = Mark(ns);
        var menu = OpenMenu(board, pm);
        var said = Messages(ns, before);
        _out.WriteLine($"from the ground: menu={menu != null}, said {string.Join(", ", said)}");

        Assert.NotNull(menu);
        OnTheWire(ns, menu);
        Assert.DoesNotContain(said, m => m.Item2 == NotInBackpack);

        // A second double-click replaces the menu rather than stacking another.
        var second = OpenMenu(board, pm);
        Assert.NotEqual(menu.Serial, second.Serial);
        Assert.Equal(1, CountOpen<NewHavenQuestPickerGump>(pm));

        // Out of reach, and in reach on another map: refused before anything is sent.
        foreach (var (where, map) in new[]
                 {
                     (new Point3D(BoardSpot.X + 3, BoardSpot.Y, BoardSpot.Z), Map.Trammel),
                     (new Point3D(BoardSpot.X + 1, BoardSpot.Y, BoardSpot.Z), Map.Felucca)
                 })
        {
            pm.CloseGump<NewHavenQuestPickerGump>();
            MoveMap(pm, where, map);
            before = Mark(ns);
            board.OnDoubleClick(pm);
            said = Messages(ns, before);
            Assert.Contains(said, m => m.Item2 == CannotReach);
            Assert.False(pm.HasGump<NewHavenQuestPickerGump>());
        }

        Cleanup(pm, board);
    }

    [Fact]
    public void TheMenuListsAllTwentySixOnTwoPagesWithinTheLimits()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(300), out var ns);

        var menu = OpenMenu(board, pm);
        var wire = OnTheWire(ns, menu);
        _out.WriteLine($"menu: 0xDD, {wire.Length} bytes; layout {wire.Layout.Length} chars; {wire.Strings.Count} strings");
        _out.WriteLine($"menu box: {NewHavenQuestPickerGump.Width} x {NewHavenQuestPickerGump.Height} at ({menu.X}, {menu.Y})");

        // The packet's length field is a u16 and BaseGump compiles into one 64 KB buffer.
        Assert.InRange(wire.Length, 1, ushort.MaxValue);
        Assert.True(wire.Length < 8192, $"menu packet {wire.Length} bytes");

        // Two client-side pages of 13, no third.
        Assert.Contains("{ page 1 }", wire.Layout);
        Assert.Contains("{ page 2 }", wire.Layout);
        Assert.DoesNotContain("{ page 3 }", wire.Layout);
        Assert.Matches(@"\{ button \d+ \d+ 4005 4007 0 2 0 \}", wire.Layout); // page 1 -> 2
        Assert.Matches(@"\{ button \d+ \d+ 4014 4016 0 1 0 \}", wire.Layout); // page 2 -> 1

        // The box fits the smallest classic game window, 640 x 480, drawn from (50, 50).
        Assert.InRange(menu.X + NewHavenQuestPickerGump.Width, 0, 640);
        Assert.InRange(menu.Y + NewHavenQuestPickerGump.Height, 0, 480);

        // Every quest, once, by its own title cliloc, in QuestTypes order; its skill beside it.
        var rows = Rows(wire);
        Assert.Equal(
            NewHavenQuestBoard.QuestTypes.Select(t => MLQuestSystem.FindQuest(t).Title.Number),
            rows.Select(r => r.Title)
        );
        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var skill = AosSkillBonuses.GetLabel(SkillOf(MLQuestSystem.FindQuest(type)));
            Assert.Contains($"{{ xmfhtmlgumpcolor 50 ", wire.Layout);
            Assert.Contains($" 125 20 {skill} 0 0 32767 }}", wire.Layout);
        }

        // A fresh character: all 26 available, each with its button, ids 1-26.
        Assert.All(rows, r => Assert.Equal("Available", r.Label));
        Assert.Equal(Enumerable.Range(1, 26), rows.Select(r => r.Button));
        Assert.Contains("Pick a quest. You carry 0 of 10.", wire.Strings);
        Assert.DoesNotContain(NewHavenQuestPickerGump.FullBanner, wire.Strings);

        Cleanup(pm, board);
    }

    [Fact]
    public void EachRowShowsTheStateTheStockPredicatesGive()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(300), out var ns);
        var recaro = new Recaro();
        recaro.MoveToWorld(new Point3D(BoardSpot.X, BoardSpot.Y + 1, BoardSpot.Z), Map.Trammel);

        var fencing = Quest<EnGuarde>();
        var mining = Quest<TheDeluciansLostMine>();
        var magery = Quest<TheMagesApprentice>();
        var smithing = Quest<ItsHammerTime>();
        var swords = Quest<TheWayOfTheBlade>();
        var context = MLQuestSystem.GetOrCreateContext(pm);

        fencing.OnAccept(recaro, pm); // taken from the trainer
        mining.OnAccept(board, pm);   // taken here, skill still 30
        magery.OnAccept(board, pm);   // taken here, skill now 50
        pm.Skills[SkillOf(magery)].BaseFixedPoint = 500;

        smithing.OnAccept(board, pm); // taken here and turned in
        pm.Skills[SkillOf(smithing)].BaseFixedPoint = 500;
        var smithingInstance = context.FindInstance(smithing);
        smithingInstance.ContinueReportBack(false);
        smithingInstance.ClaimRewards();
        Assert.True(context.HasDoneQuest(smithing));

        pm.Skills[SkillOf(swords)].BaseFixedPoint = 500; // never taken, now past 50

        var expected = new Dictionary<MLQuest, RowState>
        {
            [fencing] = RowState.InProgress, // cc-P17 PT-03: a trainer's quest is the board's to take back too
            [mining] = RowState.InProgress,
            [magery] = RowState.ReadyToTurnIn,
            [smithing] = RowState.Done, // done wins over past 50: its skill is at 50 too
            [swords] = RowState.LockedAtFifty
        };

        AssertRows(board, pm, ns, context, expected, RowState.Available);

        // Fill the log to ten from the board: every row that would be on offer now says why not.
        var taken = 0;
        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var quest = MLQuestSystem.FindQuest(type);
            if (context.QuestInstances.Count < MLQuestSystem.MaxConcurrentQuests && !expected.ContainsKey(quest))
            {
                quest.OnAccept(board, pm);
                expected[quest] = RowState.InProgress;
                taken++;
            }
        }

        Assert.Equal(7, taken);
        Assert.True(context.IsFull);
        var wire = AssertRows(board, pm, ns, context, expected, RowState.NoRoom);
        Assert.Contains("Pick a quest. You carry 10 of 10.", wire.Strings);
        Assert.Contains(NewHavenQuestPickerGump.FullBanner, wire.Strings);

        recaro.Delete();
        Cleanup(pm, board);
    }

    private WireGump AssertRows(
        NewHavenQuestBoard board, PlayerMobile pm, NetState ns, MLQuestContext context,
        Dictionary<MLQuest, RowState> expected, RowState otherwise
    )
    {
        var wire = OnTheWire(ns, OpenMenu(board, pm));
        var rows = Rows(wire);

        for (var i = 0; i < NewHavenQuestBoard.QuestTypes.Length; i++)
        {
            var quest = MLQuestSystem.FindQuest(NewHavenQuestBoard.QuestTypes[i]);
            var want = expected.TryGetValue(quest, out var s) ? s : otherwise;
            var state = board.GetRowState(pm, context, quest, out _);

            Assert.Equal(want, state);
            Assert.Equal(NewHavenQuestPickerGump.Label(want), rows[i].Label);
            Assert.Equal(NewHavenQuestPickerGump.HasButton(want) ? i + 1 : 0, rows[i].Button);

            // Available is exactly RandomStarterQuest's eligible pool, and nothing else is.
            var stockWouldOffer = !quest.IsChainTriggered && !context.IsDoingQuest(quest) &&
                                  quest.CanOffer(board, pm, context, false);
            Assert.Equal(stockWouldOffer, state == RowState.Available);
        }

        _out.WriteLine(string.Join("; ", rows.Select(r => $"{r.Title}:{r.Label}")));
        return wire;
    }

    [Fact]
    public void TheBoardCarriesTheWholeStockLoopAndTakeTen()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(300), out var ns);
        var fencing = Quest<EnGuarde>();

        // Pick EnGuarde: the stock offer gump, for that quest and no other.
        Press(ns, OpenMenu(board, pm), Button<EnGuarde>());
        var offer = Open<QuestOfferGump>(pm);
        var offerWire = OnTheWire(ns, offer);
        Assert.Contains($"@#{fencing.Title.Number}@", offerWire.Layout);
        Assert.False(pm.HasGump<NewHavenQuestPickerGump>()); // answered, so the gump system dropped it

        // Accept on the stock gump: the quest is the board's.
        Press(ns, offer, 1);
        var instance = MLQuestSystem.GetContext(pm).FindInstance(fencing);
        Assert.NotNull(instance);
        Assert.Equal(typeof(NewHavenQuestBoard), instance.QuesterType);

        // Held: the stock progress gump.
        var menu = OnTheWire(ns, OpenMenu(board, pm));
        Assert.Contains(Rows(menu), r => r.Title == fencing.Title.Number && r.Label == "Taken, in progress");
        Press(ns, Open<NewHavenQuestPickerGump>(pm), Button<EnGuarde>());
        OnTheWire(ns, Open<QuestConversationGump>(pm));

        // Objective met: the stock report-back, then the stock reward gump, then the reward.
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
        Press(ns, OpenMenu(board, pm), Button<EnGuarde>());
        var report = Open<QuestReportBackGump>(pm);
        OnTheWire(ns, report);
        Press(ns, report, 4);
        var reward = Open<QuestRewardGump>(pm);
        OnTheWire(ns, reward);
        Press(ns, reward, 1);
        Assert.NotNull(pm.Backpack.FindItemByType<RecarosRiposte>());
        Assert.True(MLQuestSystem.GetContext(pm).HasDoneQuest(fencing));

        menu = OnTheWire(ns, OpenMenu(board, pm));
        Assert.Contains(Rows(menu), r => r.Title == fencing.Title.Number && r.Label == "Done" && r.Button == 0);

        // Take ten, through the menu, one after another: the loop the wishlist describes.
        var picked = new List<Type>();
        foreach (var type in NewHavenQuestBoard.QuestTypes.Where(t => t != typeof(EnGuarde)).Take(10))
        {
            Press(ns, OpenMenu(board, pm), Array.IndexOf(NewHavenQuestBoard.QuestTypes, type) + 1);
            Press(ns, Open<QuestOfferGump>(pm), 1);
            picked.Add(type);
        }

        var context = MLQuestSystem.GetContext(pm);
        Assert.Equal(10, context.QuestInstances.Count);
        Assert.All(context.QuestInstances, i => Assert.Equal(typeof(NewHavenQuestBoard), i.QuesterType));
        Assert.Equal(picked, context.QuestInstances.Select(i => i.Quest.GetType()));

        // The eleventh: the menu says why, and a press anyway is refused, stock and in words.
        var eleventh = NewHavenQuestBoard.QuestTypes.First(t => t != typeof(EnGuarde) && !picked.Contains(t));
        menu = OnTheWire(ns, OpenMenu(board, pm));
        Assert.Contains(NewHavenQuestPickerGump.FullBanner, menu.Strings);
        var row = Rows(menu).Single(r => r.Title == MLQuestSystem.FindQuest(eleventh).Title.Number);
        Assert.Equal("No room: 10 held", row.Label);
        Assert.Equal(0, row.Button);

        var before = Mark(ns);
        Press(ns, Open<NewHavenQuestPickerGump>(pm), Array.IndexOf(NewHavenQuestBoard.QuestTypes, eleventh) + 1);
        Assert.Contains(((uint)board.Serial, NothingForYou), Messages(ns, before));
        Assert.True(SentText(ns, before, NewHavenQuestBoard.FullMessage));
        Assert.False(pm.HasGump<QuestOfferGump>());
        Assert.Equal(10, context.QuestInstances.Count);
        Assert.Equal(MLQuestSystem.MaxConcurrentQuests, 10);

        Cleanup(pm, board);
    }

    [Fact]
    public void AButtonIsJudgedByTheWorldAtTheClickNotByTheMenu()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(300), out var ns);
        var fencing = Quest<EnGuarde>();
        var home = pm.Location;

        // Open a menu (EnGuarde available, with its button), change the world, then press.
        List<(uint, int)> PressAfter(Action change, int button = 0)
        {
            pm.CloseGump<QuestOfferGump>();
            var menu = OpenMenu(board, pm);
            Assert.Contains(Rows(OnTheWire(ns, menu)), r => r.Title == fencing.Title.Number && r.Button > 0);
            change();
            var before = Mark(ns);
            Press(ns, menu, button == 0 ? Button<EnGuarde>() : button);
            return Messages(ns, before);
        }

        void NoOffer(string why, List<(uint, int)> said)
        {
            _out.WriteLine($"{why}: {string.Join(", ", said)}");
            Assert.False(pm.HasGump<QuestOfferGump>(), why);
            Assert.Null(MLQuestSystem.GetContext(pm)?.FindInstance(fencing));
        }

        // Walked away.
        NoOffer("walked away", PressAfter(() => pm.MoveToWorld(new Point3D(BoardSpot.X + 3, BoardSpot.Y, BoardSpot.Z), Map.Trammel)));
        pm.MoveToWorld(home, Map.Trammel);

        // Same spot, another facet.
        var said = PressAfter(() => MoveMap(pm, home, Map.Felucca));
        NoOffer("other map", said);
        Assert.Contains(said, m => m.Item2 == CannotReach);
        MoveMap(pm, home, Map.Trammel);

        // The board taken off the map.
        said = PressAfter(() => board.Internalize());
        NoOffer("board internalized", said);
        board.MoveToWorld(BoardSpot, Map.Trammel);

        // The skill passed 50: the stock lockout, spoken by the board.
        said = PressAfter(() => pm.Skills[SkillName.Fencing].BaseFixedPoint = 500);
        NoOffer("skill passed 50", said);
        Assert.Contains(((uint)board.Serial, KnowAllICanTeach), said);
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 300;

        // Forged buttons: ids the menu never had. Nothing happens, nothing throws.
        foreach (var forged in new[] { 27, 28, -1, 1000, int.MaxValue, int.MinValue })
        {
            NoOffer($"button {forged}", PressAfter(() => { }, forged));
        }

        // The client closing it (button 0).
        var closed = OpenMenu(board, pm);
        Press(ns, closed, 0);
        Assert.False(pm.HasGump<QuestOfferGump>());

        // Ten quests taken elsewhere in the meantime.
        var context = MLQuestSystem.GetOrCreateContext(pm);
        said = PressAfter(
            () =>
            {
                foreach (var type in NewHavenQuestBoard.QuestTypes.Where(t => t != typeof(EnGuarde)).Take(10))
                {
                    MLQuestSystem.FindQuest(type).OnAccept(board, pm);
                }
            }
        );
        NoOffer("ten held", said);
        Assert.Contains(((uint)board.Serial, NothingForYou), said);
        foreach (var instance in context.QuestInstances.ToArray())
        {
            instance.Cancel();
        }
        Assert.Empty(context.QuestInstances);

        // The quest taken from a trainer after the menu was sent: no second instance. Since
        // cc-P17 PT-03 the board shows the trainer's quest's progress, and it stays the trainer's.
        var recaro = new Recaro();
        recaro.MoveToWorld(new Point3D(BoardSpot.X, BoardSpot.Y + 1, BoardSpot.Z), Map.Trammel);
        PressAfter(() => fencing.OnAccept(recaro, pm));
        Assert.False(pm.HasGump<QuestOfferGump>());
        Assert.True(pm.HasGump<QuestConversationGump>());
        Assert.Single(context.QuestInstances, i => i.Quest == fencing);
        Assert.Equal(typeof(Recaro), context.FindInstance(fencing).QuesterType);
        pm.CloseGump<QuestConversationGump>();
        context.FindInstance(fencing).Cancel();
        recaro.Delete();

        // Double-click twice and answer both. The second menu replaced the first, so the gump
        // system drops the first's answer; the second's is judged on its own.
        var first = OpenMenu(board, pm);
        var second = OpenMenu(board, pm);
        Press(ns, first, Button<EnGuarde>());
        Assert.False(pm.HasGump<QuestOfferGump>());
        Press(ns, second, Button<EnGuarde>());
        Assert.True(pm.HasGump<QuestOfferGump>());
        pm.CloseGump<QuestOfferGump>();

        // Two menus in flight around one accept: the offer from the first is open when the
        // second menu is sent, so the second still shows EnGuarde available. Accept the offer,
        // then answer the second menu for EnGuarde: the player gets the progress gump for the
        // quest they now hold, never a second offer or a second instance.
        Press(ns, OpenMenu(board, pm), Button<EnGuarde>());
        var offer = Open<QuestOfferGump>(pm);
        var stale = OpenMenu(board, pm);
        Assert.Contains(Rows(OnTheWire(ns, stale)), r => r.Title == fencing.Title.Number && r.Label == "Available");
        Press(ns, offer, 1);
        Press(ns, stale, Button<EnGuarde>());
        Assert.False(pm.HasGump<QuestOfferGump>());
        Assert.True(pm.HasGump<QuestConversationGump>());
        Assert.Single(context.QuestInstances, i => i.Quest == fencing);
        context.FindInstance(fencing).Cancel();

        // The board deleted outright.
        var gone = OpenMenu(board, pm);
        board.Delete();
        Press(ns, gone, Button<EnGuarde>());
        Assert.False(pm.HasGump<QuestOfferGump>());

        // Dead. Not Kill(): in the test host that deletes an account-less PlayerMobile outright
        // (probed: Deleted true, Map null), which tests deletion, not death. A ghost body on a
        // Player is what Mobile.Alive reads as dead, with the mobile still in the world.
        board = PlaceBoard();
        pm.Player = true;
        said = PressAfter(
            () =>
            {
                pm.Body = 0x192; // ghost
                Assert.False(pm.Alive);
                Assert.False(pm.Deleted);
            }
        );
        NoOffer("dead", said);

        Cleanup(pm, board);
    }

    [Fact]
    public void SkillAtFiftyLocksTheQuestOnTheBoardToo()
    {
        var board = PlaceBoard();
        var pm = NewPlayer(500, SkillName.Fencing);
        var fencing = Quest<EnGuarde>();

        pm.Skills[SkillName.Fencing].BaseFixedPoint = 499;
        Assert.True(fencing.CanOffer(board, pm, false));
        Assert.Equal(RowState.Available, board.GetRowState(pm, MLQuestSystem.GetContext(pm), fencing, out _));

        // At 50.0 the stock objective refuses, from the board exactly as from Recaro.
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
        Assert.False(fencing.CanOffer(board, pm, false));
        Assert.Equal(RowState.LockedAtFifty, board.GetRowState(pm, MLQuestSystem.GetContext(pm), fencing, out _));

        // Chase's character: Fencing past 50, everything else fresh. Twenty-five on offer, on the wire.
        var chase = Online(NewPlayer(300), out var ns);
        chase.Skills[SkillName.Fencing].BaseFixedPoint = 510;
        var rows = Rows(OnTheWire(ns, OpenMenu(board, chase)));
        Assert.Equal(25, rows.Count(r => r.Label == "Available"));
        Assert.Contains(rows, r => r.Title == fencing.Title.Number && r.Label == "Past 50: gone" && r.Button == 0);

        Cleanup(chase);
        Cleanup(pm, board);
    }

    // Pinned's own path, unchanged: MLQuestSystem.OnDoubleClick alone does not match a board quest
    // to its trainer. cc-P17 PT-03 goes around it in the trainer's double-click (the facts below).
    [Fact]
    public void TheStockPathAloneDoesNotMatchABoardQuestToItsTrainer()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(500, SkillName.Fencing), out var ns);

        var fencing = Quest<EnGuarde>();
        var recaro = new Recaro();
        recaro.MoveToWorld(new Point3D(BoardSpot.X, BoardSpot.Y + 1, BoardSpot.Z), Map.Trammel);
        Assert.Contains(fencing, recaro.MLQuests);

        fencing.OnAccept(board, pm);

        // The quest's own text says "return to Recaro". Recaro, on the stock path alone, has
        // nothing for a player already doing his only quest.
        var before = Mark(ns);
        MLQuestSystem.OnDoubleClick(recaro, pm);
        var said = Messages(ns, before);
        _out.WriteLine($"Recaro to a board-quest holder: {string.Join(", ", said)}");
        Assert.Contains(((uint)recaro.Serial, NothingForYou), said);

        // Deleting the board does not cancel the quest while the server runs; the instance
        // keeps the board's type, which FindQuest step 1 would match a replacement on. It does
        // not survive a save: MLQuestInstance writes the quester's serial only and drops the
        // quest on load if that board is gone (MLQuestEntry.cs:520, :536-548), which is why
        // NewHavenQuestBoard.Place never deletes a board.
        board.Delete();
        var instance = MLQuestSystem.GetContext(pm).FindInstance(fencing);
        Assert.NotNull(instance);
        Assert.False(instance.Removed);
        Assert.Equal(typeof(NewHavenQuestBoard), instance.QuesterType);

        recaro.Delete();
        Cleanup(pm);
    }

    // ---------------------------------------------------------------- cc-P17 PT-03
    //
    // Chase, 2026-09-30 (D-90): the board and the quest's trainer are interchangeable. A quest taken
    // at either is turned in at either, for the same reward, paid once. The quest done through a
    // P15 guild item is GuildStarterPathVerification.AQuestDoneThroughAGuildItemPaysAtNeitherEnd.

    private static T Trainer<T>(T trainer) where T : BaseCreature
    {
        trainer.MoveToWorld(new Point3D(BoardSpot.X, BoardSpot.Y + 1, BoardSpot.Z), Map.Trammel);
        return trainer;
    }

    private static void TurnIn(NetState ns, PlayerMobile pm)
    {
        var report = Open<QuestReportBackGump>(pm);
        Assert.NotNull(report);
        Press(ns, report, 4);
        var reward = Open<QuestRewardGump>(pm);
        Assert.NotNull(reward);
        Press(ns, reward, 1);
    }

    private static void CloseQuestGumps(PlayerMobile pm)
    {
        pm.CloseGump<QuestOfferGump>();
        pm.CloseGump<QuestConversationGump>();
        pm.CloseGump<QuestReportBackGump>();
        pm.CloseGump<QuestRewardGump>();
    }

    // Done: the board's row says so, and neither the board nor the trainer opens anything for it.
    private static void PaysNothingAtEitherEnd(NewHavenQuestBoard board, BaseCreature trainer, PlayerMobile pm, MLQuest quest)
    {
        CloseQuestGumps(pm);
        var context = MLQuestSystem.GetContext(pm);
        Assert.Equal(RowState.Done, board.GetRowState(pm, context, quest, out _));

        board.OnPick(pm, quest.GetType());
        trainer.OnDoubleClick(pm);

        Assert.False(pm.HasGump<QuestOfferGump>());
        Assert.False(pm.HasGump<QuestReportBackGump>());
        Assert.False(pm.HasGump<QuestRewardGump>());
        Assert.Null(context.FindInstance(quest));
    }

    [Fact]
    public void AQuestTakenAtTheBoardIsTurnedInToItsTrainerAndPaysOnce()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(500, SkillName.Fencing), out var ns);
        var fencing = Quest<EnGuarde>();
        var recaro = Trainer(new Recaro());

        fencing.OnAccept(board, pm);
        var context = MLQuestSystem.GetContext(pm);
        var instance = context.FindInstance(fencing);

        // Not met yet: Recaro shows its progress, and the quest stays the board's (nothing saved changes).
        recaro.OnDoubleClick(pm);
        Assert.True(pm.HasGump<QuestConversationGump>());
        Assert.Same(board, instance.Quester);
        CloseQuestGumps(pm);

        // Met: Recaro takes the report and pays.
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
        recaro.OnDoubleClick(pm);
        TurnIn(ns, pm);

        Assert.Equal(1, pm.Backpack.GetAmount(typeof(RecarosRiposte)));
        Assert.True(context.HasDoneQuest(fencing));

        // Then the other end, and this end again: nothing more.
        PaysNothingAtEitherEnd(board, recaro, pm, fencing);
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(RecarosRiposte)));

        recaro.Delete();
        Cleanup(pm, board);
    }

    [Fact]
    public void AQuestTakenFromTheTrainerIsTurnedInAtTheBoardAndPaysOnce()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(500, SkillName.Mining), out var ns);
        var mining = Quest<TheDeluciansLostMine>();
        var jacob = Trainer(new JacobWaltz());

        mining.OnAccept(jacob, pm);
        var context = MLQuestSystem.GetContext(pm);
        Assert.Equal(typeof(JacobWaltz), context.FindInstance(mining).QuesterType);
        Assert.Equal(RowState.InProgress, board.GetRowState(pm, context, mining, out _));

        pm.Skills[SkillName.Mining].BaseFixedPoint = 500;
        Assert.Equal(RowState.ReadyToTurnIn, board.GetRowState(pm, context, mining, out _));

        // The board takes the report and pays, through its menu.
        Press(ns, OpenMenu(board, pm), Button<TheDeluciansLostMine>());
        TurnIn(ns, pm);

        Assert.Equal(1, pm.Backpack.GetAmount(typeof(JacobsPickaxe)));
        Assert.True(context.HasDoneQuest(mining));

        PaysNothingAtEitherEnd(board, jacob, pm, mining);
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(JacobsPickaxe)));

        jacob.Delete();
        Cleanup(pm, board);
    }

    // Every quest on the board has a trainer that offers it (Data/MLQuests.cfg), so none is
    // board-only and the menu needs no line saying so.
    [Fact]
    public void EveryBoardQuestHasATrainerAtTheOtherEnd()
    {
        var creatureTypes = typeof(Recaro).Assembly.GetTypes()
            .Where(t => typeof(BaseCreature).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var boardOnly = new List<string>();
        foreach (var type in NewHavenQuestBoard.QuestTypes)
        {
            var quest = MLQuestSystem.FindQuest(type);
            var trainers = creatureTypes.Where(t => MLQuestSystem.FindQuestList(t).Contains(quest)).ToList();
            _out.WriteLine($"{type.Name}: {string.Join(", ", trainers.Select(t => t.Name))}");

            if (trainers.Count == 0)
            {
                boardOnly.Add(type.Name);
            }
        }

        Assert.Empty(boardOnly);
    }

    // A quest held before PT-03 keeps the quester it was taken from whichever end the player visits,
    // so its save is unchanged and it still turns in where it did before.
    [Fact]
    public void AQuestHeldBeforePT03KeepsTheQuesterItWasTakenFrom()
    {
        var board = PlaceBoard();
        var pm = Online(NewPlayer(500, SkillName.Fencing), out var ns);
        var fencing = Quest<EnGuarde>();
        var recaro = Trainer(new Recaro());
        var context = MLQuestSystem.GetOrCreateContext(pm);

        fencing.OnAccept(board, pm);
        recaro.OnDoubleClick(pm);
        Assert.Same(board, context.FindInstance(fencing).Quester);
        CloseQuestGumps(pm);

        context.FindInstance(fencing).Cancel();
        fencing.OnAccept(recaro, pm);
        board.OnPick(pm, typeof(EnGuarde));
        Assert.Same(recaro, context.FindInstance(fencing).Quester);
        CloseQuestGumps(pm);

        // And the trainer's own quest still turns in at the trainer, stock.
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
        recaro.OnDoubleClick(pm);
        TurnIn(ns, pm);
        Assert.Equal(1, pm.Backpack.GetAmount(typeof(RecarosRiposte)));

        recaro.Delete();
        Cleanup(pm, board);
    }

    private static List<NewHavenQuestBoard> Boards() =>
        World.Items.Values.OfType<NewHavenQuestBoard>().Where(b => !b.Deleted).ToList();

    // Every fact here deletes its boards; one that failed part-way may not have.
    private void ClearBoards()
    {
        foreach (var leftover in Boards())
        {
            _out.WriteLine($"deleting a board left by another fact: {leftover.Serial} on {leftover.Map}");
            leftover.Delete();
        }
    }

    // D13 and cc-P23. Place() finds a board on any map, Map.Internal included, so it never builds a
    // second. One found off Trammel is reported and left alone unless the caller says move.
    [Fact]
    public void PlaceFindsABoardOnAnyMapAndRefusesToMoveOneOffTrammelUnlessTold()
    {
        ClearBoards();

        // A board on Map.Internal, where a Trammel-only search could not see it (D13).
        var lost = new NewHavenQuestBoard();
        Assert.Equal(Map.Internal, lost.Map);

        var said = NewHavenQuestBoard.Place();
        _out.WriteLine(said);
        Assert.Contains("found 0 on Trammel, 1 elsewhere", said);
        Assert.Contains("not moved", said);
        Assert.Contains(lost.Serial.ToString(), said);
        Assert.Equal(lost, Assert.Single(Boards()));
        Assert.Equal(Map.Internal, lost.Map);

        // The dry run says what move would do and does nothing.
        said = NewHavenQuestBoard.Place(move: true, dryRun: true);
        _out.WriteLine(said);
        Assert.StartsWith("Dry run", said);
        Assert.Contains("would move", said);
        Assert.Equal(Map.Internal, lost.Map);
        Assert.Equal(lost, Assert.Single(Boards()));

        // Told to: moved home, the same board.
        said = NewHavenQuestBoard.Place(move: true);
        _out.WriteLine(said);
        Assert.Contains("moved to", said);
        Assert.Equal(lost, Assert.Single(Boards()));
        Assert.Equal(Map.Trammel, lost.Map);
        Assert.Equal(NewHavenQuestBoard.HomeLocation, lost.Location);

        said = NewHavenQuestBoard.Place();
        Assert.Contains("already at", said);
        Assert.Contains("found 1 on Trammel, 0 elsewhere", said);

        // A second board on another facet: reported, left alone, and the home one kept.
        var felucca = new NewHavenQuestBoard();
        felucca.MoveToWorld(NewHavenQuestBoard.HomeLocation, Map.Felucca);
        said = NewHavenQuestBoard.Place();
        _out.WriteLine(said);
        Assert.Contains("already at", said);
        Assert.Contains("found 1 on Trammel, 1 elsewhere", said);
        Assert.Contains("More than one board exists", said);
        Assert.Equal(Map.Felucca, felucca.Map);
        Assert.Equal(2, Boards().Count);

        // Only the Felucca one left: refused, not moved and not duplicated.
        lost.Delete();
        said = NewHavenQuestBoard.Place();
        _out.WriteLine(said);
        Assert.Contains("not moved", said);
        Assert.Equal(felucca, Assert.Single(Boards()));
        Assert.Equal(Map.Felucca, felucca.Map);

        felucca.Delete();
    }

    // The brief's fact 1: a board already on Trammel, away from home, is moved home and not doubled.
    [Fact]
    public void PlaceWithABoardOnTrammelMakesNoSecondBoard()
    {
        ClearBoards();

        var board = new NewHavenQuestBoard();
        board.MoveToWorld(new Point3D(NewHavenQuestBoard.HomeLocation.X + 3, NewHavenQuestBoard.HomeLocation.Y, NewHavenQuestBoard.HomeLocation.Z), Map.Trammel);

        var said = NewHavenQuestBoard.Place();
        _out.WriteLine(said);
        Assert.Contains("moved to", said);
        Assert.Equal(board, Assert.Single(Boards()));
        Assert.Equal(NewHavenQuestBoard.HomeLocation, board.Location);

        board.Delete();
    }

    // The brief's fact 3: run twice from clean, exactly one board, and a dry run from clean places none.
    [Fact]
    public void PlaceRunTwiceFromCleanMakesExactlyOneBoard()
    {
        ClearBoards();

        var said = NewHavenQuestBoard.Place(dryRun: true);
        _out.WriteLine(said);
        Assert.StartsWith("Dry run", said);
        Assert.Empty(Boards());

        _out.WriteLine(NewHavenQuestBoard.Place());
        _out.WriteLine(NewHavenQuestBoard.Place());

        var board = Assert.Single(Boards());
        Assert.Equal(Map.Trammel, board.Map);
        Assert.Equal(NewHavenQuestBoard.HomeLocation, board.Location);

        board.Delete();
    }
}
