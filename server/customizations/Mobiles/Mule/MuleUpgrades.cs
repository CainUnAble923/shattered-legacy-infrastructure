// cc-P69, F-33 Part B: the mule's tier-up path, one record per step (P47 5.1's UpgradeStep shape, cut to what the mule
// needs). Tier 1 -> 2 is the old Civic Mule Exchange price (25 each of four currencies and 25,000 gold, Chase
// 2026-10-05); the steps above it are placeholders for Chase, rising, with a Miners' Compact rank and more guilds.
//
// No multi-currency payment existed (GuildResources.TryPay takes one scrip; the Exchange hand-rolled four), so this is
// the smallest one that serves: check everything, take nothing unless all of it is there, then take it all.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Items;
using Server.Mobiles;

namespace Server
{
    public sealed record MuleUpgradeStep(
        int FromTier,
        int CompactRank,                       // Miners' Compact rank index (ClusterFGuildSystem.GuildRankIndex), 0 = none
        (string Guild, int Amount)[] Scrip,    // guild currency keys (CharacterGuildData.GuildCurrency)
        int Gold
    );

    public static class MuleUpgrades
    {
        public static readonly MuleUpgradeStep[] Steps =
        {
            new(1, 0, new[] { ("mining", 25), ("smithing", 25), ("rangers", 25), ("foresters", 25) }, 25_000),
            new(2, ClusterFGuildSystem.RankJourneyman, new[] { ("mining", 60), ("smithing", 40), ("foresters", 40), ("tinkers", 40) }, 50_000),
            new(3, ClusterFGuildSystem.RankMaster, new[] { ("mining", 120), ("smithing", 80), ("rangers", 80), ("arcane", 60) }, 100_000),
            new(4, ClusterFGuildSystem.RankGrandmaster, new[] { ("mining", 250), ("smithing", 150), ("artificers", 100), ("warriors", 100), ("arcane", 100) }, 250_000)
        };

        public static MuleUpgradeStep StepFrom(int tier) => Steps.FirstOrDefault(s => s.FromTier == tier);

        // The four named currencies; the rest show as "<guild> scrip".
        private static readonly Dictionary<string, string> CurrencyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["mining"] = "Mining Vouchers",
            ["smithing"] = "Smithing Seals",
            ["rangers"] = "Trail Marks",
            ["foresters"] = "Timber Tokens",
            ["artificers"] = "Essence Shards"
        };

        public static string CurrencyName(string key)
        {
            if (CurrencyNames.TryGetValue(key, out var name))
            {
                return name;
            }

            return $"{ClusterFGuildSystem.GetDef(key)?.Name ?? key} scrip";
        }

        /// <summary>What is short for this step, one line each; empty when the player can pay.</summary>
        public static List<string> Shortfalls(PlayerMobile pm, CharacterGuildData guild, MuleUpgradeStep step)
        {
            var short_ = new List<string>();

            if (step.CompactRank > 0 && ClusterFGuildSystem.GuildRankIndex("mining", guild) < step.CompactRank)
            {
                short_.Add($"Miners' Compact rank {step.CompactRank} or higher (you are rank {ClusterFGuildSystem.GuildRankIndex("mining", guild)})");
            }

            foreach (var (key, amount) in step.Scrip)
            {
                guild.GuildCurrency.TryGetValue(key, out var held);

                if (held < amount)
                {
                    short_.Add($"{amount:N0} {CurrencyName(key)} (you have {held:N0})");
                }
            }

            var gold = CompactGoldHelper.GetTotalGold(pm);

            if (gold < step.Gold)
            {
                short_.Add($"{step.Gold:N0} gold in your backpack or bank (you have {gold:N0})");
            }

            return short_;
        }

        /// <summary>Pays for the step if everything is there (the testing token waives it), and tiers the mule up.</summary>
        public static bool TryUpgrade(PlayerMobile pm, CharacterGuildData guild, PackMule mule)
        {
            if (mule == null || mule.Deleted || mule.ControlMaster != pm)
            {
                pm.SendMessage(0x22, "That is not your mule.");
                return false;
            }

            var step = StepFrom(mule.Tier);

            if (step == null)
            {
                pm.SendMessage(0x22, "Your mule is already a Celestial mule; there is no higher tier.");
                return false;
            }

            if (!DevTestingCrystal.IsActive(pm))
            {
                var shortfalls = Shortfalls(pm, guild, step);

                if (shortfalls.Count > 0)
                {
                    pm.SendMessage(0x22, $"You need {shortfalls[0]}.");
                    return false;
                }

                foreach (var (key, amount) in step.Scrip)
                {
                    guild.GuildCurrency[key] = guild.GuildCurrency[key] - amount;
                }

                CompactGoldHelper.ConsumeGold(pm, step.Gold);
            }

            mule.TierUp();
            mule.FixedParticles(0x375A, 9, 20, 5027, EffectLayer.Waist);
            mule.PlaySound(0xA8);
            pm.SendMessage(0x44, $"Your mule is now {mule.Name}.");
            return true;
        }

        /// <summary>The player's mules here: the one they ride and any following within 12 tiles.</summary>
        public static List<PackMule> MulesOf(PlayerMobile pm)
        {
            var list = new List<PackMule>();

            if (pm.Mount is PackMule ridden)
            {
                list.Add(ridden);
            }

            // Null until the character has had a follower (pinned PlayerMobile.AllFollowers is created on first use).
            foreach (var m in pm.AllFollowers ?? new HashSet<Mobile>())
            {
                if (m is PackMule mule && !list.Contains(mule) && mule.Map == pm.Map && mule.InRange(pm, 12))
                {
                    list.Add(mule);
                }
            }

            return list;
        }
    }
}
