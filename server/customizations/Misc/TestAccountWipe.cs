// TestAccountWipe: on the TEST shard only, delete every account and character, with everything they own, and keep
// the world (cc-P73). Asked for by Shard-Console.ps1's Test shard > Wipe test accounts, which stops the test shard,
// backs up its save, places RequestFileName in the world folder and starts it. Notes and the inventory of what goes and
// what stays, with pinned file:line: shard-migration notes/cc-P73-test-account-wipe.md.
//
// WHEN. Once, at the end of AssemblyHandler.Invoke("Initialize") (Server/Main.cs:674), after World.Load (:672) and
// before NetState.Start (:678): no player can be connected. CallPriority int.MaxValue puts it after every other
// Initialize (sorted ascending, Server/AssemblyHandler.cs:143, Attributes.cs:59-70), so every system has subscribed
// its delete handlers first (PlayerMobile.PlayerDeletedEvent, PlayerMobile.cs:3285-3292), and AccountPrompt.Initialize
// (UOContent/Misc/AccountPrompt.cs:10-17) has already run and seen the old accounts, so it cannot ask for an owner and
// hang the boot (the D40 shape). A world with no accounts at all would prompt before this runs, so EarlyAdmin, at
// int.MinValue, makes the admin first in that one case.
//
// TEST ONLY, IN CODE. Refusal() wants all three: SL_SHARD_ROLE=test in the container's environment (set only by
// docker/uo/docker-compose.test.yml), clusterf.testShard.allowAccountWipe True in modernuo.json (written only into
// Configuration-test, by docker/uo/Start-TestShard.ps1), and no listener on 2593, the live port. Anything else logs
// RefusedLine, removes the request (it carries a password hash) and changes nothing.
//
// THE PASSWORD never reaches this file or the disk. The request carries a PBKDF2 hash in pinned's own format
// (UOContent/Accounting/Security/PBKDF2PasswordProtection.cs), made by the console; it is installed with
// Account.ApplyPasswordWrite (UOContent/Accounting/Account.cs:411) and rehashed to the configured algorithm at the
// first login (AccountHandler.cs:414, Security/PasswordWorker.cs:231). The request file is removed once the wiped
// world is saved; until then a crash leaves it, and the next boot does the wipe again from scratch.
//
// ONCE. The request's id goes into this persistence (AccountWipe/AccountWipe.bin, test shard only) in the same save as
// the wipe, so a request that is somehow read again is recognised, removed and not carried out twice.
//
// The log lines are read by Shard-Console.ps1 (ConvertFrom-WipeLog): change both together. Console.WriteLine, as
// SaveOnShutdown does, so each is one line exactly as written.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.BulkOrders;
using Server.Engines.Points;
using Server.Guilds;
using Server.Items;
using Server.Mobiles;
using Server.Multis;

namespace Server.Misc;

public sealed record AccountWipeRequest(string Id, string Admin, string PasswordPbkdf2);

public sealed record AccountWipeCounts(
    int Accounts, int Characters, int Items, int Houses, int Pets, int Boats, int Vendors, int Guilds, int KeptOwnerlessHouses
);

public class TestAccountWipe : GenericPersistence
{
    public const string RequestFileName = "account-wipe-request.json";
    public const string RoleVariable = "SL_SHARD_ROLE";
    public const string RoleValue = "test";
    public const string AllowSetting = "clusterf.testShard.allowAccountWipe";
    public const int LivePort = 2593;
    public const string Tag = "[AccountWipe]";
    public const int MaxRememberedIds = 20;
    public static readonly TimeSpan SaveWait = TimeSpan.FromMinutes(10);

    // 0: cc-P73.
    public const int CurrentVersion = 0;

    private static TestAccountWipe _instance;
    private static readonly List<string> _doneIds = new();
    private static Account _earlyAdmin;
    private static string _lastCensus;

    public TestAccountWipe() : base("AccountWipe", 10)
    {
    }

    // ------------------------------------------------------------------ the guard

    /// <summary>Why this shard may not wipe, or null when it may. Pure, for the tests.</summary>
    public static string Refusal(string role, string allowSetting, IEnumerable<IPEndPoint> listeners)
    {
        if (!string.Equals(role, RoleValue, StringComparison.Ordinal))
        {
            return $"{RoleVariable} is '{role ?? "(unset)"}', not '{RoleValue}': only docker-compose.test.yml sets it";
        }

        if (!bool.TryParse(allowSetting, out var allowed) || !allowed)
        {
            return $"{AllowSetting} is '{allowSetting ?? "(unset)"}' in modernuo.json, not True: only Configuration-test has it";
        }

        foreach (var l in listeners ?? Array.Empty<IPEndPoint>())
        {
            if (l.Port == LivePort)
            {
                return $"this shard listens on {l}, the live port";
            }
        }

        return null;
    }

    private static string CurrentRefusal() =>
        Refusal(
            Environment.GetEnvironmentVariable(RoleVariable),
            ServerConfiguration.GetSetting(AllowSetting, (string)null), // read only: never writes the key into live's config
            ServerConfiguration.Listeners
        );

    public static void Configure()
    {
        // The persistence exists on the test shard only, so live's save never gains an AccountWipe folder.
        if (CurrentRefusal() == null)
        {
            _instance ??= new TestAccountWipe();
        }
    }

    // ------------------------------------------------------------------ the request

    public static string RequestPath => Path.Combine(Path.GetDirectoryName(World.SavePath.TrimEnd('/', '\\')) ?? ".", RequestFileName);

    /// <summary>Reads a request, or says why it cannot be read. Pure, for the tests.</summary>
    public static AccountWipeRequest ParseRequest(string json, out string error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != 1)
            {
                error = "the request is not version 1";
                return null;
            }

            var id = root.TryGetProperty("id", out var i) ? i.GetString() : null;
            var admin = root.TryGetProperty("admin", out var a) ? a.GetString() : null;
            var hash = root.TryGetProperty("passwordPbkdf2", out var h) ? h.GetString() : null;

            if (string.IsNullOrEmpty(id) || id.Length > 64 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                error = "the request has no usable id";
                return null;
            }

            if (string.IsNullOrEmpty(admin) || admin.Length > 30 || !AccountHandler.IsValidUsername(admin))
            {
                error = "the admin name is not one the shard accepts";
                return null;
            }

            if (!IsPbkdf2Hash(hash))
            {
                error = "the password hash is not pinned's PBKDF2 format (84 upper-case hex characters, 1024 to 1536 iterations)";
                return null;
            }

            return new AccountWipeRequest(id, admin, hash);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            error = "the request is not readable JSON: " + e.Message;
            return null;
        }
    }

    // PBKDF2PasswordProtection: 2 bytes of iterations (little-endian), 8 of salt, 32 of hash, upper-case hex.
    public static bool IsPbkdf2Hash(string hex)
    {
        if (hex == null || hex.Length != 84 || !hex.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
        {
            return false;
        }

        var iterations = Convert.ToInt32(hex[2..4] + hex[..2], 16);
        return iterations is >= 1024 and <= 1536;
    }

    private static AccountWipeRequest ReadRequest(out string error)
    {
        error = null;
        var path = RequestPath;
        if (!File.Exists(path))
        {
            return null;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            error = "the request could not be read: " + e.Message;
            return null;
        }

        return ParseRequest(text, out error);
    }

    private static void RemoveRequest()
    {
        try
        {
            File.Delete(RequestPath);
        }
        catch (Exception e)
        {
            Console.WriteLine($"{Tag} could not remove {RequestPath}: {e.Message}. Delete it by hand; it holds a password hash.");
        }
    }

    // ------------------------------------------------------------------ boot

    private static bool IsPlayerHouse(BaseHouse h) => h is { Deleted: false, Owner: PlayerMobile };

    public static string Census()
    {
        var characters = World.Mobiles.Values.Count(m => m is PlayerMobile);
        var houses = BaseHouse.AllHouses.Count(IsPlayerHouse);
        return $"{Tag} census: {Plural(Accounts.Count, "account")}, {Plural(characters, "character")}, {Plural(houses, "house")}";
    }

    private static string Plural(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";

    /// <summary>
    /// What a wipe must leave at nothing, counted from the world, and what it must leave alone. Logged on the test shard
    /// at start and after a wipe, so the proof is the shard's own count (cc-P73 notes).
    /// </summary>
    public static string Audit()
    {
        var houses = BaseHouse.AllHouses.Where(h => !h.Deleted).ToList();
        var mobiles = World.Mobiles.Values;
        var items = World.Items.Values;
        var pets = mobiles.OfType<BaseCreature>().Count(c => c.Controlled || c.ControlMaster != null || c.SummonMaster != null);
        var vendors = mobiles.Count(m => m is PlayerVendor or PlayerBarkeeper);
        var corpses = items.Count(i => i is Corpse { Owner: PlayerMobile });
        var orphans = items.Count(i => i.Parent is IEntity { Deleted: true });
        var internalLoose = items.Count(i => i.Parent == null && i.Map == Map.Internal);
        var boats = BaseBoat.Boats.Count(b => !b.Deleted && b.Owner != null);
        return $"{Tag} audit: {Plural(houses.Count, "house")} ({houses.Count(IsPlayerHouse)} with a player owner, " +
               $"{houses.Count(h => h.Owner == null)} with none), {boats} owned boats, {pets} tamed or summoned creatures, " +
               $"{vendors} player vendors, {corpses} player corpses, {orphans} items in a deleted container, " +
               $"{internalLoose} loose items on Map.Internal, {ClusterFAccountPersistence.All.Count} ClusterF account records, " +
               $"{World.Mobiles.Count} mobiles, {World.Items.Count} items";
    }

    private static void LogCensusIfChanged()
    {
        var c = Census();
        if (c != _lastCensus)
        {
            _lastCensus = c;
            Console.WriteLine(c);
        }
    }

    [CallPriority(int.MaxValue)]
    public static void Initialize()
    {
        // The census is how the console shows what a wipe would delete; logged at start and after a save that changed
        // it, on live too (counts only, no names).
        LogCensusIfChanged();
        EventSink.WorldSave += LogCensusIfChanged;
        if (CurrentRefusal() == null)
        {
            Console.WriteLine(Audit());
        }

        var request = ReadRequest(out var error);
        if (request == null && error == null)
        {
            return;
        }

        var refusal = CurrentRefusal();
        if (refusal != null)
        {
            Console.WriteLine($"{Tag} REFUSED: {refusal}. Nothing was wiped; the request was removed.");
            RemoveRequest();
            return;
        }

        if (request == null)
        {
            Console.WriteLine($"{Tag} FAILED: {error}. Nothing was wiped; the request was removed.");
            RemoveRequest();
            return;
        }

        if (_doneIds.Contains(request.Id))
        {
            Console.WriteLine($"{Tag} ALREADY DONE: request {request.Id} was carried out before. Nothing was wiped again; the request was removed.");
            RemoveRequest();
            return;
        }

        AccountWipeCounts counts;
        try
        {
            counts = Wipe(
                Accounts.GetAccounts().OfType<Account>().Where(a => a != _earlyAdmin).ToList(),
                World.Mobiles.Values.OfType<PlayerMobile>().ToHashSet(),
                clearShardRecords: true
            );

            // 11. The new admin, and the protected list made to name only it. ServerAccess makes a protected name Owner
            //     on its first login with its own password (ServerAccess.cs:72-95), and auto account creation would let
            //     anyone on the LAN create 'aetherion' or 'admin' after the wipe, so the old names come off the list.
            var admin = _earlyAdmin ?? CreateAdmin(request);
            var list = ServerAccess.ServerAccessConfiguration.ProtectedAccounts;
            list.Clear();
            list.Add(admin.Username.ToLowerInvariant());
            ServerAccess.SaveConfiguration();
        }
        catch (Exception e)
        {
            // Stop at once, saving nothing: the save on disk is still the one from before the wipe. Not a throw: an
            // unhandled exception can stop at "press return to exit" (Server/Main.cs:430-434), the D40 hang. The
            // console removes the request when it reads this line.
            Console.WriteLine($"{Tag} FAILED: the wipe threw {e.GetType().Name}: {e.Message}. Nothing was saved; the shard stops.");
            Console.WriteLine(e);
            Environment.Exit(1);
            return;
        }

        _doneIds.Add(request.Id);
        if (_doneIds.Count > MaxRememberedIds)
        {
            _doneIds.RemoveAt(0);
        }

        Console.WriteLine($"{Tag} wiped in memory; saving the world before the request is removed.");
        LogCensusIfChanged();
        WaitForSave(request, counts);
    }

    // A world with no accounts asks for an owner on the console in AccountPrompt.Initialize. When a valid request is
    // waiting, make its admin first, so the prompt is never reached; Wipe keeps that account.
    public static class EarlyAdmin
    {
        [CallPriority(int.MinValue)]
        public static void Initialize()
        {
            if (Accounts.Count != 0 || CurrentRefusal() != null)
            {
                return;
            }

            var request = ReadRequest(out _);
            if (request != null && !_doneIds.Contains(request.Id))
            {
                _earlyAdmin = CreateAdmin(request);
                Console.WriteLine($"{Tag} the world has no accounts; made the admin before the owner prompt.");
            }
        }
    }

    // ------------------------------------------------------------------ the wipe

    /// <summary>
    /// Deletes these accounts and these characters, with everything they own (steps 1 to 10). The boot passes every
    /// account but a just-made admin, and every character. The tests pass their own, and leave the shard records alone.
    /// </summary>
    public static AccountWipeCounts Wipe(IReadOnlyCollection<Account> accounts, HashSet<PlayerMobile> players, bool clearShardRecords)
    {
        var itemsBefore = World.Items.Count;
        var accountsBefore = accounts.Count;
        var guildsBefore = World.Guilds.Count;
        var books = World.Items.Values.OfType<BulkOrderBook>().ToList();
        bool Wiped(Mobile m) => m is PlayerMobile pm && players.Contains(pm);

        // 1. Pets, summons, hirelings and escorts first. Deleting their master would release an unbonded pet into the
        //    wild for three days instead (PlayerMobile.cs:3847-3862, PetOrders.cs:476-530). Shrunk pets on
        //    Map.Internal still name their master (ShrunkPet.cs:56-66), so they go here too.
        var pets = World.Mobiles.Values.OfType<BaseCreature>()
            .Where(c => Wiped(c.ControlMaster) || Wiped(c.SummonMaster)).ToList();
        foreach (var c in pets)
        {
            c.Delete();
        }

        // 2. Vendors and barkeepers owned by a character, wherever they stand. A plain Delete takes their stock with
        //    them; Destroy (what a house delete does, BaseHouse.cs:640-657) would drop it in a backpack on the ground
        //    or leak it on Map.Internal (PlayerVendor.cs:316-387).
        var vendors = World.Mobiles.Values
            .Where(m => m is PlayerVendor v && Wiped(v.Owner) || m is PlayerBarkeeper b && Wiped(b.Owner)).ToList();

        // 3. Houses with a character as owner, and everything in them. BaseHouse.OnAfterDelete unlocks lockdowns and
        //    secure containers and leaves them in the world (BaseHouse.cs:3400-3456), so they are deleted first.
        //    A house with no owner is kept and counted: server-placed buildings have none.
        var houses = BaseHouse.AllHouses.Where(h => !h.Deleted && Wiped(h.Owner)).ToList();
        var keptOwnerless = BaseHouse.AllHouses.Count(h => !h.Deleted && h.Owner == null);
        foreach (var h in houses)
        {
            vendors.AddRange(h.PlayerVendors);
            vendors.AddRange(h.PlayerBarkeepers);
            vendors.AddRange(h.InternalizedVendors ?? new List<Mobile>());
        }

        vendors = vendors.Distinct().ToList();
        foreach (var v in vendors)
        {
            v.Delete();
        }

        foreach (var h in houses)
        {
            DeleteAll(h.LockDowns);
            DeleteAll(h.Secures?.Select(s => (Item)s.Item));
            DeleteAll(h.VendorRentalContracts);
            DeleteAll(h.Addons); // a deleted addon gives back no deed (BaseHouse.cs:3467)
            DeleteAll(h.GetItems());
            foreach (var inv in h.VendorInventories.ToList())
            {
                inv.Delete();
            }

            h.MovingCrate?.Delete();
            h.LockDowns?.Clear();
            h.Secures?.Clear();
            h.VendorRentalContracts?.Clear();
            h.Addons?.Clear();
            h.PlayerVendors.Clear();
            h.PlayerBarkeepers.Clear();
            h.InternalizedVendors?.Clear();
            h.Delete();
        }

        // 4. Boats: nothing deletes a boat with its owner (BaseBoat.cs:298, no PlayerDeletedEvent handler).
        var boats = BaseBoat.Boats.Where(b => !b.Deleted && Wiped(b.Owner)).ToList();
        foreach (var b in boats)
        {
            b.Delete();
        }

        // 5. Things in the world that name a character and outlive it: corpses (Corpse.cs:135), trash bags left
        //    outside a pack (TrashBag.cs:41-43), bulletin posts (BulletinMessage.cs:42).
        DeleteAll(World.Items.Values.Where(i =>
            i is Corpse co && Wiped(co.Owner) ||
            i is TrashBag tb && Wiped(tb.Owner) ||
            i is BulletinMessage bm && Wiped(bm.Poster)).ToList());

        // 6. The accounts: each deletes its characters and their houses (Account.cs:331-363). Then any character with
        //    no account.
        foreach (var a in accounts)
        {
            a.Delete();
        }

        foreach (var pm in players.Where(pm => !pm.Deleted).ToList())
        {
            pm.Delete();
        }

        // 7. Orphans the deletes above made: items inside something deleted (a Jacob's pickaxe's exhausted replacement
        //    lands in the pack being deleted, JacobsReinforcedPickaxe.cs:164), creatures whose master is gone.
        for (var pass = 0; pass < 5; pass++)
        {
            var orphans = World.Items.Values.Where(i => i.Parent is IEntity { Deleted: true }).Cast<IEntity>()
                .Concat(World.Mobiles.Values.OfType<BaseCreature>().Where(c => c.Controlled && c.ControlMaster is { Deleted: true }))
                .ToList();
            if (orphans.Count == 0)
            {
                break;
            }

            foreach (var o in orphans)
            {
                o.Delete();
            }
        }

        // 8. Bulk order book entries live in their own persistence and a deleted book leaves them there
        //    (BaseBOBEntry.cs:37-41 has no caller on book delete).
        foreach (var book in books.Where(b => b.Deleted))
        {
            foreach (var e in book.Entries.ToList())
            {
                e.Delete();
            }
        }

        // 9. Guilds: the last member's delete disbands each (Guild.cs:1342-1380). Any left with no live leader go too.
        foreach (var g in World.Guilds.Values.OfType<Guild>().Where(g => g.Leader is null or { Deleted: true }).ToList())
        {
            g.Disband();
        }

        // 10. Shard data keyed by account or character, after every delete: deleting a Jacob's pickaxe writes its
        //     account record back (ClusterFRestorationRegistry.cs:150).
        if (clearShardRecords)
        {
            ClusterFAccountPersistence.ClearAll();
            ClusterFAchievementSystem.LoadFromSave(new(), new());
            foreach (var system in PointsSystem.Systems)
            {
                system.Clear();
            }
        }

        return new AccountWipeCounts(
            accountsBefore,
            players.Count,
            itemsBefore - World.Items.Count,
            houses.Count,
            pets.Count,
            boats.Count,
            vendors.Count,
            guildsBefore - World.Guilds.Count,
            keptOwnerless
        );
    }

    private static void DeleteAll(IEnumerable<Item> items)
    {
        if (items == null)
        {
            return;
        }

        foreach (var i in items.ToList())
        {
            if (i is { Deleted: false })
            {
                i.Delete();
            }
        }
    }

    public static Account CreateAdmin(AccountWipeRequest request)
    {
        // The constructor hashes a throwaway password with the configured algorithm; the console's hash replaces it.
        var a = new Account(request.Admin, Guid.NewGuid().ToString("N"))
        {
            AccessLevel = AccessLevel.Owner
        };
        a.ApplyPasswordWrite(request.PasswordPbkdf2, PasswordProtectionAlgorithm.PBKDF2);
        return a;
    }

    // ------------------------------------------------------------------ saving, then removing the request

    private static void WaitForSave(AccountWipeRequest request, AccountWipeCounts counts)
    {
        // World.Save only queues: the snapshot runs on the event loop (World.cs:281), which starts after Initialize, and
        // a published save sets Saves' write time to Core.Now (World.cs:466). The save on disk now is from before this
        // boot, so a write time at or after this moment is this save.
        var asked = Core.Now;
        var deadline = asked + SaveWait;
        var lastAsk = asked;
        World.Save();

        Timer timer = null;
        timer = Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
        {
            var published = World.WorldState == WorldState.Running &&
                            Directory.Exists(World.SavePath) &&
                            Directory.GetLastWriteTimeUtc(World.SavePath) >= asked;
            if (published)
            {
                timer.Stop();
                RemoveRequest();
                Console.WriteLine(
                    $"{Tag} DONE: request {request.Id}: deleted {Plural(counts.Accounts, "account")}, " +
                    $"{Plural(counts.Characters, "character")}, {Plural(counts.Items, "item")}, {Plural(counts.Houses, "house")}, " +
                    $"{Plural(counts.Pets, "pet")}, {Plural(counts.Boats, "boat")}, {Plural(counts.Vendors, "vendor")}, " +
                    $"{Plural(counts.Guilds, "guild")}; kept {Plural(counts.KeptOwnerlessHouses, "house")} with no owner; " +
                    $"admin '{request.Admin}' made (Owner); world saved; request removed"
                );
                LogCensusIfChanged();
                Console.WriteLine(Audit());
            }
            else if (Core.Now > deadline)
            {
                timer.Stop();
                Console.WriteLine(
                    $"{Tag} FAILED: no save was published within {SaveWait.TotalMinutes} minutes of the wipe. The request is " +
                    "kept, so the next boot wipes again from the save on disk; the console removes it if it gives up first."
                );
            }
            else if (World.WorldState == WorldState.Running && Core.Now > lastAsk + TimeSpan.FromSeconds(60))
            {
                // No save under way and none published a minute after the last ask: it failed. Ask again.
                lastAsk = Core.Now;
                World.Save();
            }
        });
    }

    // ------------------------------------------------------------------ persistence

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(_doneIds.Count);
        foreach (var id in _doneIds)
        {
            writer.Write(id);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version != CurrentVersion)
        {
            throw new Exception($"AccountWipe: unknown version {version}");
        }

        _doneIds.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            _doneIds.Add(reader.ReadString());
        }
    }
}
