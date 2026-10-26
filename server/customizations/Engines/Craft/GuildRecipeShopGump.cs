// cc-P78 Part E (F-43): a craft guild's pattern books and recipe scrolls, sold at its guildmaster for guild currency.
// Notes: shard-migration notes/cc-P78-recipe-gating.md.
//
// What a member sees, per band-gated craft skill the guild owns, by its rank index (ClusterFGuildSystem.GuildRankIndex):
//   - the pattern book for each band up to one band ahead of the rank (Band I at rank 0, Band II at rank 1, ... Band IV
//     at rank 3) that the account does not know yet. Further bands are not listed.
//   - at rank 4 and 5, a rotation of Band V scrolls (RotationSize: 3 and 5), changing once a day, that the account does
//     not know yet. A scroll costs guild currency plus BandVMaterialAmount of the skill's next-tier material.
// Prices are data (RecipeGatingTable). No gold. What is bought is the unread book or scroll, which can be traded.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public sealed record RecipeShopRow(string Key, SkillName Skill, int Band, bool Book, int Price, Type? Material, int MaterialAmount);

    public static class GuildRecipeShop
    {
        public static List<RecipeShopRow> Rows(PlayerMobile pm, string guildKey)
        {
            var rows = new List<RecipeShopRow>();
            var data = ClusterFAccountPersistence.GetGuild(pm);
            if (data == null || !data.JoinedGuilds.Contains(guildKey))
            {
                return rows;
            }

            var table = RecipeGatingTable.Current;
            var rank = ClusterFGuildSystem.GuildRankIndex(guildKey, data);

            foreach (var skill in RecipeOwners.GatedSkillsOf(guildKey))
            {
                var system = ClusterFRecipeGate.SystemFor(skill)!;
                var books = ClusterFRecipeGate.BooksFor(system);

                for (var band = 1; band <= Math.Min(table.LastBookBand, rank + 1); band++)
                {
                    var book = ClusterFRecipeGate.MergedBand(system, band);
                    var key = ClusterFRecipeStore.BookKey(skill, book);
                    if (books.Contains(book) && !rows.Exists(r => r.Key == key) && !ClusterFRecipeStore.Knows(pm, key))
                    {
                        rows.Add(new RecipeShopRow(key, skill, book, true, table.PriceOfBook(book), null, 0));
                    }
                }

                if (rank >= ClusterFGuildSystem.RankGrandmaster)
                {
                    var size = table.RotationSize[Math.Min(rank - ClusterFGuildSystem.RankGrandmaster, table.RotationSize.Length - 1)];
                    var material = MaterialFor(skill);

                    foreach (var key in Rotation(skill, size))
                    {
                        if (!ClusterFRecipeStore.Knows(pm, key))
                        {
                            rows.Add(new RecipeShopRow(key, skill, 5, false, table.BandVScrollPrice, material,
                                material != null ? table.BandVMaterialAmount : 0));
                        }
                    }
                }
            }

            return rows;
        }

        public static Type? MaterialFor(SkillName skill) =>
            RecipeGatingTable.Current.BandVMaterial.TryGetValue(skill.ToString(), out var name) && !string.IsNullOrWhiteSpace(name)
                ? AssemblyHandler.FindTypeByName(name)
                : null;

        /// <summary>Today's Band V rotation for a skill: the same picks all day, different the next (seeded by day and skill).</summary>
        public static List<string> Rotation(SkillName skill, int size, DateTime? day = null)
        {
            var all = ClusterFRecipeRewards.BandVKeys(skill);
            var seed = (int)((day ?? Core.Now).Date.Ticks / TimeSpan.TicksPerDay) * 59 + (int)skill;
            var rng = new System.Random(seed);

            for (var i = all.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }

            return all.GetRange(0, Math.Min(size, all.Count));
        }

        /// <summary>Pays and hands over the unread book or scroll. Re-checked against today's rows, so a stale gump buys nothing.</summary>
        public static bool Buy(PlayerMobile pm, string guildKey, string key, out string message)
        {
            var row = Rows(pm, guildKey).Find(r => r.Key == key);
            var data = ClusterFAccountPersistence.GetGuild(pm);

            if (row == null || data == null)
            {
                message = "That is not for sale to you now.";
                return false;
            }

            var currencyName = MuleUpgrades.CurrencyName(guildKey);
            GuildCost[] materials = row.Material != null ? [GuildCost.Of(row.Material, row.MaterialAmount)] : [];

            if (!GuildResources.TryPay(pm, data, guildKey, row.Price, currencyName, 0, materials))
            {
                message = $"You need {row.Price} {currencyName}" +
                          (row.Material != null ? $" and {row.MaterialAmount} {row.Material.Name}" : "") + ".";
                return false;
            }

            ClusterFGuildPromotion.Deliver(pm, row.Book ? new PatternBook(row.Skill, row.Band) : new AccountRecipeScroll(row.Key));
            message = "Bought.";
            return true;
        }
    }

    public class GuildRecipeShopGump : GuildStyledGump
    {
        private readonly PlayerMobile _pm;
        private readonly string _guildKey;
        private readonly Mobile? _guildmaster;
        private readonly List<RecipeShopRow> _rows;

        private const int W = 560;

        public GuildRecipeShopGump(PlayerMobile pm, string guildKey, Mobile? guildmaster) : base(80, 80)
        {
            _pm = pm;
            _guildKey = guildKey;
            _guildmaster = guildmaster;
            _rows = GuildRecipeShop.Rows(pm, guildKey);

            var data = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            var name = ClusterFGuildSystem.GetDef(guildKey)?.Name ?? guildKey;
            var currency = MuleUpgrades.CurrencyName(guildKey);
            var h = 120 + Math.Max(1, _rows.Count) * 24;

            AddBackground(0, 0, W, h, 9270);
            AddAlphaRegion(10, 10, W - 20, h - 20);
            AddLabel(20, 16, 1154, $"{name}: pattern books and recipe scrolls");
            AddLabel(20, 38, 999, $"Rank {ClusterFGuildSystem.GetRankName(guildKey, data)}. You hold {data.GetCurrency(guildKey)} {currency}.");
            AddLabel(20, 58, 999, "A book or scroll teaches your whole account. Unread ones can be traded.");

            var y = 84;
            if (_rows.Count == 0)
            {
                AddLabel(20, y, 37, "Nothing new for your rank. Rise in the guild for the next band.");
            }

            for (var i = 0; i < _rows.Count; i++, y += 24)
            {
                var row = _rows[i];
                var what = row.Book
                    ? $"Band {RecipeGatingTable.Current.BandName(row.Band)} pattern book: {ClusterFRecipeStore.SkillTitle(row.Skill)}"
                    : $"Scroll: {ClusterFRecipeStore.Describe(row.Key)}";
                var price = $"{row.Price} {currency}" + (row.Material != null ? $" + {row.MaterialAmount} {row.Material.Name}" : "");

                AddButton(20, y, 4005, 4007, 100 + i);
                AddLabel(55, y + 2, 1154, what);
                AddLabel(380, y + 2, 999, price);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 100;
            if (sender.Mobile != _pm || i < 0 || i >= _rows.Count)
            {
                return;
            }

            if (!ClusterFGuildSystem.IsNearGuildmaster(_pm, _guildmaster))
            {
                _pm.SendMessage(0x22, "You must be at the guildmaster to buy.");
                return;
            }

            if (!GuildRecipeShop.Buy(_pm, _guildKey, _rows[i].Key, out var message))
            {
                _pm.SendMessage(0x22, message);
            }

            _pm.SendGump(new GuildRecipeShopGump(_pm, _guildKey, _guildmaster));
        }
    }
}
