// cc-P82: formula books (design: "Formula books are sold by the Artificers' Order for guild currency only, granted by
// guild rank index"). One item class reads its book from Configuration/imbuing.json (books) by id, so what a book
// teaches is data. The shop sells each book from its rank (ClusterFGuildSystem.GuildRankIndex, the recipe gating
// shops' rank check) for Essence Shards only (GuildResources.TryPay, as GuildRecipeShop.Buy); the Master Formulary
// (rank 5) also needs the guild trial (ClusterFGuildPromotion.TrialDone). Unread books are ordinary items: tradeable.
// Reading one teaches the whole account (ClusterFImbueKnowledge.ReadBook) and consumes it; the Master Formulary is read
// only by a character who passed the trial.
//
// Also here: [ImbueKnowledge's staff gump (view, grant and revoke an account's imbue knowledge).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ImbueFormulaBook : Item
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _bookId = "";

        [Constructible]
        public ImbueFormulaBook(string bookId) : base(0x0FF4)
        {
            _bookId = bookId;
            var book = ImbuingTable.Current.Book(bookId);
            Hue = RecipeItemHues.ForBand(book?.Rank ?? 1);
            Weight = 1.0;
        }

        public override string DefaultName => ImbuingTable.Current.Book(_bookId)?.Name ?? "a formula book";

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            var book = ImbuingTable.Current.Book(_bookId);
            list.Add(1049644, book == null
                ? "Its formulae have faded"
                : $"Artificers' rank {ClusterFGuildPromotion.RankNameForIndex("artificers", book.Rank)}: {book.Grants.Count} imbue formulae");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            if (!IsChildOf(pm.Backpack))
            {
                pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            ImbueFormulaShop.Read(pm, this);
        }
    }
}

namespace Server
{
    public sealed record ImbueBookRow(string Id, string Name, int Rank, int Price);

    public static class ImbueFormulaShop
    {
        public const string GuildKey = "artificers";

        /// <summary>The books this character may buy now: a member, at or above the book's rank, not yet read by the account.</summary>
        public static List<ImbueBookRow> Rows(PlayerMobile pm)
        {
            var rows = new List<ImbueBookRow>();
            var guild = ClusterFAccountPersistence.GetGuild(pm);
            var data = ClusterFRecipeStoreData(pm);
            if (guild == null || data == null || !guild.JoinedGuilds.Contains(GuildKey))
            {
                return rows;
            }

            var rank = ClusterFGuildSystem.GuildRankIndex(GuildKey, guild);
            foreach (var book in ImbuingTable.Current.Books)
            {
                if (book.Rank > rank || data.LearnedRecipes.Contains(ClusterFImbueKnowledge.BookKey(book.Id)))
                {
                    continue;
                }

                if (book.MasterFormulary && !ClusterFGuildPromotion.TrialDone(guild, GuildKey))
                {
                    continue;
                }

                rows.Add(new ImbueBookRow(book.Id, book.Name, book.Rank, book.Price));
            }

            return rows;
        }

        private static ClusterFAccountData? ClusterFRecipeStoreData(PlayerMobile pm) =>
            ClusterFRecipeStore.DataOf(pm);

        /// <summary>Pays in Essence Shards (never gold) and hands over the unread book. Re-checked against the rows.</summary>
        public static bool Buy(PlayerMobile pm, string id, out string message)
        {
            var row = Rows(pm).Find(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var guild = ClusterFAccountPersistence.GetGuild(pm);
            if (row == null || guild == null)
            {
                message = "That is not for sale to you now.";
                return false;
            }

            var currency = MuleUpgrades.CurrencyName(GuildKey);
            if (!GuildResources.TryPay(pm, guild, GuildKey, row.Price, currency, 0))
            {
                message = $"You need {row.Price} {currency}.";
                return false;
            }

            ClusterFGuildPromotion.Deliver(pm, new ImbueFormulaBook(row.Id));
            message = "Bought.";
            return true;
        }

        /// <summary>Reads a book into the reader's account and consumes it, or refuses and keeps it.</summary>
        public static void Read(PlayerMobile pm, ImbueFormulaBook item)
        {
            var book = ImbuingTable.Current.Book(item.BookId);
            var data = ClusterFRecipeStoreData(pm);
            if (book == null || data == null)
            {
                pm.SendMessage(0x22, "The formulae in this book mean nothing to you.");
                return;
            }

            if (book.MasterFormulary &&
                (ClusterFAccountPersistence.GetGuild(pm) is not { } guild || !ClusterFGuildPromotion.TrialDone(guild, GuildKey)))
            {
                pm.SendMessage(0x22, "The Master Formulary opens only for one who has passed the Artificers' trial.");
                return;
            }

            if (data.LearnedRecipes.Contains(ClusterFImbueKnowledge.BookKey(book.Id)))
            {
                pm.SendMessage(0x22, $"Your account has already read the {book.Name}. You keep this.");
                return;
            }

            var learned = ClusterFImbueKnowledge.ReadBook(data, book);
            pm.SendMessage(0x44, learned.Count == 0
                ? $"You study the {book.Name}. Your account knew all of it already."
                : $"You study the {book.Name}. Your account now knows: {string.Join("; ", learned)}.");
            pm.PlaySound(0x249);
            item.Delete();
        }
    }
}

namespace Server.Gumps
{
    public sealed class ImbueFormulaShopGump : Gump
    {
        private readonly PlayerMobile _pm;
        private readonly List<ImbueBookRow> _rows;
        private const int W = 520;

        public ImbueFormulaShopGump(PlayerMobile pm) : base(80, 80)
        {
            _pm = pm;
            _rows = ImbueFormulaShop.Rows(pm);

            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            var currency = MuleUpgrades.CurrencyName(ImbueFormulaShop.GuildKey);
            var h = 130 + Math.Max(1, _rows.Count) * 24;

            AddBackground(0, 0, W, h, 9270);
            AddAlphaRegion(10, 10, W - 20, h - 20);
            AddLabel(20, 16, 1154, "Artificers' Order: formula books");
            AddLabel(20, 38, 999,
                $"Rank {ClusterFGuildSystem.GetRankName(ImbueFormulaShop.GuildKey, guild)}. You hold {guild.GetCurrency(ImbueFormulaShop.GuildKey)} {currency}.");
            AddLabel(20, 58, 999, "A formula book teaches your whole account its imbue properties.");
            AddLabel(20, 76, 999, "Unread books can be traded. Past rank 5, learn on the 140+ path.");

            var y = 102;
            if (_rows.Count == 0)
            {
                AddLabel(20, y, 37, "Nothing new for your rank. Rise in the Order for the next formulary.");
            }

            for (var i = 0; i < _rows.Count; i++, y += 24)
            {
                var row = _rows[i];
                AddButton(20, y, 4005, 4007, 100 + i);
                AddLabel(55, y + 2, 1154, $"{row.Name} (rank {ClusterFGuildPromotion.RankNameForIndex(ImbueFormulaShop.GuildKey, row.Rank)})");
                AddLabel(380, y + 2, 999, $"{row.Price} {currency}");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 100;
            if (sender.Mobile != _pm || i < 0 || i >= _rows.Count)
            {
                return;
            }

            if (ClusterFGuildSystem.FindGuildmasterNear(_pm, ClusterFGuildSystem.GetDef(ImbueFormulaShop.GuildKey)) == null)
            {
                _pm.SendMessage(0x22, "You must be at the Artificers' guildmaster to buy.");
                return;
            }

            if (!ImbueFormulaShop.Buy(_pm, _rows[i].Id, out var message))
            {
                _pm.SendMessage(0x22, message);
            }

            _pm.SendGump(new ImbueFormulaShopGump(_pm));
        }
    }

    /// <summary>[ImbueKnowledge: one account's imbue knowledge, with grant (+) and revoke (-) per property.</summary>
    public sealed class ImbueKnowledgeGump : Gump
    {
        private const int PerPage = 20;
        private const int W = 560;

        private readonly IAccount _acct;
        private readonly int _page;
        private readonly List<string> _keys;

        public ImbueKnowledgeGump(IAccount acct, int page) : base(60, 40)
        {
            _acct = acct;
            _keys = ImbueFamilies.AllKeys();
            _page = Math.Clamp(page, 0, Math.Max(0, (_keys.Count - 1) / PerPage));

            var data = ClusterFAccountPersistence.GetOrCreate(acct);
            var h = 150 + PerPage * 22;

            AddBackground(0, 0, W, h, 9270);
            AddAlphaRegion(10, 10, W - 20, h - 20);
            AddLabel(20, 16, 1154, $"Imbue knowledge: account {acct.Username}");
            var slots = ClusterFImbueKnowledge.HasMasterFormulary(data) ? "read" : "not read";
            AddButton(20, 40, 4005, 4007, 10);
            AddLabel(55, 42, 999, $"Master Formulary: {slots} (toggle). Item slots: {ClusterFImbueKnowledge.MaxSlots(data)}.");

            var y = 70;
            for (var i = _page * PerPage; i < _keys.Count && i < (_page + 1) * PerPage; i++, y += 22)
            {
                var key = _keys[i];
                var known = ClusterFImbueKnowledge.KnownTier(data, key);
                var at = ImbuingTable.Current.Ladder.TryGetValue(key, out var a) ? a : "?";
                AddButton(20, y, 4005, 4007, 1000 + i * 2);
                AddButton(50, y, 4017, 4019, 1001 + i * 2);
                AddLabel(85, y + 2, known >= ImbueTier.Basic ? 1154 : 0x3B2, key);
                AddLabel(330, y + 2, 999, $"{known}{(ClusterFImbueKnowledge.IsFree(key) ? " (free)" : "")}");
                AddLabel(470, y + 2, 0x3B2, $"ladder: {at}");
            }

            if (_page > 0)
            {
                AddButton(20, h - 34, 4014, 4015, 2);
                AddLabel(55, h - 32, 999, "Prev");
            }

            if ((_page + 1) * PerPage < _keys.Count)
            {
                AddButton(140, h - 34, 4005, 4006, 3);
                AddLabel(175, h - 32, 999, "Next");
            }

            AddLabel(260, h - 32, 0x3B2, "+ grants the next tier, - revokes the top one.");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var staff = sender.Mobile;
            if (staff == null || staff.AccessLevel < AccessLevel.GameMaster || info.ButtonID == 0)
            {
                return;
            }

            var data = ClusterFAccountPersistence.GetOrCreate(_acct);
            var page = _page;

            switch (info.ButtonID)
            {
                case 2: page--; break;
                case 3: page++; break;
                case 10:
                    if (!data.LearnedRecipes.Remove(ClusterFImbueKnowledge.MasterKey))
                    {
                        data.LearnedRecipes.Add(ClusterFImbueKnowledge.MasterKey);
                    }

                    break;
                default:
                {
                    var i = (info.ButtonID - 1000) / 2;
                    if (info.ButtonID < 1000 || i >= _keys.Count)
                    {
                        return;
                    }

                    var key = _keys[i];
                    var known = ClusterFImbueKnowledge.KnownTier(data, key);
                    if ((info.ButtonID - 1000) % 2 == 0)
                    {
                        if (!ClusterFImbueKnowledge.Grant(data, key, known + 1))
                        {
                            staff.SendMessage($"{key} has no tier above {known}.");
                        }
                    }
                    else if (known >= ImbueTier.Basic)
                    {
                        ClusterFImbueKnowledge.Revoke(data, key, known);
                    }

                    CommandLogging.WriteLine(staff, $"[ImbueKnowledge {_acct.Username}: {key} now {ClusterFImbueKnowledge.KnownTier(data, key)}");
                    break;
                }
            }

            staff.SendGump(new ImbueKnowledgeGump(_acct, page));
        }
    }
}
