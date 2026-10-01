// ThievesDenLookoutVerification.cs
//
// cc-P22, the F-9 follow-up: a Thieves' Den lookout on the stool by the New Haven fighting pit sends players to the
// guildmaster in the inn's back room. Notes in shard-migration notes/cc-P22-small-features-1.md.
//
// Facts:
//   1. The seeder run twice places one lookout, on the stool (3440,2558,56), facing east, not walking; a dry run
//      places none.
//   2. The lookout's arrow points at a Thieves' Den guildmaster that exists, and says so when the guildmaster is
//      missing (P15's rule: reported, never silently skipped).
//   3. Not a vendor, not a trainer, invulnerable; "guild", "thief", "thieves" and "join" are what he answers to.

using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ThievesDenLookoutVerification
{
    private static readonly Point3D Stool = new(3440, 2558, 56);

    private readonly ITestOutputHelper _out;

    public ThievesDenLookoutVerification(ITestOutputHelper output)
    {
        _out = output;
        ClusterFGuildSystem.EnsureRegistered();
    }

    private static List<ThievesDenLookout> LookoutsNearTheStool() =>
        World.Mobiles.Values.OfType<ThievesDenLookout>()
            .Where(m => !m.Deleted && m.Map == Map.Trammel && m.InRange(Stool, GuildLocation.SearchRange))
            .ToList();

    [Fact]
    public void TheSeederRunTwicePlacesOneLookoutOnTheStool()
    {
        var before = new HashSet<Mobile>(World.Mobiles.Values);

        try
        {
            Assert.Empty(LookoutsNearTheStool());

            var row = Assert.Single(GuildLocations.Lookouts);
            Assert.Equal(typeof(ThievesDenLookout), row.NpcType);
            Assert.Equal(Stool, row.Point);
            Assert.DoesNotContain(GuildLocations.All, l => l.NpcType == typeof(ThievesDenLookout));

            var dry = ClusterFGuildHallSeeder.Seed(dryRun: true, repair: false);
            Assert.Empty(LookoutsNearTheStool());
            Assert.Contains(dry, l => l.Contains("ThievesDenLookout") && l.Contains("would place"));

            var first = ClusterFGuildHallSeeder.Seed(dryRun: false, repair: false);
            var second = ClusterFGuildHallSeeder.Seed(dryRun: false, repair: false);
            _out.WriteLine(string.Join("\n", first));
            _out.WriteLine(string.Join("\n", second));

            var lookout = Assert.Single(LookoutsNearTheStool());
            Assert.Equal(Stool, lookout.Location);
            Assert.Equal(Direction.East, lookout.Direction & Direction.Mask);
            Assert.True(lookout.CantWalk);
            Assert.Equal(0, lookout.RangeHome);
            Assert.DoesNotContain(second, l => l.Contains("ThievesDenLookout"));
        }
        finally
        {
            foreach (var m in World.Mobiles.Values.ToList())
            {
                if (!before.Contains(m))
                {
                    m.Delete();
                }
            }
        }
    }

    [Fact]
    public void TheArrowPointsAtTheGuildmasterAndAMissingOneIsReported()
    {
        var post = GuildLocations.For(ThievesDenLookout.GuildKey).Single();
        var lookout = new ThievesDenLookout();
        lookout.MoveToWorld(Stool, Map.Trammel);

        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.MoveToWorld(new Point3D(Stool.X + 1, Stool.Y, 55), Map.Trammel);

        var guildmaster = new ThiefGuildmaster();
        guildmaster.MoveToWorld(post.Point, Map.Trammel);

        try
        {
            var told = lookout.SendToGuildmaster(pm);
            _out.WriteLine($"present: {told}");
            Assert.StartsWith("Follow the arrow", told);
            var arrow = Assert.IsType<GuildDirectionArrow>(pm.QuestArrow);
            Assert.Same(guildmaster, arrow.Target);
            Assert.Equal(post.Point, arrow.Point);

            pm.QuestArrow = null;
            guildmaster.Delete();

            told = lookout.SendToGuildmaster(pm);
            _out.WriteLine($"missing: {told}");
            Assert.Contains("is not at Bountiful Harvest Inn, back room", told);
            Assert.Null(pm.QuestArrow);
        }
        finally
        {
            pm.QuestArrow = null;
            guildmaster.Delete();
            lookout.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheLookoutIsNotAVendorOrATrainerAndAnswersToTheGuildWords()
    {
        var lookout = new ThievesDenLookout();

        try
        {
            Assert.IsNotAssignableFrom<BaseVendor>(lookout);
            Assert.False(lookout.CanTeach);
            Assert.True(lookout.IsInvulnerable);
            Assert.True(lookout.CantWalk);

            foreach (var said in new[] { "guild", "Where is the THIEVES den?", "I am a thief", "can I join" })
            {
                Assert.True(ThievesDenLookout.IsAboutTheGuild(said), said);
            }

            foreach (var said in new[] { "hello", "vendor buy", "", null })
            {
                Assert.False(ThievesDenLookout.IsAboutTheGuild(said), said ?? "(null)");
            }
        }
        finally
        {
            lookout.Delete();
        }
    }
}
