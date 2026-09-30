// GuildStarterPathVerification.cs
//
// cc-P15, F-9: guilds replace the New Haven training quests as the starter path. Notes in
// shard-migration notes/cc-P15-guild-starter-path.md.
//
// Facts (the brief's numbering; 8, old saves load, is in AccountDataSerializerVerification):
//   1. A fresh character with no skill and no gold joins and gets its tools once. Nothing is consumed.
//   2. A second character on the same account gets its own tools and items (PT-06).
//   3. An item taken from the guild marks its quest done and the quest then pays nothing; a quest
//      done first shows its item as received and the guild pays nothing.
//   4. The Necromancer spellbook from the guild has every spell.
//   5. Every SkillName is in exactly one guild, and each of the 26 starter items in exactly one.
//   6. Joining each guild as a gargoyle hands over only items a gargoyle can use. The 26 starter
//      items are checked the same way and listed as data for Chase.
//   7. The directory's arrow resolves to a guildmaster that exists; a missing one reports it.
//   9. The dev reset's Account Data clears JoinedGuilds and every character's starter record.
// And three more: a guild kit never deletes what is worn, the old join task is the Apprentice task,
// and every trainer's line names its hall's guild.
//
// The test host has no tiledata.mul (D27), so equipment cannot be worn and the creation kit's
// EquipItem packs it. Every fact reads the whole of what the character owns, worn or packed.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Items;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildStarterPathVerification
{
    private readonly ITestOutputHelper _out;

    private static readonly Point3D Spot = new(1200, 1200, 0);

    public GuildStarterPathVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();

        // The server calls these Configure methods; the test host calls none.
        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        NewHavenQuestBoard.Register();
        ClusterFGuildSystem.EnsureRegistered();
    }

    // ---------------------------------------------------------------- helpers

    private static bool _startupHooksRun;

    // As GargoyleStartingClothesVerification: new Account(...) needs Accounts.Configure and a password
    // algorithm.
    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private static Account NewAccount() => new($"p15{Guid.NewGuid():N}"[..16], "p15-test-only");

    // A character with every skill at 0, no gold, an empty backpack, standing at Spot.
    private static PlayerMobile NewCharacter(Account account, int slot, Race race = null, bool female = false)
    {
        var pm = new PlayerMobile { Player = true, Race = race ?? Race.Human, Female = female };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 50;
        account[slot] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static GuildDef Guild(string key) => Assert.IsType<GuildDef>(ClusterFGuildSystem.GetDef(key));

    // Everything the character owns: worn, and everything in the backpack at any depth.
    private static List<Item> Owned(PlayerMobile pm)
    {
        var all = new List<Item>();
        foreach (var worn in pm.Items)
        {
            if (worn is not Backpack)
            {
                all.Add(worn);
            }
        }

        if (pm.Backpack != null)
        {
            foreach (var item in pm.Backpack.FindItemsByType<Item>())
            {
                all.Add(item);
            }
        }

        return all;
    }

    private static int CountOf<T>(PlayerMobile pm) where T : Item => Owned(pm).Count(i => i is T);

    private static void Cleanup(Account account, params IEntity[] others)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                MLQuestSystem.HandleDeletion(pm);
                pm.Delete();
            }
        }

        foreach (var other in others)
        {
            other?.Delete();
        }

        Accounts.Remove(account);
    }

    // What pinned's race rules let a gargoyle equip or use: Item.CheckRace (RequiredRaces, Item.cs:2113)
    // and BaseClothing's RequiredRace (BaseClothing.cs:442). Nothing else in pinned gates by race.
    private static bool GargoyleCanUse(Item item) =>
        item.CheckRace(Race.Gargoyle) &&
        (item is not BaseClothing clothing || clothing.RequiredRace == null || clothing.RequiredRace == Race.Gargoyle);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AFreshCharacterWithNoSkillAndNoGoldJoinsAndGetsItsToolsOnce()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        var healers = Guild("healers");

        try
        {
            // Carrying the old tribute: joining must not take it.
            pm.Backpack.DropItem(new Bandage(25));
            var tribute = pm.Backpack.FindItemByType<Bandage>();

            Assert.Equal(0, pm.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(0.0, pm.Skills[SkillName.Healing].Base);
            Assert.True(ClusterFGuildSystem.CanJoin(pm, healers, out var reason), reason);

            ClusterFGuildSystem.Join(pm, healers);

            var owned = Owned(pm);
            _out.WriteLine("after joining the Healers: " + string.Join(", ", owned.Select(i => $"{i.GetType().Name}x{i.Amount}")));

            Assert.True(ClusterFGuildSystem.IsJoined(account, "healers"));
            Assert.Contains("healers", ClusterFGuildStarter.GetRecord(pm).ToolsTaken);
            Assert.Equal("Initiate", ClusterFGuildSystem.GetRankName("healers", ClusterFAccountPersistence.GetOrCreate(account)));

            // Healing, Anatomy, Alchemy and Taste ID at creation: 50 bandages and scissors, 3 bandages
            // and a robe, 4 bottles, a mortar and a robe, nothing. No type twice: Anatomy's bandages
            // and Alchemy's robe are dropped.
            Assert.Equal(1, CountOf<Scissors>(pm));
            Assert.Equal(1, CountOf<MortarPestle>(pm));
            Assert.Equal(1, CountOf<Bottle>(pm));
            Assert.Equal(1, CountOf<Robe>(pm));
            Assert.Contains(owned, i => i is Bandage { Amount: 50 });
            Assert.DoesNotContain(owned, i => i is Bandage { Amount: 3 });

            // The tribute is still there, untouched.
            Assert.False(tribute.Deleted);
            Assert.Equal(25, tribute.Amount);
            Assert.Same(pm.Backpack, tribute.Parent);

            // Once: joining again, or asking for the tools again, hands over nothing.
            var count = Owned(pm).Count;
            Assert.False(ClusterFGuildSystem.CanJoin(pm, healers, out _));
            ClusterFGuildSystem.Join(pm, healers);
            Assert.Null(ClusterFGuildStarter.GiveTools(pm, healers));
            Assert.Equal(count, Owned(pm).Count);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AGuildKitNeverDeletesWhatIsWorn()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);

        try
        {
            // Bushido and Ninjitsu at creation delete the worn pants; Tracking deletes the shoes
            // (CharacterCreation AddSkillItems). A guild handing out the same kit must not.
            var pants = new LongPants { Layer = Layer.OuterLegs };
            var shoes = new Boots { Layer = Layer.Shoes };
            pm.AddItem(pants);
            pm.AddItem(shoes);
            Assert.Same(pants, pm.FindItemOnLayer(Layer.OuterLegs));

            ClusterFGuildSystem.Join(pm, Guild("dojo"));
            ClusterFGuildSystem.Join(pm, Guild("rangers"));

            Assert.False(pants.Deleted);
            Assert.False(shoes.Deleted);
            Assert.Same(pants, pm.FindItemOnLayer(Layer.OuterLegs));
            Assert.Same(shoes, pm.FindItemOnLayer(Layer.Shoes));

            // The kit still arrived: the hakama in the pack, Bushido's and Ninjitsu's books.
            Assert.Equal(1, CountOf<Hakama>(pm));
            Assert.Equal(1, CountOf<BookOfBushido>(pm));
            Assert.Equal(1, CountOf<BookOfNinjitsu>(pm));
            Assert.Equal(1, CountOf<SkinningKnife>(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ASecondCharacterOnTheSameAccountGetsItsOwnToolsAndItems()
    {
        var account = NewAccount();
        var first = NewCharacter(account, 0);
        var second = NewCharacter(account, 1);
        var mining = Guild("mining");

        try
        {
            ClusterFGuildSystem.Join(first, mining);
            Assert.True(ClusterFGuildStarter.TakeStarterItem(first, mining, typeof(TheDeluciansLostMine), out var said), said);
            Assert.Equal(1, CountOf<CompactOreSatchel>(first));
            Assert.Equal(1, CountOf<JacobsPickaxe>(first));

            // PT-06: the account is a member, so the old code said "Already a member" and gave nothing.
            Assert.False(ClusterFGuildSystem.CanJoin(second, mining, out var reason));
            Assert.Equal("Already a member.", reason);
            Assert.Empty(Owned(second));

            ClusterFGuildSystem.Join(second, mining); // "Welcome back"

            var owned = Owned(second);
            _out.WriteLine("second character after Welcome back: " + string.Join(", ", owned.Select(i => i.GetType().Name)));
            Assert.Equal(1, CountOf<Pickaxe>(second) - CountOf<JacobsPickaxe>(second));
            Assert.Equal(1, CountOf<CompactOreSatchel>(second));
            Assert.Equal(1, CountOf<ProspectorsLogbook>(second));
            Assert.Equal(1, CountOf<CompactDispatchLedger>(second));

            Assert.Equal(StarterItemState.Available, ClusterFGuildStarter.GetState(second, typeof(TheDeluciansLostMine)));
            Assert.True(ClusterFGuildStarter.TakeStarterItem(second, mining, typeof(TheDeluciansLostMine), out said), said);
            Assert.Equal(1, CountOf<JacobsPickaxe>(second));

            // Each character's own record; the first cannot take a second pickaxe.
            Assert.False(ClusterFGuildStarter.TakeStarterItem(first, mining, typeof(TheDeluciansLostMine), out _));
            Assert.Equal(1, CountOf<JacobsPickaxe>(first));
            Assert.Equal(2, ClusterFAccountPersistence.GetOrCreate(account).GuildStarterRecordCount);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AnItemTakenFromTheGuildMarksItsQuestDoneAndTheReverse()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        var other = NewCharacter(account, 1);
        var warriors = Guild("warriors");
        var fencing = MLQuestSystem.FindQuest(typeof(EnGuarde));
        var recaro = new Recaro();
        recaro.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);

        try
        {
            ClusterFGuildSystem.Join(pm, warriors);

            // Carrying the quest from the trainer when the guild hands the item over.
            fencing.OnAccept(recaro, pm);
            Assert.NotNull(MLQuestSystem.GetContext(pm).FindInstance(fencing));

            Assert.True(ClusterFGuildStarter.TakeStarterItem(pm, warriors, typeof(EnGuarde), out var said), said);
            Assert.Equal(1, CountOf<RecarosRiposte>(pm));

            var context = MLQuestSystem.GetContext(pm);
            Assert.Null(context.FindInstance(fencing));
            Assert.True(context.HasDoneQuest(fencing));
            Assert.Equal(StarterItemState.Taken, ClusterFGuildStarter.GetState(pm, typeof(EnGuarde)));

            // The quest then pays nothing: the trainer and the board will not offer it again.
            Assert.False(fencing.CanOffer(recaro, pm, false));
            Assert.False(ClusterFGuildStarter.TakeStarterItem(pm, warriors, typeof(EnGuarde), out _));
            Assert.Equal(1, CountOf<RecarosRiposte>(pm));

            // The reverse: the quest turned in first, recorded exactly as MLQuestInstance.ClaimRewards
            // records it (MLQuestEntry.cs:420-423). The guild shows it received and pays nothing.
            ClusterFGuildSystem.Join(other, warriors);
            MLQuestSystem.GetOrCreateContext(other).SetDoneQuest(fencing);

            Assert.Equal(StarterItemState.ReceivedFromTrainer, ClusterFGuildStarter.GetState(other, typeof(EnGuarde)));
            Assert.False(ClusterFGuildStarter.TakeStarterItem(other, warriors, typeof(EnGuarde), out said));
            _out.WriteLine($"quest done first: {said}");
            Assert.Equal(0, CountOf<RecarosRiposte>(other));
        }
        finally
        {
            Cleanup(account, recaro);
        }
    }

    // cc-P17 PT-03: the board and the trainer are interchangeable ends of a quest. One done through a
    // guild item, whether it was held from the board or not held at all, pays at neither end.
    [Fact]
    public void AQuestDoneThroughAGuildItemPaysAtNeitherEnd()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        var ns = Server.Tests.Network.PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        pm.Skills[SkillName.Fencing].BaseFixedPoint = 300;

        var warriors = Guild("warriors");
        var fencing = MLQuestSystem.FindQuest(typeof(EnGuarde));
        var board = new NewHavenQuestBoard();
        board.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        var recaro = new Recaro();
        recaro.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);

        try
        {
            ClusterFGuildSystem.Join(pm, warriors);

            // Held from the board, then the guild hands the item over.
            fencing.OnAccept(board, pm);
            Assert.True(ClusterFGuildStarter.TakeStarterItem(pm, warriors, typeof(EnGuarde), out var said), said);
            Assert.Equal(1, CountOf<RecarosRiposte>(pm));

            // Skill met, as if the player went on training: still nothing at either end.
            pm.Skills[SkillName.Fencing].BaseFixedPoint = 500;
            var context = MLQuestSystem.GetContext(pm);
            Assert.Equal(NewHavenQuestBoard.RowState.Done, board.GetRowState(pm, context, fencing, out _));

            board.OnPick(pm, typeof(EnGuarde));
            recaro.OnDoubleClick(pm);

            Assert.False(pm.HasGump<Server.Engines.MLQuests.Gumps.QuestOfferGump>());
            Assert.False(pm.HasGump<Server.Engines.MLQuests.Gumps.QuestReportBackGump>());
            Assert.False(pm.HasGump<Server.Engines.MLQuests.Gumps.QuestRewardGump>());
            Assert.Null(context.FindInstance(fencing));
            Assert.Equal(1, CountOf<RecarosRiposte>(pm));
        }
        finally
        {
            Cleanup(account, recaro, board);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheNecromancerSpellbookFromTheGuildHasEverySpell()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        var keepers = Guild("keepers");

        try
        {
            ClusterFGuildSystem.Join(pm, keepers);
            Assert.True(ClusterFGuildStarter.TakeStarterItem(pm, keepers, typeof(TheAllureOfDarkMagic), out var said), said);

            var book = Assert.Single(Owned(pm).OfType<NecromancerSpellbook>());
            _out.WriteLine($"book: {book.BookCount} spells in the type, content 0x{book.Content:X}, {book.SpellCount} in the book");

            Assert.Equal((1ul << book.BookCount) - 1, book.Content);
            Assert.Equal(book.BookCount, book.SpellCount);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void EverySkillIsInExactlyOneGuild()
    {
        var skills = Enum.GetValues<SkillName>();
        _out.WriteLine($"{skills.Length} SkillName values, {GuildSkillTable.Skills.Count} guilds in the table");

        var homeless = new List<SkillName>();
        var twice = new List<string>();

        foreach (var skill in skills)
        {
            var guilds = GuildSkillTable.Skills.Where(g => g.Value.Contains(skill)).Select(g => g.Key).ToList();

            if (guilds.Count == 0)
            {
                homeless.Add(skill);
            }
            else if (guilds.Count > 1)
            {
                twice.Add($"{skill}: {string.Join(", ", guilds)}");
            }
        }

        Assert.True(homeless.Count == 0, "in no guild: " + string.Join(", ", homeless));
        Assert.True(twice.Count == 0, "in more than one: " + string.Join("; ", twice));
        Assert.Equal(skills.Length, GuildSkillTable.Skills.Sum(g => g.Value.Length));

        // The table and the registered guilds are the same 17.
        Assert.Equal(17, ClusterFGuildSystem.AllGuilds.Count);
        Assert.Equal(
            ClusterFGuildSystem.AllGuilds.Keys.OrderBy(k => k),
            GuildSkillTable.Skills.Keys.OrderBy(k => k));

        // The 26 starter items: each board quest in exactly one guild, and its skill in that guild.
        foreach (var questType in NewHavenQuestBoard.QuestTypes)
        {
            var owners = GuildStarterItems.Quests.Where(g => g.Value.Contains(questType)).Select(g => g.Key).ToList();
            var owner = Assert.Single(owners);
            var skill = ((Server.Engines.MLQuests.Objectives.GainSkillObjective)MLQuestSystem.FindQuest(questType).Objectives[0]).Skill;
            Assert.True(GuildSkillTable.For(owner).Contains(skill), $"{questType.Name} ({skill}) is with {owner}, which does not teach it");
        }

        Assert.Equal(26, GuildStarterItems.Quests.Sum(g => g.Value.Length));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void JoiningEachGuildAsAGargoyleHandsOverOnlyItemsAGargoyleCanUse()
    {
        var account = NewAccount();
        var unusable = new List<string>();
        var slot = 0;

        try
        {
            foreach (var def in ClusterFGuildSystem.AllGuilds.Values.OrderBy(d => d.Key))
            {
                // An account holds a handful of characters; a fresh account per guild keeps each kit alone.
                if (slot >= account.Length)
                {
                    Cleanup(account);
                    account = NewAccount();
                    slot = 0;
                }

                var gargoyle = NewCharacter(account, slot++, Race.Gargoyle);
                var handed = ClusterFGuildStarter.GiveTools(gargoyle, def);
                Assert.NotNull(handed);

                foreach (var item in handed)
                {
                    if (!GargoyleCanUse(item))
                    {
                        unusable.Add($"{def.Key}: {item.GetType().Name}");
                    }
                }

                _out.WriteLine($"{def.Key,-11} gargoyle kit: {string.Join(", ", handed.Select(i => i.GetType().Name))}");
            }

            Assert.True(unusable.Count == 0, "a gargoyle cannot use: " + string.Join("; ", unusable));
        }
        finally
        {
            Cleanup(account);
        }

        // The 26 starter items, as data for Chase: which a gargoyle can use by pinned's rules, and which
        // are not gargoyle-made (no gargoyle-only race flag), the candidates for a gargish equivalent.
        var starterUnusable = new List<string>();
        var notGargish = new List<string>();
        var probe = new PlayerMobile();

        foreach (var (key, quests) in GuildStarterItems.Quests)
        {
            foreach (var questType in quests)
            {
                var made = new List<Item>();
                foreach (var reward in MLQuestSystem.FindQuest(questType).Rewards)
                {
                    reward.AddRewardItems(probe, made);
                }

                foreach (var item in made)
                {
                    if (!GargoyleCanUse(item))
                    {
                        starterUnusable.Add($"{key}: {item.GetType().Name}");
                    }

                    if (item is BaseArmor or BaseWeapon or BaseClothing or BaseJewel && item.RequiredRaces != Race.AllowGargoylesOnly)
                    {
                        notGargish.Add(item.GetType().Name);
                    }

                    item.Delete();
                }
            }
        }

        probe.Delete();

        _out.WriteLine($"starter items a gargoyle cannot use by pinned's rules ({starterUnusable.Count}): {string.Join(", ", starterUnusable)}");
        _out.WriteLine($"starter items that are not gargoyle-made ({notGargish.Count}): {string.Join(", ", notGargish)}");
        Assert.Empty(starterUnusable);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheDirectoryArrowResolvesToAGuildmasterThatExistsAndAMissingOneReportsIt()
    {
        // Every guild has a location, and every location's NPC resolves to its own guild.
        foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
        {
            Assert.NotEmpty(GuildLocations.For(def.Key));
        }

        foreach (var loc in GuildLocations.All)
        {
            Assert.True(typeof(Mobile).IsAssignableFrom(loc.NpcType), loc.NpcType.Name);
            Assert.Same(ClusterFGuildSystem.GetDef(loc.GuildKey), ClusterFGuildSystem.GetDefForGuildmaster(loc.NpcType));
        }

        var warriors = GuildLocations.For("warriors").Single();
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        pm.MoveToWorld(new Point3D(warriors.Point.X - 5, warriors.Point.Y, warriors.Point.Z), Map.Trammel);

        var guildmaster = new WarriorGuildmaster();
        guildmaster.MoveToWorld(warriors.Point, Map.Trammel);

        try
        {
            var said = ClusterFGuildStarter.ShowTheWay(pm, warriors);
            _out.WriteLine($"present: {said}");
            Assert.StartsWith("Follow the arrow", said);
            var arrow = Assert.IsType<GuildDirectionArrow>(pm.QuestArrow);
            Assert.Same(guildmaster, arrow.Target);
            Assert.Equal(warriors.Point, arrow.Point);
            Assert.True(arrow.Running);

            pm.QuestArrow = null;
            guildmaster.Delete();

            said = ClusterFGuildStarter.ShowTheWay(pm, warriors);
            _out.WriteLine($"missing: {said}");
            Assert.Contains("is not at Warrior's Guild Hall", said);
            Assert.Null(pm.QuestArrow);

            // Another facet: said, not pointed.
            var terMur = GuildLocations.For("artificers").Single(l => l.MapIndex == GuildLocation.TerMurIndex);
            if (Map.TerMur != null)
            {
                var there = new ArtificersGuildmaster();
                there.MoveToWorld(terMur.Point, Map.TerMur);
                said = ClusterFGuildStarter.ShowTheWay(pm, terMur);
                _out.WriteLine($"other facet: {said}");
                Assert.Contains("cannot cross facets", said);
                Assert.Null(pm.QuestArrow);
                there.Delete();
            }
            else
            {
                _out.WriteLine("Ter Mur is not loaded in this host; the other-facet case was not run.");
            }
        }
        finally
        {
            pm.QuestArrow = null;
            guildmaster.Delete();
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheDevResetClearsJoinedGuildsAndEveryCharactersStarterRecord()
    {
        var account = NewAccount();
        var first = NewCharacter(account, 0);
        var second = NewCharacter(account, 1);

        try
        {
            ClusterFGuildSystem.Join(first, Guild("warriors"));
            ClusterFGuildSystem.Join(second, Guild("warriors"));
            ClusterFGuildSystem.Join(second, Guild("tinkers"));
            Assert.True(ClusterFGuildStarter.TakeStarterItem(first, Guild("warriors"), typeof(EnGuarde), out _));

            var data = ClusterFAccountPersistence.GetOrCreate(account);
            data.SetFlag(ClusterFGuildSystem.ApprenticeFlag("warriors"));
            data.SetFlag("league.joined");
            ClusterFGuildStarter.GetRecord(first).WelcomeShown = true;

            Assert.Equal(2, data.JoinedGuilds.Count);
            Assert.Equal(2, data.GuildStarterRecordCount);

            ClusterFDevTools.ExecuteReset(first, first, account, new ResetOptions { AccountData = true });

            Assert.Empty(data.JoinedGuilds);
            Assert.Equal(0, data.GuildStarterRecordCount);
            Assert.False(ClusterFGuildSystem.IsApprentice(data, "warriors"));
            Assert.True(data.HasFlag("league.joined")); // not a guild flag: left alone

            // Both characters can join and take their tools again.
            Assert.True(ClusterFGuildSystem.CanJoin(second, Guild("warriors"), out _));
            Assert.False(ClusterFGuildStarter.HasTools(first, "warriors"));
            Assert.False(ClusterFGuildStarter.HasTools(second, "tinkers"));

            // The starter item's other half is the quest's done-record, which is Quest History, not
            // Account Data: with only Account Data cleared the guild still shows it as received.
            Assert.Equal(StarterItemState.ReceivedFromTrainer, ClusterFGuildStarter.GetState(first, typeof(EnGuarde)));
            ClusterFDevTools.ExecuteReset(first, first, account, new ResetOptions { Quests = true });
            Assert.Equal(StarterItemState.Available, ClusterFGuildStarter.GetState(first, typeof(EnGuarde)));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- the Apprentice task

    [Fact]
    public void TheOldJoinTaskIsTheApprenticeTask()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        var thieves = Guild("thieves");
        var data = ClusterFAccountPersistence.GetOrCreate(account);

        try
        {
            // Stealing 20 used to be needed to join. Now it is not, and it pays nothing on joining.
            ClusterFGuildSystem.Join(pm, thieves);
            Assert.Equal(0, data.GetReputation("thieves"));
            Assert.Equal(0, data.GetCurrency("thieves"));
            Assert.Equal("Initiate", ClusterFGuildSystem.GetRankName("thieves", data));

            Assert.False(ClusterFGuildSystem.CompleteApprenticeTask(pm, thieves, out var reason));
            _out.WriteLine($"not yet: {reason}");

            pm.Skills[SkillName.Stealing].Base = 20.0;
            Assert.True(ClusterFGuildSystem.CompleteApprenticeTask(pm, thieves, out reason), reason);
            Assert.Equal(thieves.JoinReputation, data.GetReputation("thieves"));
            Assert.Equal(thieves.JoinScrip, data.GetCurrency("thieves"));
            Assert.Equal("Apprentice", ClusterFGuildSystem.GetRankName("thieves", data));

            // Once.
            Assert.False(ClusterFGuildSystem.CompleteApprenticeTask(pm, thieves, out _));
            Assert.Equal(thieves.JoinScrip, data.GetCurrency("thieves"));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- the trainers' line

    [Fact]
    public void EveryTrainerNamesTheGuildWhoseHallItStandsIn()
    {
        // Every quest giver of the 26 has a line, and it names its hall's guild.
        var trainerTypes = ClusterFGuildStarter.TrainerHallTable.Keys.ToList();
        Assert.Equal(26, trainerTypes.Count);

        foreach (var type in trainerTypes)
        {
            var trainer = (BaseCreature)Activator.CreateInstance(type);

            try
            {
                var line = ClusterFGuildStarter.TrainerLine(trainer);
                var hallGuild = ClusterFGuildSystem.GetDef(ClusterFGuildStarter.TrainerHallTable[type].GuildKey);
                _out.WriteLine($"{type.Name,-17} {line}");
                Assert.NotNull(line);
                Assert.Contains(hallGuild.Name, line);
            }
            finally
            {
                trainer.Delete();
            }
        }

        // Chiyo teaches Hiding in the Ninja Dojo: the Dojo's hall, the Thieves' Den's item.
        var chiyo = new Chiyo();
        var chiyoLine = ClusterFGuildStarter.TrainerLine(chiyo);
        chiyo.Delete();
        Assert.Contains("Twin Paths Dojo", chiyoLine);
        Assert.Contains("Thieves' Den", chiyoLine);
    }
}
