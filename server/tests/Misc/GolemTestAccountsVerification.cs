// GolemTestAccountsVerification.cs
//
// cc-P215: every Golem's own Admin and Player account on a test shard. Notes in shard-migration
// notes/cc-P215-golem-test-accounts.md.
//
// What the facts pin:
//   1. The seed refuses without the test-shard flag (TestAccountWipe's guard): nothing is made.
//   2. Every Golem in the manifest gets exactly its two accounts, at their access levels, each with its starter
//      character logged out at the meeting spot; nothing else is made.
//   3. A re-seed changes nothing; a new hash in the manifest changes that one password, and an access level that drifted
//      is put back.
//   4. The manifest refuses a name that is not '<golem>-admin' or '<golem>-player', a role other than those two, a
//      password that is not a PBKDF2 hash, and a spot outside Green Acres.
//   5. Another account, even one named like a Golem's, keeps its level and characters.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Misc;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GolemTestAccountsVerification
{
    // A hash in pinned's PBKDF2 format: iterations 1024 (0x0400, little-endian "0004"), 8 bytes of salt, 32 of hash.
    private static string Hash(char fill) => "0004" + new string(fill, 80);

    private static readonly string[] Golems = { "Pfaketest", "Qfaketest", "Rfaketest" };

    private static string Manifest(string[] golems, char fill = 'A')
    {
        var rows = new List<string>();
        foreach (var g in golems)
        {
            rows.Add($$"""{ "golem": "{{g}}", "role": "admin", "username": "{{g.ToLowerInvariant()}}-admin", "character": "{{g}}", "passwordPbkdf2": "{{Hash(fill)}}" }""");
            rows.Add($$"""{ "golem": "{{g}}", "role": "player", "username": "{{g.ToLowerInvariant()}}-player", "character": "{{g}} the Tester", "passwordPbkdf2": "{{Hash(fill)}}" }""");
        }

        return "{ \"version\": 1, \"accounts\": [" + string.Join(",", rows) + "] }";
    }

    private static List<GolemTestAccount> Parse(string json)
    {
        var list = GolemTestAccounts.ParseManifest(json, out var error);
        Assert.True(list != null, error);
        return list;
    }

    private static void Clean()
    {
        foreach (var a in Accounts.GetAccounts().OfType<Account>().Where(a => a.Username.Contains("faketest")).ToList())
        {
            a.Delete();
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheSeedRefusesWithoutTheTestShardFlag()
    {
        try
        {
            Clean();
            var refusal = TestAccountWipe.Refusal("test", null, Array.Empty<System.Net.IPEndPoint>());
            Assert.NotNull(refusal);
            Assert.NotNull(TestAccountWipe.Refusal("test", "False", Array.Empty<System.Net.IPEndPoint>()));
            Assert.NotNull(TestAccountWipe.Refusal(null, "True", Array.Empty<System.Net.IPEndPoint>()));

            var before = Accounts.Count;
            var lines = GolemTestAccounts.Seed(Parse(Manifest(Golems)), refusal, false, out var counts);
            Assert.Contains("REFUSED", lines[0]);
            Assert.Equal(before, Accounts.Count);
            Assert.Equal(0, counts.AccountsMade);
            Assert.Null(Accounts.GetAccount("pfaketest-admin"));
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EveryGolemInTheManifestGetsExactlyItsTwoAccounts()
    {
        try
        {
            Clean();
            var before = Accounts.Count;
            GolemTestAccounts.Seed(Parse(Manifest(Golems)), null, false, out var counts);
            Assert.Equal(6, counts.AccountsMade);
            Assert.Equal(6, counts.CharactersMade);
            Assert.Equal(before + 6, Accounts.Count);

            foreach (var g in Golems)
            {
                foreach (var (role, level, character) in new[] { ("admin", AccessLevel.Administrator, g), ("player", AccessLevel.Player, g + " the Tester") })
                {
                    var a = Accounts.GetAccount(g.ToLowerInvariant() + "-" + role) as Account;
                    Assert.NotNull(a);
                    Assert.Equal(level, a.AccessLevel);
                    var chars = GolemTestAccounts.Characters(a).ToList();
                    Assert.Single(chars);
                    var m = chars[0];
                    Assert.Equal(character, m.Name);
                    Assert.Equal(level, m.AccessLevel);
                    Assert.Equal(Map.Internal, m.Map);
                    Assert.Equal(Map.Trammel, m.LogoutMap);
                    Assert.Equal(GolemTestAccounts.MeetingSpot, m.LogoutLocation);
                    Assert.NotNull(m.Backpack);
                }
            }

            Assert.Equal(6, Accounts.GetAccounts().Count(a => a.Username.Contains("faketest")));
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AReseedChangesNothingAndOnlyADifferenceIsCorrected()
    {
        try
        {
            Clean();
            GolemTestAccounts.Seed(Parse(Manifest(Golems)), null, false, out _);
            var accounts = Accounts.Count;
            var mobiles = World.Mobiles.Count;

            var dry = GolemTestAccounts.Seed(Parse(Manifest(Golems)), null, true, out var dryCounts);
            Assert.Equal(new GolemSeedCounts(0, 0, 0, 6, 0), dryCounts);
            var again = GolemTestAccounts.Seed(Parse(Manifest(Golems)), null, false, out var counts);
            Assert.Equal(new GolemSeedCounts(0, 0, 0, 6, 0), counts);
            Assert.Equal(accounts, Accounts.Count);
            Assert.Equal(mobiles, World.Mobiles.Count);
            Assert.Contains("0 made, 0 changed, 0 character(s) made, 6 already right", again.Last());

            // A drifted level is put back; a new password hash is installed for every account it names.
            var admin = (Account)Accounts.GetAccount("qfaketest-admin");
            admin.AccessLevel = AccessLevel.Player;
            GolemTestAccounts.Seed(Parse(Manifest(Golems)), null, false, out counts);
            Assert.Equal(1, counts.AccountsChanged);
            Assert.Equal(AccessLevel.Administrator, admin.AccessLevel);

            GolemTestAccounts.Seed(Parse(Manifest(Golems, 'B')), null, false, out counts);
            Assert.Equal(6, counts.AccountsChanged);
            Assert.Equal(Hash('B'), admin.GetTag(GolemTestAccounts.GolemSeedTag));
            Assert.Equal(mobiles, World.Mobiles.Count);
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheManifestRefusesAnythingButAGolemsOwnTwoAccounts()
    {
        Assert.Equal(6, Parse(Manifest(Golems)).Count);
        var good = Manifest(new[] { "Pfaketest" });

        foreach (var (bad, why) in new[]
        {
            (good.Replace("\"pfaketest-admin\"", "\"chase\""), "username"),
            (good.Replace("\"pfaketest-player\"", "\"qfaketest-player\""), "username"),
            (good.Replace("\"role\": \"admin\"", "\"role\": \"owner\""), "role"),
            (good.Replace(Hash('A'), "secret-password"), "hash"),
            (good.Replace("\"version\": 1", "\"version\": 2"), "version"),
            (good.Replace("\"character\": \"Pfaketest\"", "\"character\": \"Pfake<test>\""), "character"),
            (good.Replace("\"character\": \"Pfaketest\",", "\"character\": \"Pfaketest\", \"x\": 1400, \"y\": 1600,"), "spot")
        })
        {
            Assert.True(GolemTestAccounts.ParseManifest(bad, out var error) == null, why);
            Assert.False(string.IsNullOrEmpty(error), why);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AnotherAccountKeepsItsLevelAndCharacters()
    {
        Account other = null;
        try
        {
            Clean();
            other = new Account("sfaketest-admin", "p215-test-only") { AccessLevel = AccessLevel.GameMaster };
            var pm = new PlayerMobile { Player = true, Name = "Somebody" };
            other[0] = pm;

            GolemTestAccounts.Seed(Parse(Manifest(Golems)), null, false, out _);
            Assert.Equal(AccessLevel.GameMaster, other.AccessLevel);
            Assert.Single(GolemTestAccounts.Characters(other));
            Assert.False(pm.Deleted);
            Assert.Null(other.GetTag(GolemTestAccounts.GolemSeedTag));
        }
        finally
        {
            Clean();
        }
    }
}
