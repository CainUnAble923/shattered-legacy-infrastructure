// GolemTestAccounts: every Golem's own Admin and Player account on a TEST shard (cc-P215). Notes in shard-migration
// notes/cc-P215-golem-test-accounts.md.
//
// WHAT. golem-accounts.json in the world folder (beside TestAccountWipe's request) lists, for every Golem that claims
// prompts, '<golem>-admin' (Administrator) and '<golem>-player' (Player), each with a PBKDF2 hash of its password in
// pinned's own format (never the password) and a starter character named after the Golem ("Copper", "Copper the
// Tester"). The game PC writes it from the Golem registry (D:\UO\golem\GolemTestAccounts.ps1) and the kit carries it to
// every PC; docker/uo/Start-TestShard.ps1 copies it into the test world folder on every start, -Fresh included.
//
// WHEN. At world load (Initialize, before AccountPrompt, so a fresh world with no accounts never asks for an owner and
// hangs the boot: AccountPrompt.Initialize is priority 0, this runs just after TestAccountWipe.EarlyAdmin), and by
// [SeedGolemAccounts [dryrun] on a running test world. Never a wipe: other accounts and characters are not touched.
//
// TEST ONLY, IN CODE. The same guard as the account wipe (TestAccountWipe.Refusal): SL_SHARD_ROLE=test, the
// clusterf.testShard.allowAccountWipe setting True (only Configuration-test has it), and no listener on 2593. Anything
// else seeds nothing and says why.
//
// IDEMPOTENT. A missing account is made; an account that is there keeps its characters, and is changed only where the
// manifest differs: its access level, or its password when the manifest's hash is a new one (remembered in the
// account tag GolemSeedTag, because the first login rehashes the password to Argon2). A missing starter character is
// made, logged out at the Golems' meeting spot in Green Acres (Trammel), out of the way of Chase's play-tests.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server.Misc;

public sealed record GolemTestAccount(string Golem, string Role, string Username, string Character, string PasswordPbkdf2, Point3D Spot)
{
    public AccessLevel Access => Role == "admin" ? AccessLevel.Administrator : AccessLevel.Player;
}

public sealed record GolemSeedCounts(int AccountsMade, int AccountsChanged, int CharactersMade, int Unchanged, int Skipped);

public static class GolemTestAccounts
{
    public const string ManifestFileName = "golem-accounts.json";
    public const string GolemSeedTag = "golemTestAccountHash";
    public const string Tag = "[GolemAccounts]";

    // Green Acres, Trammel: open grass with no spawner and no town (checked against the map files, cc-P215 notes).
    public static readonly Point3D MeetingSpot = new(5440, 1150, 0);

    public static string ManifestPath =>
        Path.Combine(Path.GetDirectoryName(World.SavePath.TrimEnd('/', '\\')) ?? ".", ManifestFileName);

    public static string CurrentRefusal() =>
        TestAccountWipe.Refusal(
            Environment.GetEnvironmentVariable(TestAccountWipe.RoleVariable),
            ServerConfiguration.GetSetting(TestAccountWipe.AllowSetting, (string)null), // read only, as the wipe
            ServerConfiguration.Listeners
        );

    public static void Configure()
    {
        CommandSystem.Register("SeedGolemAccounts", AccessLevel.Administrator, OnCommand);
    }

    // Just after TestAccountWipe.EarlyAdmin (int.MinValue) and well before AccountPrompt (0).
    [CallPriority(int.MinValue + 1)]
    public static void Initialize()
    {
        if (!File.Exists(ManifestPath))
        {
            return;
        }

        foreach (var line in SeedFromFile(CurrentRefusal(), false))
        {
            Console.WriteLine(line);
        }
    }

    // ------------------------------------------------------------------ the manifest

    /// <summary>The manifest's accounts, or null and why. Pure, for the tests.</summary>
    public static List<GolemTestAccount> ParseManifest(string json, out string error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != 1)
            {
                error = "the manifest is not version 1";
                return null;
            }

            if (!root.TryGetProperty("accounts", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                error = "the manifest has no accounts list";
                return null;
            }

            var result = new List<GolemTestAccount>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in list.EnumerateArray())
            {
                var golem = Str(e, "golem");
                var role = Str(e, "role");
                var user = Str(e, "username");
                var character = Str(e, "character");
                var hash = Str(e, "passwordPbkdf2");
                var x = Int(e, "x", MeetingSpot.X);
                var y = Int(e, "y", MeetingSpot.Y);
                var z = Int(e, "z", MeetingSpot.Z);

                if (string.IsNullOrEmpty(golem) || golem.Length > 20 || !golem.All(char.IsAsciiLetterOrDigit))
                {
                    error = $"a golem name is not letters and digits: '{golem}'";
                    return null;
                }

                if (role is not ("admin" or "player"))
                {
                    error = $"{golem}: the role is '{role}', not admin or player";
                    return null;
                }

                // The name is the Golem's, lower case, and the role: never anyone else's account.
                var want = golem.ToLowerInvariant() + "-" + role;
                if (user != want)
                {
                    error = $"{golem} {role}: the username is '{user}', not '{want}'";
                    return null;
                }

                if (!names.Add(user))
                {
                    error = $"'{user}' is listed twice";
                    return null;
                }

                if (string.IsNullOrEmpty(character) || character.Length > 30 || !character.All(c => char.IsAsciiLetter(c) || c == ' '))
                {
                    error = $"{user}: the character name '{character}' is not letters and spaces";
                    return null;
                }

                if (!TestAccountWipe.IsPbkdf2Hash(hash))
                {
                    error = $"{user}: the password hash is not pinned's PBKDF2 format";
                    return null;
                }

                if (x is < 5376 or > 6143 || y is < 512 or > 1279)
                {
                    error = $"{user}: the spot {x},{y} is not in Green Acres";
                    return null;
                }

                result.Add(new GolemTestAccount(golem, role, user, character, hash, new Point3D(x, y, z)));
            }

            return result;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            error = "the manifest is not readable JSON: " + e.Message;
            return null;
        }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int Int(JsonElement e, string name, int fallback) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : fallback;

    // ------------------------------------------------------------------ the seed

    public static List<string> SeedFromFile(string refusal, bool dryRun)
    {
        if (!File.Exists(ManifestPath))
        {
            return new List<string> { $"{Tag} no {ManifestFileName} in the world folder: nothing to seed. The game PC writes it (GolemTestAccounts.ps1 -Write) and Start-TestShard.ps1 copies it in." };
        }

        var accounts = ParseManifest(File.ReadAllText(ManifestPath), out var error);
        if (accounts == null)
        {
            return new List<string> { $"{Tag} REFUSED: {error}. Nothing changed." };
        }

        return Seed(accounts, refusal, dryRun, out _);
    }

    /// <summary>
    /// Makes what is missing and corrects what differs; touches no other account. refusal non-null: nothing, said so.
    /// </summary>
    public static List<string> Seed(IReadOnlyList<GolemTestAccount> wanted, string refusal, bool dryRun, out GolemSeedCounts counts)
    {
        var lines = new List<string>();
        counts = new GolemSeedCounts(0, 0, 0, 0, 0);
        if (refusal != null)
        {
            lines.Add($"{Tag} REFUSED: {refusal}. Golem accounts are seeded on a test shard only; nothing changed.");
            return lines;
        }

        int made = 0, changed = 0, chars = 0, same = 0, skipped = 0;
        foreach (var w in wanted)
        {
            var verb = dryRun ? "would " : "";
            var acct = Accounts.GetAccount(w.Username) as Account;
            var touched = false;
            var isNew = acct == null;
            if (isNew)
            {
                lines.Add($"{Tag} {verb}make {w.Username} ({w.Access})");
                made++;
                if (dryRun)
                {
                    chars++;
                    continue;
                }

                // The constructor hashes a throwaway password; the manifest's hash replaces it, as the wipe's admin.
                acct = new Account(w.Username, Guid.NewGuid().ToString("N")) { AccessLevel = w.Access };
                acct.ApplyPasswordWrite(w.PasswordPbkdf2, PasswordProtectionAlgorithm.PBKDF2);
                acct.SetTag(GolemSeedTag, w.PasswordPbkdf2);
            }
            else
            {
                if (acct.AccessLevel != w.Access)
                {
                    lines.Add($"{Tag} {verb}set {w.Username} to {w.Access} (was {acct.AccessLevel})");
                    touched = true;
                    if (!dryRun)
                    {
                        acct.AccessLevel = w.Access;
                        foreach (var m in Characters(acct))
                        {
                            m.AccessLevel = w.Access;
                        }
                    }
                }

                if (acct.GetTag(GolemSeedTag) != w.PasswordPbkdf2)
                {
                    lines.Add($"{Tag} {verb}set {w.Username}'s password from the manifest");
                    touched = true;
                    if (!dryRun)
                    {
                        acct.ApplyPasswordWrite(w.PasswordPbkdf2, PasswordProtectionAlgorithm.PBKDF2);
                        acct.SetTag(GolemSeedTag, w.PasswordPbkdf2);
                    }
                }
            }

            if (Characters(acct).All(m => m.Name != w.Character))
            {
                if (acct.Count >= acct.Limit)
                {
                    lines.Add($"{Tag} {w.Username} has no free character slot for {w.Character}; left as it is");
                    skipped++;
                }
                else
                {
                    lines.Add($"{Tag} {verb}make {w.Username}'s character {w.Character}, logged out at {w.Spot} (Trammel)");
                    chars++;
                    touched = true;
                    if (!dryRun)
                    {
                        MakeCharacter(acct, w);
                    }
                }
            }

            // A new account counts as made, not changed.
            if (!isNew)
            {
                if (touched)
                {
                    changed++;
                }
                else
                {
                    same++;
                }
            }
        }

        counts = new GolemSeedCounts(made, changed, chars, same, skipped);
        lines.Add(
            $"{Tag} {(dryRun ? "dry run: " : "")}{wanted.Count} Golem accounts in the manifest: {made} {(dryRun ? "to make" : "made")}, " +
            $"{changed} {(dryRun ? "to change" : "changed")}, {chars} character(s) {(dryRun ? "to make" : "made")}, {same} already right, {skipped} skipped. No other account is touched."
        );
        return lines;
    }

    public static IEnumerable<Mobile> Characters(Account acct)
    {
        for (var i = 0; i < acct.Length; i++)
        {
            if (acct[i] is { } m)
            {
                yield return m;
            }
        }
    }

    public static PlayerMobile MakeCharacter(Account acct, GolemTestAccount w)
    {
        for (var i = 0; i < acct.Length; i++)
        {
            if (acct[i] != null)
            {
                continue;
            }

            var m = new PlayerMobile
            {
                Player = true,
                Name = w.Character,
                Race = Race.Human,
                Body = 0x190,
                Female = false,
                Hue = Race.Human.ClipSkinHue(0x83EA & 0x3FFF) | 0x8000,
                AccessLevel = w.Access,
                Hunger = 20
            };
            acct[i] = m;
            m.InitStats(50, 50, 50);
            var pack = new Backpack { Movable = false };
            m.AddItem(pack);
            pack.DropItem(new Gold(100)); // something to offer in a trade check (uobot golemcheck pair)
            m.AddItem(new Robe(w.Role == "admin" ? 0x0481 : 0x0000));
            if (w.Access > AccessLevel.Player)
            {
                m.Blessed = true;
            }

            // Logged out at the meeting spot: a character on the Internal map goes to LogoutMap/LogoutLocation when
            // its player connects (pinned Server/Mobiles/Mobile.cs:1320-1324).
            m.LogoutLocation = w.Spot;
            m.LogoutMap = Map.Trammel;
            m.MoveToWorld(w.Spot, Map.Internal);
            return m;
        }

        return null;
    }

    // ------------------------------------------------------------------ the command

    [Usage("SeedGolemAccounts [dryrun]")]
    [Description("Test shard only: makes every Golem's own admin and player account and starter character from golem-accounts.json in the world folder, and corrects an access level or a password that differs. Touches no other account. dryrun lists what it would do.")]
    [ShardCommand(CommandCategory.DevTool, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Makes every Golem's own admin and player test account (and starter character) from the manifest the game PC writes. Refuses anywhere but a test shard. Touches no other account. Dry run: 'dryrun'.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var dryRun = e.Arguments.Any(a => a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));
        foreach (var line in SeedFromFile(CurrentRefusal(), dryRun))
        {
            e.Mobile.SendMessage(line);
        }
    }
}
