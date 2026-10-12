// TestAccountWipeVerification.cs
//
// cc-P73. The test account wipe, customizations Misc/TestAccountWipe.cs. Notes in shard-migration
// notes/cc-P73-test-account-wipe.md. The wipe itself deletes every account in the world, so it is proved on a copy of
// the test world (the notes), not here, where it would delete other facts' fixtures. These are its locks and its inputs.
//
//   1 the guard: a wipe is allowed only with SL_SHARD_ROLE=test, the setting True, and no listener on 2593 (live).
//   2 a request is read only when it is version 1, has an id, names an admin the shard accepts and carries a PBKDF2 hash
//     in pinned's format; a plain password in its place is refused.
//   4 a wipe of one character deletes its pet (in the world and shrunk on Map.Internal), its house with the lockdown,
//     secure box and gold, loose item and vendor with stock inside it, its boat and its trash bag, and keeps a wild
//     creature and an unowned item: what the test world copy could not show, having none of them.
//   3 the console's hash (Shard-Console.ps1 New-WipePasswordHash; these vectors were made by Python's hashlib, a third
//     implementation) verifies in pinned's PBKDF2PasswordProtection, and the admin made from it logs in with that
//     password, is Owner, and is rehashed to the configured algorithm on that first check.

using System;
using System.Collections.Generic;
using System.Net;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TestAccountWipeVerification
{
    // hashlib.pbkdf2_hmac('sha256', 'correct horse', 01..08, 1200) with the iteration and salt prefix, upper-case hex.
    private const string Vector = "B0040102030405060708AD8E61DCEDE035E57B71FBFF78B61BAA4D7A54B257FDB5A7D4DF9BEA21B9B45F";
    // The same for 'päss wörd €' at 1536 iterations: the password is UTF-8 on both sides.
    private const string UnicodeVector = "00060102030405060708C791FCAC584D61D7399B74241102E40492EA3EFBA683594D1F269F400DCC2564";

    private static readonly IPEndPoint[] TestListeners = [new(IPAddress.Any, 2594)];

    [Fact]
    public void TheWipeIsAllowedOnlyWithAllThreeTestShardLocks()
    {
        Assert.Null(TestAccountWipe.Refusal("test", "True", TestListeners));
        Assert.Null(TestAccountWipe.Refusal("test", "true", TestListeners));

        Assert.Contains("SL_SHARD_ROLE", TestAccountWipe.Refusal(null, "True", TestListeners));
        Assert.Contains("SL_SHARD_ROLE", TestAccountWipe.Refusal("live", "True", TestListeners));
        Assert.Contains("SL_SHARD_ROLE", TestAccountWipe.Refusal("TEST", "True", TestListeners));
        Assert.Contains("allowAccountWipe", TestAccountWipe.Refusal("test", null, TestListeners));
        Assert.Contains("allowAccountWipe", TestAccountWipe.Refusal("test", "False", TestListeners));
        Assert.Contains("allowAccountWipe", TestAccountWipe.Refusal("test", "yes", TestListeners));
        Assert.Contains("live port", TestAccountWipe.Refusal("test", "True", [new IPEndPoint(IPAddress.Any, 2593)]));
        Assert.Contains("live port", TestAccountWipe.Refusal("test", "True", [new IPEndPoint(IPAddress.Any, 2594), new IPEndPoint(IPAddress.Loopback, 2593)]));
    }

    private static string Request(string id = "20261012-150000-abcd1234", string admin = "newadmin", string hash = Vector, int version = 1) =>
        $$"""{ "version": {{version}}, "id": "{{id}}", "admin": "{{admin}}", "passwordPbkdf2": "{{hash}}", "requestedAt": "2026-10-12T15:00:00Z" }""";

    [Fact]
    public void ARequestIsReadOnlyInTheShapeTheConsoleWrites()
    {
        var r = TestAccountWipe.ParseRequest(Request(), out var error);
        Assert.Null(error);
        Assert.Equal("20261012-150000-abcd1234", r.Id);
        Assert.Equal("newadmin", r.Admin);
        Assert.Equal(Vector, r.PasswordPbkdf2);

        Assert.Null(TestAccountWipe.ParseRequest(Request(version: 2), out error));
        Assert.Contains("version", error);
        Assert.Null(TestAccountWipe.ParseRequest(Request(id: ""), out error));
        Assert.Contains("id", error);
        Assert.Null(TestAccountWipe.ParseRequest(Request(admin: "bad/name"), out error));
        Assert.Contains("admin", error);
        Assert.Null(TestAccountWipe.ParseRequest(Request(admin: "trailingdot."), out error));
        // A password where the hash belongs, lower-case hex, and an iteration count pinned never writes are all refused.
        Assert.Null(TestAccountWipe.ParseRequest(Request(hash: "correct horse"), out error));
        Assert.Contains("PBKDF2", error);
        Assert.Null(TestAccountWipe.ParseRequest(Request(hash: Vector.ToLowerInvariant()), out error));
        Assert.Null(TestAccountWipe.ParseRequest(Request(hash: "FFFF" + Vector[4..]), out error));
        Assert.Null(TestAccountWipe.ParseRequest("not json", out error));
        Assert.Contains("JSON", error);
    }

    [Fact]
    public void TheConsolesHashVerifiesInPinnedsPbkdf2()
    {
        Assert.True(TestAccountWipe.IsPbkdf2Hash(Vector));
        Assert.True(PBKDF2PasswordProtection.Instance.ValidatePassword(Vector, "correct horse"));
        Assert.False(PBKDF2PasswordProtection.Instance.ValidatePassword(Vector, "correct horsE"));
        Assert.True(PBKDF2PasswordProtection.Instance.ValidatePassword(UnicodeVector, "päss wörd €"));
    }

    // A house with a floor, as CC6FollowupBreathIncubatorVerification's: the host has no multi data, and the placement
    // table keys on the exact type (HousePlacementTool.cs:2181-2183), so the secure and lockdown limits are given here.
    private sealed class WipeHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public WipeHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;
        public override int GetAosMaxSecures() => 425;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    [Fact]
    public void AWipeLeavesNothingTheCharacterOwnedAndKeepsTheWorld()
    {
        // The test world copy (notes) has no houses, pets, vendors or boats, so they are made here: one character with
        // a pet in the world, a shrunk pet on Map.Internal, a house holding a lockdown, a secure box with gold, a loose
        // item and a vendor with stock, a boat, and a trash bag on the ground. A wild horse and an item on the ground
        // that nobody owns must survive.
        var at = new Point3D(1500, 1500, 0);
        var account = new Account($"p73{Guid.NewGuid():N}"[..16], "p73-test-only");
        var pm = new PlayerMobile();
        account[0] = pm;
        pm.MoveToWorld(new Point3D(at.X + 6, at.Y, 0), Map.Felucca);

        var pet = new Horse();
        pet.MoveToWorld(new Point3D(at.X + 7, at.Y, 0), Map.Felucca);
        Assert.True(pet.SetControlMaster(pm));
        var shrunk = new Horse();
        Assert.True(shrunk.SetControlMaster(pm));
        shrunk.Internalize();
        var wild = new Horse();
        wild.MoveToWorld(new Point3D(at.X + 8, at.Y, 0), Map.Felucca);

        var house = new WipeHouse(pm);
        house.MoveToWorld(at, Map.Felucca);
        var lockdown = new Item(0xEED);
        lockdown.MoveToWorld(at, Map.Felucca);
        Assert.True(house.LockDown(pm, lockdown, false));
        var box = new MetalBox();
        box.MoveToWorld(new Point3D(at.X + 1, at.Y, 0), Map.Felucca);
        house.AddSecure(pm, box);
        var gold = new Gold(500);
        box.DropItem(gold);
        var loose = new Item(0xEEF);
        loose.MoveToWorld(new Point3D(at.X - 1, at.Y, 0), Map.Felucca);
        var vendor = new PlayerVendor(pm, house);
        vendor.MoveToWorld(new Point3D(at.X, at.Y + 1, 0), Map.Felucca);
        house.PlayerVendors.Add(vendor);
        var stock = new Item(0x13FF);
        vendor.Backpack.DropItem(stock);

        var boat = new SmallBoat { Owner = pm };
        boat.MoveToWorld(new Point3D(at.X + 40, at.Y, -5), Map.Felucca);
        var bag = new TrashBag { Owner = pm };
        bag.MoveToWorld(new Point3D(at.X + 9, at.Y, 0), Map.Felucca);
        var world = new Item(0xEEE);
        world.MoveToWorld(new Point3D(at.X + 10, at.Y, 0), Map.Felucca);

        Assert.True(house.LockDowns.Contains(lockdown) && house.Secures.Exists(s => s.Item == box));
        try
        {
            var counts = TestAccountWipe.Wipe([account], [pm], clearShardRecords: false);

            Assert.True(account.Deleted && pm.Deleted, "the account and its character");
            Assert.True(pet.Deleted && shrunk.Deleted, "the pet, and the one on Map.Internal, deleted, not released");
            Assert.True(house.Deleted, "the house");
            Assert.True(lockdown.Deleted, "its lockdown, which a house delete alone leaves unlocked in the world");
            Assert.True(box.Deleted && gold.Deleted, "its secure box and what is in it");
            Assert.True(loose.Deleted, "a loose item inside it");
            Assert.True(vendor.Deleted && stock.Deleted, "its vendor and the stock, not dropped on the ground");
            Assert.True(boat.Deleted, "the boat, which nothing deletes with its owner");
            Assert.True(bag.Deleted, "the trash bag on the ground");
            Assert.False(wild.Deleted, "a creature nobody owns stays");
            Assert.False(world.Deleted, "an item nobody owns stays");
            Assert.Equal(1, counts.Accounts);
            Assert.Equal(1, counts.Characters);
            Assert.Equal(1, counts.Houses);
            Assert.Equal(2, counts.Pets);
            Assert.Equal(1, counts.Boats);
            Assert.Equal(1, counts.Vendors);
            Assert.Null(Accounts.GetAccount(account.Username));
        }
        finally
        {
            foreach (var e in new IEntity[] { pm, pet, shrunk, wild, house, lockdown, box, loose, vendor, boat, bag, world })
            {
                e.Delete();
            }

            if (!account.Deleted)
            {
                account.Delete();
            }
        }
    }

    [Fact]
    public void TheAdminMadeFromARequestLogsInWithThatPasswordAsOwner()
    {
        var name = $"p73{Guid.NewGuid():N}"[..16];
        var request = TestAccountWipe.ParseRequest(Request(admin: name), out var error);
        Assert.Null(error);

        var a = TestAccountWipe.CreateAdmin(request);
        try
        {
            Assert.Same(a, Accounts.GetAccount(name));
            Assert.Equal(AccessLevel.Owner, a.AccessLevel);
            Assert.Equal(PasswordProtectionAlgorithm.PBKDF2, a.PasswordAlgorithm);
            Assert.False(a.CheckPassword("wrong"));
            Assert.True(a.CheckPassword("correct horse"));
            // The first good check rehashes to the configured algorithm (Account.cs:441-444), and it still verifies.
            Assert.Equal(AccountSecurity.CurrentAlgorithm, a.PasswordAlgorithm);
            Assert.True(a.CheckPassword("correct horse"));
        }
        finally
        {
            a.Delete();
        }
    }
}
