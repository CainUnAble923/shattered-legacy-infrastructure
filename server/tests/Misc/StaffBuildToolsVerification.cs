// StaffBuildToolsVerification.cs
//
// cc-P812: the Visual Builder's decor capture and the animation locator. Notes in shard-migration
// notes/cc-P812-decor-capture-animation-locator.md.
//
// Facts: a capture writes each piece's ID, place, hue, name and a door's facing as [Decorate text, relative or in world
// coordinates, and flattens what it cannot remake; what it writes places back the same through [PlaceBuild; an open door is
// captured closed; a box too big, a bad name and skipped types are refused or left out; [VBuild capture asks for corners and
// the file lands in the capture folder. The locator counts actions by body type, steps a stand of our body through every
// action and removes it, plays on a targeted mobile without touching it, stops on demand, lists our bodies, and a saved
// stand deletes itself. A bad settings file is refused whole.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StaffBuildToolsVerification : IDisposable
{
    private static readonly Point3D Corner = new(5720, 1200, 0);
    private readonly List<IEntity> _made = [];
    private readonly List<Account> _accounts = [];

    public StaffBuildToolsVerification()
    {
        ShardTestHost.EnsureAccounts();
        StaffBuildToolsSettings.Current = new StaffBuildToolsSettings();
        ShardBodyTypes.Apply();
        AnimLocator.Runs.Clear();
    }

    public void Dispose()
    {
        foreach (var run in AnimLocator.Runs.Values.ToList())
        {
            AnimLocator.Stop(run.Staff);
        }

        foreach (var e in _made)
        {
            e?.Delete();
        }

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                account[i]?.Delete();
            }

            Accounts.Remove(account);
        }

        StaffBuildToolsSettings.Current = new StaffBuildToolsSettings();
    }

    private T Track<T>(T e) where T : IEntity
    {
        _made.Add(e);
        return e;
    }

    private T Put<T>(T item, int dx, int dy, int z = 0) where T : Item
    {
        Track(item).MoveToWorld(new Point3D(Corner.X + dx, Corner.Y + dy, z), Map.Felucca);
        return item;
    }

    private PlayerMobile Staff(AccessLevel level = AccessLevel.GameMaster)
    {
        var account = new Account($"p812{Guid.NewGuid():N}"[..16], "p812-test-only");
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, AccessLevel = level, Name = "Builder" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(Corner.X - 2, Corner.Y - 2, 0), Map.Felucca);
        return pm;
    }

    private (Static Banner, Static Plain, DarkWoodDoor Door, AddonComponent Part, Lantern Lamp) Decorate()
    {
        var banner = Put(new Static(0x0A99) { Hue = 0x482, Name = "a guild banner" }, 1, 1);
        var plain = Put(new Static(0x0007), 2, 1);
        var door = Put(new DarkWoodDoor(DoorFacing.WestCCW), 3, 0);
        var part = Put(new BareAddonPart(), 4, 2);
        var lamp = Put(new Lantern(), 0, 3, 5);
        return (banner, plain, door, part, lamp);
    }

    // An addon part that could be made bare, as some are: the capture must still write it as a Static, not as its type.
    private class BareAddonPart : AddonComponent
    {
        public BareAddonPart() : base(0x1234)
        {
        }

        public BareAddonPart(Serial serial) : base(serial)
        {
        }
    }

    // ---------------------------------------------------------------- capture

    [Fact]
    public void ACaptureWritesEachPieceAsDecorationData()
    {
        Decorate();
        var pieces = DecorCapture.Collect(Map.Felucca, new Point2D(Corner.X, Corner.Y), new Point2D(Corner.X + 5, Corner.Y + 4),
            out var refused, out var flattened);
        Assert.Null(refused);
        Assert.Equal(5, pieces.Count);
        Assert.Equal(1, flattened); // the addon part

        var text = DecorCapture.ToDecorate(pieces, Corner, ["a test"]);
        var lines = text.Split('\n');
        Assert.Equal("# a test", lines[0]);
        Assert.Contains("Static 0x0A99 (Hue=0x482;Name=a guild banner)\n1 1 0\n", text);
        Assert.Contains("Static 0x0007\n2 1 0\n", text);
        Assert.Contains("DarkWoodDoor (Facing=WestCCW)\n3 0 0\n", text);
        Assert.Contains("Static 0x1234\n4 2 0\n", text);
        Assert.Contains($"Lantern 0x{new Lantern().ItemID:X4}\n0 3 5\n", text);

        // World form keeps the world's coordinates.
        var world = DecorCapture.ToDecorate(pieces, null, []);
        Assert.Contains($"DarkWoodDoor (Facing=WestCCW)\n{Corner.X + 3} {Corner.Y} 0\n", world);
    }

    [Fact]
    public void WhatACaptureWritesPlacesBackTheSame()
    {
        var (banner, _, door, _, lamp) = Decorate();
        var pieces = DecorCapture.Collect(Map.Felucca, new Point2D(Corner.X, Corner.Y), new Point2D(Corner.X + 5, Corner.Y + 4), out _, out _);
        var text = DecorCapture.ToDecorate(pieces, Corner, ["round trip"]);
        Assert.NotEmpty(PlaceBuild.Parse(text));

        var closedId = door.ClosedId;
        foreach (var e in _made.ToList())
        {
            e.Delete();
        }

        // Placed somewhere else: every piece comes back, relative to the new origin.
        var at = new Point3D(Corner.X + 20, Corner.Y, 0);
        var before = World.Items.Keys.ToHashSet();
        Assert.Equal(5, PlaceBuild.Place(text, Map.Felucca, at));
        var placed = World.Items.Values.Where(i => !before.Contains(i.Serial)).ToList();
        foreach (var i in placed)
        {
            _made.Add(i);
        }

        Assert.Contains(placed, i => i is Static { ItemID: 0x0A99, Hue: 0x482, Name: "a guild banner" } && i.X == at.X + 1 && i.Y == at.Y + 1);
        Assert.Contains(placed, i => i is DarkWoodDoor d && d.ClosedId == closedId && i.X == at.X + 3 && i.Y == at.Y);
        Assert.Contains(placed, i => i is Lantern && i.X == at.X && i.Y == at.Y + 3 && i.Z == 5);
        Assert.Contains(placed, i => i is Static { ItemID: 0x1234 });
    }

    [Fact]
    public void AnOpenDoorIsCapturedClosedWhereItHangs()
    {
        var door = Put(new DarkWoodDoor(DoorFacing.SouthCW), 2, 2);
        door.Open = true;
        Assert.NotEqual(new Point3D(Corner.X + 2, Corner.Y + 2, 0), door.Location);

        var pieces = DecorCapture.Collect(Map.Felucca, new Point2D(Corner.X, Corner.Y), new Point2D(Corner.X + 5, Corner.Y + 5), out _, out _);
        var piece = Assert.Single(pieces);
        Assert.Equal("DarkWoodDoor (Facing=SouthCW)", piece.Header);
        Assert.Equal((Corner.X + 2, Corner.Y + 2, 0), (piece.X, piece.Y, piece.Z));
    }

    [Fact]
    public void ABoxTooBigABadNameAndSkippedTypesAreRefusedOrLeftOut()
    {
        Assert.Null(DecorCapture.Collect(Map.Felucca, new Point2D(0, 0), new Point2D(100, 10), out var refused, out _));
        Assert.Contains("64 x 64", refused);

        Put(new Static(0x0007), 1, 1);
        var corpseLike = Put(new Moongate(), 2, 2);
        var pieces = DecorCapture.Collect(Map.Felucca, new Point2D(Corner.X, Corner.Y), new Point2D(Corner.X + 3, Corner.Y + 3), out _, out _);
        Assert.Single(pieces);

        var gm = Staff();
        var msg = DecorCapture.Capture(gm, "../escape", true, Map.Felucca, Corner, new Point3D(Corner.X + 3, Corner.Y + 3, 0), out var ok);
        Assert.False(ok);
        Assert.Contains("name", msg);
        Assert.False(File.Exists(DecorCapture.PathOf("../escape")));

        StaffBuildToolsSettings.Current.CaptureMaxPieces = 0;
        Assert.Null(DecorCapture.Collect(Map.Felucca, new Point2D(Corner.X, Corner.Y), new Point2D(Corner.X + 3, Corner.Y + 3), out refused, out _));
        Assert.Contains("at most 0", refused);
        _ = corpseLike;
    }

    [Fact]
    public void VBuildCaptureAsksForCornersAndWritesTheFile()
    {
        ClusterFVisualBuilder.Configure();
        var gm = Staff();
        Decorate();

        var path = DecorCapture.PathOf("p812-test");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        // The command's own two targets, answered as the client answers them.
        Assert.True(CommandSystem.Handle(gm, "[VBuild capture p812-test"));
        Assert.NotNull(gm.Target);
        gm.Target.Invoke(gm, new LandTarget(Corner, Map.Felucca));
        Assert.NotNull(gm.Target);
        gm.Target.Invoke(gm, new LandTarget(new Point3D(Corner.X + 5, Corner.Y + 4, 0), Map.Felucca));

        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("[PlaceBuild p812-test", text);
        Assert.Contains("DarkWoodDoor (Facing=WestCCW)", text);
        Assert.NotEmpty(PlaceBuild.Parse(text));
        File.Delete(path);
    }

    // ---------------------------------------------------------------- the animation locator

    [Fact]
    public void TheNumberOfActionsComesFromTheBodyType()
    {
        Assert.Equal(35, AnimLocator.ActionCount(400));      // human
        Assert.Equal(13, AnimLocator.ActionCount(0xC8));     // horse: animal
        Assert.Equal(22, AnimLocator.ActionCount(2049));     // our gourdling: monster
        Assert.Equal(13, AnimLocator.ActionCount(739));      // our leather wolf: animal (ShardBodyTypes)
        Assert.Equal("gourdling", AnimLocator.NameOf(2049));
    }

    [Fact]
    public void AStandOfOurBodyStepsThroughEveryActionAndGoes()
    {
        var gm = Staff();
        ShardTestClock.Arm();
        var run = AnimLocator.Start(gm, 2049);
        var stand = Assert.IsType<AnimLocatorStand>(run.Subject);
        Assert.Equal(2049, (int)stand.Body);
        Assert.True(stand.Blessed && stand.Frozen);
        Assert.Equal(1, run.Next); // action 0 played at once
        Assert.Equal(21, run.Last);

        ShardTestClock.AdvanceSeconds(3 * 10 + 1);
        Assert.Equal(11, run.Next);
        ShardTestClock.AdvanceSeconds(3 * 11);
        Assert.Equal(22, run.Next);          // the last action, 21, is playing
        Assert.False(stand.Deleted);
        Assert.True(AnimLocator.Runs.ContainsKey(gm));
        ShardTestClock.AdvanceSeconds(3);
        Assert.True(stand.Deleted);
        Assert.False(AnimLocator.Runs.ContainsKey(gm));
    }

    [Fact]
    public void ATargetedMobileIsPlayedButKeptAndStopEndsARun()
    {
        var gm = Staff();
        var horse = Track(new Horse());
        horse.MoveToWorld(new Point3D(Corner.X, Corner.Y, 0), Map.Felucca);

        ShardTestClock.Arm();
        var run = AnimLocator.Begin(gm, horse, false, 10);
        Assert.Equal(11, run.Next);
        ShardTestClock.AdvanceSeconds(3 * 5);
        Assert.False(AnimLocator.Runs.ContainsKey(gm));
        Assert.False(horse.Deleted);

        var again = AnimLocator.Start(gm, 400);
        Assert.True(AnimLocator.Stop(gm));
        Assert.True(again.Subject.Deleted);
        Assert.False(AnimLocator.Stop(gm));
    }

    [Fact]
    public void TheGumpListsOurBodiesAndASavedStandGoes()
    {
        var gm = Staff();
        var g = new AnimLocatorGump(0);
        var buttons = g.Entries.OfType<Server.Gumps.GumpButton>().Select(b => b.ButtonID).ToList();
        Assert.Contains(AnimLocatorGump.BtnBodyBase, buttons);
        Assert.Contains(AnimLocatorGump.BtnNext, buttons);

        var player = Staff(AccessLevel.Player);
        g.Respond(player, AnimLocatorGump.BtnBodyBase);
        Assert.False(AnimLocator.Runs.ContainsKey(player));

        g.Respond(gm, AnimLocatorGump.BtnBodyBase);
        Assert.Equal(ShardBodyTypes.Entries[0].Body, AnimLocator.Runs[gm].Body);
        AnimLocator.Stop(gm);

        var stand = Track(new AnimLocatorStand(2049));
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        stand.Serialize(writer);
        writer.Flush();
        ShardTestClock.Arm();
        var copy = (AnimLocatorStand)Activator.CreateInstance(typeof(AnimLocatorStand), stand.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        ShardTestClock.AdvanceSeconds(0.1);
        Assert.True(copy.Deleted);
    }

    [Fact]
    public void ABadSettingsFileIsRefusedWhole()
    {
        var errors = new List<string>();
        Assert.Null(StaffBuildToolsSettings.Parse("{\"CaptureFolder\": \"../../repo\"}", errors));
        Assert.NotEmpty(errors);

        errors.Clear();
        Assert.Null(StaffBuildToolsSettings.Parse("{\"ActionsByBodyType\": {\"Dragon\": 5}}", errors));

        errors.Clear();
        var ok = StaffBuildToolsSettings.Parse(StaffBuildToolsSettings.ToJson(new StaffBuildToolsSettings { SecondsPerAction = 1.5 }), errors);
        Assert.Empty(errors);
        Assert.Equal(1.5, ok.SecondsPerAction);
    }
}
