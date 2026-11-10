// cc-P858: the lamps that really light (customizations/Items/Lights/ShardLamps.cs) and the fortune teller's wagon beside
// the Festival stall (customizations/FestivalWagon/, RewardStores/FestivalStore.cs FestivalMarket).
//
// Facts:
//   1. Eleven lamps, each starting unlit on its first look, every look a distinct unlit/lit pair, credited.
//   2. Double-click within 2 tiles lights it and again puts it out; farther does nothing; it never burns out.
//   3. Flipping (the decorator's FlippableAttribute path) turns a lamp to its other facing and keeps it lit or unlit; a
//      one-look lamp stays; the jack-o-lantern steps through its 11 carvings and back.
//   4. The stores: ten lamps in the Achievement store (AP decor), the jack-o-lantern in the Festival's Halloween stock;
//      each row hands over its lamp.
//   5. A lit, turned lamp survives a save.
//   6. The wagon's design: Olorin's parts with the ladder out and a two-wide stair from the ground to the porch, 5 a step;
//      nothing on the roof (cc-P872): the rune glyphs and the vent out, the black cat on the porch's potion kegs.
//   7. The wagon addon lays every part at its offset with its hue, light and name.
//   8. The stall puts the wagon up with it and takes it down with it; a world whose stall is already up gets the wagon
//      once (TopUpWagon); nothing when the stall is down or the wagon is set off.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.RewardStores;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LampsAndWagonVerification
{
    private static readonly Point3D Spot = new(1460, 1500, 0);

    public LampsAndWagonVerification()
    {
        ShardTestHost.EnsureSkillChecks();
    }

    private static PlayerMobile NewPlayer()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static List<ShardLamp> AllLamps() => ShardLampStock.All.Select(e => e.Make()).ToList();

    // Exactly as the interior decorator and [Flip read it (InteriorDecorator.Turn, FlipCommandHandlers): the type's own
    // attribute, not inherited. (The first version inherited it and passed while the decorator could not turn a lamp;
    // the TazUO run caught it.)
    private static void Turn(Item item)
    {
        var attrs = (FlippableAttribute[])item.GetType().GetCustomAttributes(typeof(FlippableAttribute), false);
        Assert.NotEmpty(attrs);
        attrs[0].Flip(item);
    }

    // ---- 1 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ElevenLampsStartUnlitOnTheirFirstLook()
    {
        var lamps = AllLamps();
        try
        {
            Assert.Equal(11, lamps.Count);
            Assert.Equal(11, lamps.Select(l => l.GetType()).Distinct().Count());
            foreach (var lamp in lamps)
            {
                var looks = lamp.Looks;
                Assert.NotEmpty(looks);
                Assert.Equal(looks[0].Unlit, lamp.ItemID);
                Assert.False(lamp.Burning);
                Assert.Equal(TimeSpan.Zero, lamp.Duration);
                var ids = looks.SelectMany(l => new[] { l.Unlit, l.Lit }).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
                Assert.NotNull(lamp.DefaultName);
                Assert.Contains("Drafted", lamp.Credit);
            }

            foreach (var lamp in lamps.Where(l => l.Looks.Length > 1))
            {
                Assert.NotEmpty(lamp.GetType().GetCustomAttributes(typeof(FlippableAttribute), false));
            }

            Assert.Equal(2, lamps.OfType<DragonLantern>().Single().Looks.Length);
            Assert.Single(lamps.OfType<StainedGlassLamp>().Single().Looks);
            Assert.Equal(11, lamps.OfType<JackLantern>().Single().Looks.Length);
            Assert.Contains("Milva", lamps.OfType<TallModernLamp>().Single().Credit);
            Assert.Equal(new LampLook(0x194E, 0x194D), lamps.OfType<DragonBrazier>().Single().Looks[0]);
        }
        finally
        {
            lamps.ForEach(l => l.Delete());
        }
    }

    // ---- 2 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void DoubleClickLightsAndPutsOutWithinTwoTiles()
    {
        var pm = NewPlayer();
        var lamps = AllLamps();
        try
        {
            foreach (var lamp in lamps)
            {
                lamp.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y, Spot.Z), Map.Trammel);
                lamp.OnDoubleClick(pm);
                Assert.True(lamp.Burning, lamp.GetType().Name);
                Assert.Equal(lamp.Looks[0].Lit, lamp.ItemID);
                Assert.Equal(TimeSpan.Zero, lamp.Duration); // no burn-out timer

                lamp.OnDoubleClick(pm);
                Assert.False(lamp.Burning);
                Assert.Equal(lamp.Looks[0].Unlit, lamp.ItemID);

                lamp.MoveToWorld(new Point3D(Spot.X + 3, Spot.Y, Spot.Z), Map.Trammel);
                lamp.OnDoubleClick(pm);
                Assert.False(lamp.Burning);
            }
        }
        finally
        {
            lamps.ForEach(l => l.Delete());
            pm.Delete();
        }
    }

    // ---- 3 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void FlippingTurnsTheLampAndKeepsItLit()
    {
        var pm = NewPlayer();
        var lantern = new DragonLantern();
        var glass = new StainedGlassLamp();
        var jack = new JackLantern();
        try
        {
            Turn(lantern);
            Assert.Equal(0x4C42, lantern.ItemID); // east, unlit
            lantern.MoveToWorld(Spot, Map.Trammel);
            lantern.OnDoubleClick(pm);
            Assert.Equal(0x4C43, lantern.ItemID); // east, lit
            Turn(lantern);
            Assert.Equal(0x4C41, lantern.ItemID); // south, still lit
            Assert.True(lantern.Burning);
            lantern.OnDoubleClick(pm);
            Assert.Equal(0x4C40, lantern.ItemID);

            glass.Flip();
            Assert.Equal(0x4C50, glass.ItemID);

            var seen = new List<int>();
            for (var i = 0; i < jack.Looks.Length; i++)
            {
                seen.Add(jack.ItemID);
                Turn(jack);
            }

            Assert.Equal(jack.Looks.Select(l => l.Unlit), seen);
            Assert.Equal(jack.Looks[0].Unlit, jack.ItemID); // back to the first carving
        }
        finally
        {
            lantern.Delete();
            glass.Delete();
            jack.Delete();
            pm.Delete();
        }
    }

    // ---- 4 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheStoresSellTheLamps()
    {
        var pm = NewPlayer();
        try
        {
            var ap = AchievementStock.Rows.Where(r => r.Key.StartsWith("ap.lamp.", StringComparison.Ordinal)).ToList();
            Assert.Equal(10, ap.Count);
            Assert.DoesNotContain(ap, r => r.Key == "ap.lamp." + ShardLampStock.HalloweenKey);
            foreach (var row in ap)
            {
                var items = row.Create!(pm);
                var lamp = Assert.IsAssignableFrom<ShardLamp>(Assert.Single(items));
                Assert.Equal(row.ItemID, lamp.Looks[0].Lit);
                lamp.Delete();
            }

            var halloween = FestivalStock.For(FestivalSeasons.Halloween).Single(r => r.Key.EndsWith(".jacklantern", StringComparison.Ordinal));
            var jack = Assert.IsType<JackLantern>(Assert.Single(halloween.Create!(pm)));
            jack.Delete();
            Assert.DoesNotContain(FestivalStock.For(FestivalSeasons.Winter), r => r.Key.EndsWith(".jacklantern", StringComparison.Ordinal));
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---- 5 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ALitTurnedLampSurvivesASave()
    {
        var pm = NewPlayer();
        var lamp = new BirdLamp();
        try
        {
            Turn(lamp);
            lamp.MoveToWorld(Spot, Map.Trammel);
            lamp.OnDoubleClick(pm);
            Assert.Equal(0x4C47, lamp.ItemID);
            var copy = ConsumablesVerification.RoundTrip(lamp, s => new BirdLamp(s));
            Assert.True(copy.Burning);
            Assert.Equal(0x4C47, copy.ItemID);
            Assert.Equal(1, copy.LookIndex);
            copy.Delete();
        }
        finally
        {
            lamp.Delete();
            pm.Delete();
        }
    }

    // ---- 6 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheWagonDesignHasAStairToThePorchInPlaceOfTheLadder()
    {
        var parts = FortuneTellerWagonDesign.Parts;
        Assert.Equal(207, parts.Length); // Olorin's 199, less the ladder and the 5 roof props (cc-P872), plus 14 stair parts
        Assert.DoesNotContain(parts, p => p.ItemID == 0x3DB4);
        Assert.Equal("Olorin", FortuneTellerWagonDesign.Credit);
        Assert.Contains(parts, p => p.Name == "a crystal ball");
        Assert.Contains(parts, p => p.Name == "Grizelda The Fortune Teller");

        // The porch's two open deck tiles, 3 high at z 18: a top of 21.
        foreach (var x in new[] { 0, 1 })
        {
            Assert.Contains(parts, p => p.ItemID == 0x3EB0 && p.X == x && p.Y == 5 && p.Z == 18);
            // North steps (0x0722, 5 high), one a tile from the ground up: 0, 5, 10, 15, the last top 20, one under the deck.
            var steps = parts.Where(p => p.ItemID == 0x0722 && p.X == x).OrderByDescending(p => p.Y).ToList();
            Assert.Equal(new[] { 9, 8, 7, 6 }, steps.Select(p => p.Y));
            Assert.Equal(new[] { 0, 5, 10, 15 }, steps.Select(p => p.Z));
            // Blocks under the two upper steps, so no side hangs in the air.
            Assert.Contains(parts, p => p.ItemID == 0x0721 && p.X == x && p.Y == 7 && p.Z == 0);
            Assert.Contains(parts, p => p.ItemID == 0x0721 && p.X == x && p.Y == 6 && p.Z == 5);
        }

        // Nothing else stands on the stair's tiles or the tile a player climbs from.
        Assert.DoesNotContain(parts, p => p.Y >= 6 && p.ItemID is not (0x0722 or 0x0721));

        // cc-P872 (the build review P868; building guide 1.1, 2.3): nothing on the roof. From the roof's lowest course
        // (z 38) up there is only roofing (bark 0x26F8-0x26FA, shingles 0x2C35) and the gable ends (0x0443, 0x299B):
        // the author's rune glyphs and vent are out, and the black cat sits on the porch's potion kegs, not the roof.
        Assert.All(parts.Where(p => p.Z >= 38), p => Assert.True(
            p.ItemID is 0x26F8 or 0x26F9 or 0x26FA or 0x2C35 or 0x0443 or 0x299B, $"0x{p.ItemID:X4} at {p.X},{p.Y},{p.Z} on the roof"));
        Assert.DoesNotContain(parts, p => p.ItemID is 0x4862 or 0x4856 or 0x4844 or 0x2419);
        var cat = Assert.Single(parts, p => p.ItemID == 0x4689);
        Assert.Equal((-1, 4, 31), (cat.X, cat.Y, cat.Z));
        Assert.Contains(parts, p => p.ItemID == 0x9E36 && p.X == -1 && p.Y == 4 && p.Z == 26); // the top keg under it
        Assert.DoesNotContain(parts, p => p.Y >= 4 && p.X == 0 && p.Z > 21 && p.ItemID == 0x4689); // not on the walk
    }

    // ---- 7 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheWagonLaysEveryPartWithItsHueLightAndName()
    {
        var wagon = new FortuneTellerWagon();
        try
        {
            wagon.MoveToWorld(Spot, Map.Trammel);
            var parts = FortuneTellerWagonDesign.Parts;
            Assert.Equal(parts.Length, wagon.Components.Count);
            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                var c = wagon.Components[i];
                Assert.IsType<FortuneTellerWagonComponent>(c);
                Assert.Equal(p.ItemID, c.ItemID);
                Assert.Equal(new Point3D(p.X, p.Y, p.Z), c.Offset);
                Assert.Equal(new Point3D(Spot.X + p.X, Spot.Y + p.Y, Spot.Z + p.Z), c.Location);
                Assert.Equal(p.Hue, c.Hue);
                if (p.Light is { } light)
                {
                    Assert.Equal(light, c.Light);
                }

                if (p.Name != null)
                {
                    Assert.Equal(p.Name, c.Name);
                }
            }

            Assert.False(wagon.ShareHue);
            Assert.False(wagon.Movable);
        }
        finally
        {
            wagon.Delete();
        }
    }

    // ---- 8 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheWagonComesAndGoesWithTheStall()
    {
        var saved = FestivalMarket.WagonAt;
        try
        {
            FestivalMarket.Down();
            FestivalSeasons.Forced = FestivalSeasons.Winter;
            FestivalMarket.WagonAt = new Point3D(3647, 2632, 0);

            FestivalMarket.Up();
            var wagon = FestivalMarket.Wagon;
            Assert.NotNull(wagon);
            Assert.Equal(new Point3D(3647, 2632, 0), wagon.Location);
            Assert.Contains(wagon.Serial, FestivalMarket.Placed());
            Assert.Empty(FestivalMarket.TopUpWagon()); // already there
            var parts = wagon.Components.ToList();

            FestivalMarket.Down();
            Assert.True(wagon.Deleted);
            Assert.All(parts, c => Assert.True(c.Deleted));
            Assert.Empty(FestivalMarket.TopUpWagon()); // the stall is down: nothing

            // A world whose stall went up before the wagon existed.
            FestivalMarket.WagonAt = null;
            FestivalMarket.Up();
            Assert.Null(FestivalMarket.Wagon);
            Assert.Empty(FestivalMarket.TopUpWagon()); // set off: nothing
            FestivalMarket.WagonAt = new Point3D(3647, 2632, 0);
            Assert.Single(FestivalMarket.TopUpWagon());
            Assert.NotNull(FestivalMarket.Wagon);
            Assert.Empty(FestivalMarket.TopUpWagon()); // once
            Assert.Single(FestivalMarket.Placed().Select(s => World.FindItem(s)).OfType<FortuneTellerWagon>());
            Assert.Equal(FestivalMarket.Pieces.Length + 2, FestivalMarket.Placed().Count);
        }
        finally
        {
            FestivalMarket.Down();
            FestivalSeasons.Forced = null;
            FestivalMarket.WagonAt = saved;
        }
    }
}
