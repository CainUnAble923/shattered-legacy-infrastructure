// Regression tests for the S2 Fountain of Fortune reward loop.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run by docker/uo/run-tests.sh. See notes/s4-test-route.md.
//
// What a MODERNUO_COMMIT bump has to re-run, and why:
//
//   1. server/patches/PlayerMobile-fountain-luck-bonus.patch is the only thing carrying the
//      fountain's luck reward to the player. If ModernUO rewrites PlayerMobile.Luck the patch
//      stops applying and the build fails loudly - but if ModernUO instead stops routing loot
//      through Mobile.Luck, the patch still applies and the reward silently stops mattering.
//      FountainLuckBonusReachesPlayerMobileLuck is the only thing that would notice.
//
//   2. FountainResurrectGump subclasses ResurrectGump because ModernUO's version takes no
//      resurrect callback. A signature change upstream breaks the build; a behaviour change in
//      OnResponse does not, and the res cooldown would quietly stop being set.
//
//   3. LuckyCoin.OnDoubleClick was removed once already (Q-010) and restored by S2. The coin is
//      dropped by twelve Mobiles/Normal creatures, so the interaction is worth pinning.
//
// See shard-migration/notes/s2-fountain.md.

using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class FountainOfFortuneVerification
{
    private readonly ITestOutputHelper _out;

    public FountainOfFortuneVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayerWithCoin(out LuckyCoin coin, int amount = 2)
    {
        var m = new PlayerMobile();
        m.AddItem(new Backpack());

        coin = new LuckyCoin(amount);
        m.Backpack.DropItem(coin);

        return m;
    }

    [Fact]
    public void FountainLuckBonusReachesPlayerMobileLuck()
    {
        var control = new PlayerMobile();
        _out.WriteLine($"control: GetLuckBonus={FountainOfFortune.GetLuckBonus(control)} Luck={control.Luck}");

        // A player who has not wished carries no bonus, so a non-zero reading below is the
        // fountain's and not an artefact of the patch adding a constant to everyone.
        Assert.Equal(0, FountainOfFortune.GetLuckBonus(control));
        Assert.Equal(0, control.Luck);
        control.Delete();

        var fountain = new FountainOfFortune();

        // OnTarget rolls an 80% buff branch and then one of four buffs, so the luck branch is
        // about 1 in 6 per wish. The reward cooldown is per player, so each attempt needs a fresh
        // one. 400 attempts puts a false failure at roughly 1e-30.
        PlayerMobile lucky = null;
        var attempts = 0;

        while (attempts < 400 && lucky == null)
        {
            attempts++;

            var m = NewPlayerWithCoin(out var coin);
            fountain.OnTarget(m, coin);

            if (FountainOfFortune.GetLuckBonus(m) > 0)
            {
                lucky = m;
            }
            else
            {
                m.Delete();
            }
        }

        Assert.True(lucky != null, $"no wish rolled the luck branch in {attempts} attempts");

        _out.WriteLine($"lucky after {attempts} attempts: GetLuckBonus=" +
                       $"{FountainOfFortune.GetLuckBonus(lucky)} PlayerMobile.Luck={lucky.Luck}");

        Assert.Equal(400, FountainOfFortune.GetLuckBonus(lucky));

        // This is the assertion the patch exists for. Without it lucky.Luck reads 0 and the
        // "Your luck just improved!" message is a lie.
        Assert.Equal(400, lucky.Luck);

        lucky.Delete();
        fountain.Delete();
    }

    [Fact]
    public void AWishConsumesOneCoinAndSetsTheDailyCooldown()
    {
        var fountain = new FountainOfFortune();
        var m = NewPlayerWithCoin(out var coin, 2);

        Assert.False(fountain.IsCoolingDown(m));

        fountain.OnTarget(m, coin);

        _out.WriteLine($"after one wish: coin.Amount={coin.Amount} deleted={coin.Deleted} " +
                       $"cooling={fountain.IsCoolingDown(m)}");

        Assert.Equal(1, coin.Amount);
        Assert.False(coin.Deleted);
        Assert.True(fountain.IsCoolingDown(m));

        // The second wish is refused, so the coin is not consumed again.
        fountain.OnTarget(m, coin);

        _out.WriteLine($"after second wish: coin.Amount={coin.Amount} deleted={coin.Deleted}");

        Assert.Equal(1, coin.Amount);
        Assert.False(coin.Deleted);

        m.Delete();
        fountain.Delete();
    }

    [Fact]
    public void TheLastCoinIsDeletedRatherThanLeftAtZero()
    {
        var fountain = new FountainOfFortune();
        var m = NewPlayerWithCoin(out var coin, 1);

        fountain.OnTarget(m, coin);

        _out.WriteLine($"single coin: amount={coin.Amount} deleted={coin.Deleted}");

        Assert.True(coin.Deleted);

        m.Delete();
        fountain.Delete();
    }

    [Fact]
    public void DoubleClickingACoinInTheBackpackAsksForATarget()
    {
        // The Q-010 restoration. Ported without OnDoubleClick the coin was inert, and nothing in
        // a build would have told us.
        var m = NewPlayerWithCoin(out var coin);

        Assert.Null(m.Target);

        coin.OnDoubleClick(m);

        _out.WriteLine($"in backpack: target={m.Target?.GetType().Name ?? "null"}");
        Assert.NotNull(m.Target);

        m.Target = null;

        // Out of the backpack it does nothing, which is the ServUO guard.
        coin.MoveToWorld(m.Location, m.Map);
        coin.OnDoubleClick(m);

        _out.WriteLine($"on the ground: target={m.Target?.GetType().Name ?? "null"}");
        Assert.Null(m.Target);

        coin.Delete();
        m.Delete();
    }

    [Fact]
    public void GemologistsSatchelCarriesGemsAndImbuingIngredients()
    {
        // The satchel is the reward branch that forced the 35-item ingredient port (Q-020). If
        // its ingredient table is ever trimmed back, this catches it.
        var satchel = new GemologistsSatchel();

        var total = satchel.Items.Count;
        _out.WriteLine($"satchel: {total} stacks, hue={satchel.Hue}");

        // Nine gems always, plus five ingredient rolls that may stack onto each other or onto a
        // gem, so the count is between 9 and 14.
        Assert.True(total >= 9 && total <= 14, $"expected 9-14 stacks, got {total}");
        Assert.Equal(1177, satchel.Hue);

        satchel.Delete();
    }
}
