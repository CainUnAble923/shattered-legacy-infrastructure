// Regression tests for the S6 Stygian Abyss damage-eater subsystem.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// These are what a MODERNUO_COMMIT bump has to re-run. The failure mode this file exists for
// is specific and silent: AOS-damage-eater-hook.patch can still apply while upstream has
// rerouted damage around AOS.Damage, or while the property no longer reaches the mobile. The
// build stays green, every tooltip still says "Cold Eater 8%", and nothing eats anything.
// See shard-migration/notes/s6-absorption.md section 3.

using System;
using System.Collections.Concurrent;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AbsorptionAttributesVerification
{
    private readonly ITestOutputHelper _out;

    public AbsorptionAttributesVerification(ITestOutputHelper output) => _out = output;

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    // The four pieces that make a complete Aloron set, each carrying EaterCold = 2.
    private static PlayerMobile WearCompleteAloronSet()
    {
        var m = new PlayerMobile();

        // A default PlayerMobile has RawStr 0, so HitsMax is small enough that setting Hits below
        // it clamps to 0 and the mobile stops being a valid heal target. Give it a real body.
        m.RawStr = 200;

        Wear(m, new AloronsTunic(), Layer.InnerTorso);
        Wear(m, new AloronsHelm(), Layer.Helm);
        Wear(m, new AloronsLegs(), Layer.Pants);
        Wear(m, new AloronsGorget(), Layer.Neck);

        return m;
    }

    // Time is advanced by ShardTestClock, the route's one way of doing it. This file used to
    // turn the timer wheel directly, which runs every callback against a Core.Now that has not
    // moved; that is Q-029, and it is why the first version of the damage test below reported
    // healed=40 for an expected 3. See server/tests/Route/ShardTestClock.cs, and
    // notes/s8-test-route.md section 3. apply-patches.sh now fails the build for a test that
    // drives the wheel itself.

    [Fact]
    public void AloronPiecesCarryTheirColdEater()
    {
        var tunic = new AloronsTunic();

        _out.WriteLine($"tunic: EaterCold={tunic.AbsorptionAttributes.EaterCold} " +
                       $"EaterFire={tunic.AbsorptionAttributes.EaterFire}");

        // The whole of A5: ServUO Alorons*.cs:23 sets this on all eight pieces and S1 dropped it.
        Assert.Equal(2, tunic.AbsorptionAttributes.EaterCold);
        Assert.Equal(0, tunic.AbsorptionAttributes.EaterFire);

        // The carrier is what the aggregation walk looks for.
        Assert.IsAssignableFrom<IAbsorptionItem>(tunic);
    }

    [Fact]
    public void AbsorptionAggregatesAcrossEquippedItems()
    {
        var m = WearCompleteAloronSet();

        var cold = SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterCold);
        var fire = SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterFire);

        _out.WriteLine($"4 Aloron pieces: EaterCold={cold} EaterFire={fire}");

        // 4 pieces x 2% each. Per-piece, not a set bonus: ServUO puts EaterCold on the piece.
        Assert.Equal(8, cold);
        Assert.Equal(0, fire);

        Assert.True(DamageEaterContext.HasValue(m));
        Assert.Equal(8, DamageEaterContext.GetValue(DamageType.Cold, m));
        Assert.Equal(0, DamageEaterContext.GetValue(DamageType.Fire, m));
    }

    [Fact]
    public void StockArmourEatsNothing()
    {
        var m = new PlayerMobile();
        var chest = Wear(m, new LeatherChest(), Layer.InnerTorso);

        _out.WriteLine($"LeatherChest: IAbsorptionItem={chest is IAbsorptionItem} " +
                       $"EaterCold={SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterCold)}");

        // Stock ModernUO items are not IAbsorptionItem and nothing upstream changes to make them
        // one. ModernUO's loot generator has no absorption property to roll, so nothing is lost.
        Assert.False(chest is IAbsorptionItem);
        Assert.Equal(0, SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterCold));
        Assert.False(DamageEaterContext.HasValue(m));
    }

    [Fact]
    public void ACompletedAloronSetEatsColdDamage()
    {
        var m = WearCompleteAloronSet();

        m.Map = Map.Felucca;
        m.Location = new Point3D(1000, 1000, 0);

        // Leave headroom so the heal is not clipped by HitsMax.
        m.Hits = m.HitsMax - 60;
        var before = m.Hits;

        Assert.True(before > 0, $"the mobile has no room to be healed: Hits={before} HitsMax={m.HitsMax}");

        // Arm before anything is scheduled. Setting Hits below HitsMax above started
        // Mobile.HitsTimer (Projects/Server/Mobiles/Mobile.cs:2029) and arming drops it, which is
        // what keeps the arithmetic below deterministic. The old Timer.Init(0) here did the same
        // thing by accident; the route now does it on purpose and says so.
        ShardTestClock.Arm();

        // 100 damage, all cold. The eater does NOT reduce this: the mobile takes it in full and a
        // share comes back three seconds later. 100 x (100/100) x 0.08 = 8, under the 30% cap.
        DamageEaterContext.CheckDamage(m, 100, 0, 0, 100, 0, 0, 0);

        _out.WriteLine($"t+0: hits={m.Hits} (damage itself is applied by AOS.Damage, not here)");
        Assert.Equal(before, m.Hits);

        ShardTestClock.AdvanceSeconds(4);

        _out.WriteLine($"after 4s: hits={m.Hits} healed={m.Hits - before}");

        Assert.Equal(before + 8, m.Hits);
    }

    // This is the patch under test, and the only test in this file that fails if
    // AOS-damage-eater-hook.patch is dropped or stops being reached.
    //
    // It asserts on the context rather than on the victim's hit points, and that is deliberate.
    // Driving AOS.Damage starts Mobile's own hit-regeneration timer, which then ticks for the
    // whole of the four seconds the delayed heal needs — it restored the full 40 damage in the
    // first version of this test, swamping the eater's 3. S8's clock fixes the frozen-Core.Now
    // half of that (Q-029) but not this half: regeneration during the wait is real behaviour, and
    // it would still confound an assertion on hit points here. The heal arithmetic is
    // proved deterministically by ACompletedAloronSetEatsColdDamage above, which never calls
    // Mobile.Damage and so never starts that timer. Between the two, the chain from AOS.Damage to
    // a heal is covered end to end with no timing dependence in either half.
    [Fact]
    public void ColdDamageThroughAosDamageReachesTheEater()
    {
        var m = WearCompleteAloronSet();

        m.Map = Map.Felucca;
        m.Location = new Point3D(1000, 1000, 0);
        m.Hits = m.HitsMax;

        var before = m.Hits;
        var coldResist = m.ColdResistance;

        Assert.True(before > 60, $"the mobile cannot survive the hit: Hits={before} HitsMax={m.HitsMax}");
        Assert.False(PropertyEffect.IsUnderEffects(m, EffectsType.DamageEater));

        var dealt = AOS.Damage(m, null, 100, 0, 0, 100, 0, 0);

        _out.WriteLine($"eater: cold resist={coldResist} dealt={dealt} hits {before} -> {m.Hits} " +
                       $"context={PropertyEffect.IsUnderEffects(m, EffectsType.DamageEater)}");

        // The eater does not reduce damage, so the hit lands in full either way.
        Assert.Equal(before - dealt, m.Hits);

        // Nothing but AOS.Damage could have created this. If upstream reroutes damage around
        // AOS.Damage, or the patch stops applying where it applies today, this is what notices.
        Assert.True(
            PropertyEffect.IsUnderEffects(m, EffectsType.DamageEater),
            "AOS.Damage did not reach DamageEaterContext.CheckDamage — the damage hook is gone"
        );

        // Negative control: the same call against a victim with nothing to eat with creates no
        // context, so the assertion above is about the eater and not about AOS.Damage running.
        var bare = new PlayerMobile { RawStr = 200 };
        Wear(bare, new LeatherChest(), Layer.InnerTorso);
        bare.Map = Map.Felucca;
        bare.Location = new Point3D(1000, 1000, 0);
        bare.Hits = bare.HitsMax;

        AOS.Damage(bare, null, 100, 0, 0, 100, 0, 0);

        _out.WriteLine($"control: context={PropertyEffect.IsUnderEffects(bare, EffectsType.DamageEater)}");

        Assert.False(PropertyEffect.IsUnderEffects(bare, EffectsType.DamageEater));
    }

    [Fact]
    public void AbsorptionSurvivesASerializationRoundTrip()
    {
        var original = new AloronsTunic();
        original.AbsorptionAttributes.EaterFire = 5;
        original.AbsorptionAttributes.CastingFocus = 3;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new AloronsTunic(original.Serial);
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: EaterCold={copy.AbsorptionAttributes.EaterCold} " +
                       $"EaterFire={copy.AbsorptionAttributes.EaterFire} " +
                       $"CastingFocus={copy.AbsorptionAttributes.CastingFocus}");

        Assert.Equal(2, copy.AbsorptionAttributes.EaterCold);
        Assert.Equal(5, copy.AbsorptionAttributes.EaterFire);
        Assert.Equal(3, copy.AbsorptionAttributes.CastingFocus);

        original.Delete();
        copy.Delete();
    }
}
