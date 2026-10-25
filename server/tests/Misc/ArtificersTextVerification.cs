// ArtificersTextVerification.cs
//
// cc-P460: the Artificers' guildmaster pages and the join message say the current rules (the P383 guide's
// contradictions, checked by cc-P429). Rewards come with the Apprentice task, not with joining (F-9 Decision 3, cc-P15:
// ClusterFGuildSystem.CompleteApprenticeTask). What a member can imbue, and how strongly, comes from learned property
// tiers and the place (cc-P82: ArtificersImbueGump's KnownTier gate, ImbueSites), not from standing-based caps or slots.
// Notes: shard-migration notes/cc-P460-artificers-text.md.

using System;
using System.Linq;
using System.Text;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ArtificersTextVerification
{
    private static string PageText(PlayerMobile pm, ArtificersGuildmasterGump.View view)
    {
        ClusterFGuildSystem.EnsureRegistered(); // the host runs no Configure
        return string.Join(" ", new ArtificersGuildmasterGump(pm, view).Entries.OfType<GumpHtml>().Select(h => h.Text));
    }

    [Fact]
    public void TheRewardsPageCreditsTheApprenticeTaskNotJoining()
    {
        var pm = new PlayerMobile { Player = true };
        try
        {
            var text = PageText(pm, ArtificersGuildmasterGump.View.Rewards);
            Assert.DoesNotContain("Upon joining", text);
            Assert.Contains("Apprentice task", text);
            Assert.Contains("Joining is free", text);
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void TheEnhancePageSaysLearnedTiersAndPlaceNotRankCaps()
    {
        var pm = new PlayerMobile { Player = true };
        try
        {
            var text = PageText(pm, ArtificersGuildmasterGump.View.WhatWeEnhance);
            Assert.DoesNotContain("greater caps", text);
            Assert.DoesNotContain("Higher ranks unlock", text);
            Assert.Contains("tier", text);
            Assert.Contains("formula books", text);
            Assert.Contains("soul", text);
        }
        finally
        {
            pm.Delete();
        }
    }

    // A system message's text, in either encoding pinned may send it in (0x1C ASCII or 0xAE big-endian Unicode).
    private static bool Sent(NetState ns, string text)
    {
        var bytes = ns.SendBuffer.GetReadSpan().ToArray();
        return Contains(bytes, Encoding.ASCII.GetBytes(text)) || Contains(bytes, Encoding.BigEndianUnicode.GetBytes(text));
    }

    private static bool Contains(byte[] hay, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= hay.Length; i++)
        {
            if (hay.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void TheJoinMessagePointsAtTheApprenticeTaskAndLearnedTiers()
    {
        var pm = new PlayerMobile { Player = true, Young = false };
        pm.AddItem(new Backpack());
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        if (Server.Accounting.Security.AccountSecurity.CurrentAlgorithm ==
            Server.Accounting.Security.PasswordProtectionAlgorithm.None)
        {
            Server.Accounting.Security.AccountSecurity.CurrentAlgorithm =
                Server.Accounting.Security.PasswordProtectionAlgorithm.PBKDF2;
        }

        var account = new Server.Accounting.Account($"p460-{pm.Serial.Value:X8}", "p460-test-only");
        ns.Account = account;
        pm.Account = account; // Join returns at once for a mobile with no account
        try
        {
            ClusterFGuildSystem.EnsureRegistered();
            var def = ClusterFGuildSystem.GetDef("artificers");
            Assert.NotNull(def);
            ClusterFGuildSystem.Join(pm, def!);

            Assert.True(Sent(ns, "Welcome to the Artificers' Order."), "the join message was not seen on the wire");
            Assert.False(Sent(ns, "higher intensity caps"), "the join message still promises standing-based caps");
            Assert.False(Sent(ns, "more property slots"), "the join message still promises slots for standing");
            Assert.True(Sent(ns, "Apprentice task"));
            Assert.True(Sent(ns, "formula books"));
        }
        finally
        {
            foreach (var i in pm.Backpack?.Items.ToList() ?? new())
            {
                i.Delete();
            }

            pm.Delete();
        }
    }
}
