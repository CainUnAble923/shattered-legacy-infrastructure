// cc-P761 (Chase 11-01: "stranger at beginning needs a hood/cowl"): the vision's Stranger is a hooded, faceless figure
// in what a player could wear. Notes: shard-migration notes/cc-P761-stranger-hood.md.
//
//   1. No name: nothing for the client to show over it or on a click.
//   2. A deep cowl on the head (the Assassin's Cowl art, helm layer), a robe and a cloak, all in the one Stranger hue,
//      and the longsword in hand; a human body, not a monster.
//   3. The hue is one value: a different hue dresses every cloth piece in it, and the sword keeps its own.

using System.Linq;
using Server;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialVisionStrangerVerification
{
    [Fact]
    public void TheStrangerHasNoName()
    {
        var f = TutorialVisionFigure.Stranger();
        try
        {
            Assert.True(string.IsNullOrEmpty(f.Name));
        }
        finally { f.Delete(); }
    }

    [Fact]
    public void TheStrangerWearsACowlRobeAndCloakInOneHueAndHoldsTheSword()
    {
        var f = TutorialVisionFigure.Stranger();
        try
        {
            Assert.Equal(0x190, f.Body.BodyID); // a human, as a player is

            var cowl = f.FindItemOnLayer(Layer.Helm);
            Assert.NotNull(cowl);
            Assert.Equal(TutorialVisionScene.CowlId, cowl.ItemID);
            Assert.Equal(0xA410, cowl.ItemID); // AssassinsCowl's art

            var robe = f.FindItemOnLayer(Layer.OuterTorso);
            Assert.NotNull(robe);
            Assert.Equal(TutorialVisionScene.RobeId, robe.ItemID);

            var cloak = f.FindItemOnLayer(Layer.Cloak);
            Assert.NotNull(cloak);
            Assert.Equal(TutorialVisionScene.CloakId, cloak.ItemID);

            foreach (var cloth in new[] { cowl, robe, cloak })
            {
                Assert.Equal(TutorialVisionScene.StrangerHue, cloth.Hue);
            }

            var sword = f.FindItemOnLayer(Layer.OneHanded);
            Assert.NotNull(sword);
            Assert.Equal(TutorialVisionScene.SwordId, sword.ItemID);
        }
        finally { f.Delete(); }
    }

    [Fact]
    public void OneHueDressesEveryClothPiece()
    {
        const int other = 0x0481;
        var f = TutorialVisionFigure.Stranger(other);
        try
        {
            var cloth = new[] { Layer.Helm, Layer.OuterTorso, Layer.Cloak }.Select(l => f.FindItemOnLayer(l)).ToList();
            Assert.All(cloth, c => Assert.Equal(other, c!.Hue));
            Assert.NotEqual(other, f.FindItemOnLayer(Layer.OneHanded)!.Hue);
        }
        finally { f.Delete(); }
    }
}
