// Regression test for the Marksman set: the one set on this shard whose two pieces sit on two different
// mechanisms, Feathernock on the S10 quiver carrier and Swiftflight as a one-off ISetItem implementer on the
// stock Bow (Q-041: no carrier can sit above BaseRanged).
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// What has to keep working across a MODERNUO_COMMIT bump is that the two S1 hooks
// (AOS-set-attribute-aggregation.patch and Mobile-set-item-resistance-hook.patch) and SetHelper reach a set
// piece through ISetItem alone, with no carrier class in the chain, and that the generated serializer on a
// Bow subclass carries the set fields. If a bump ever adds a type test on the carriers, this is the test that
// goes red. Unlike the S10 carrier tests this one uses the real content pieces, because the point is the
// content shape, not a carrier. See shard-migration/notes/cc9-artifacts.md section 15.

using System;
using System.Collections.Concurrent;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MarksmanSetVerification
{
    private readonly ITestOutputHelper _out;

    public MarksmanSetVerification(ITestOutputHelper output) => _out = output;

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    [Fact]
    public void ABowImplementingISetItemCompletesTheSetWithTheQuiver()
    {
        var m = new PlayerMobile();
        m.RawDex = 50;

        var bow = new Swiftflight();
        var quiver = new Feathernock();

        Assert.True(bow is ISetItem);
        Assert.False(bow is BaseSetWeapon);
        Assert.Equal(SetItem.Marksman, bow.SetID);
        Assert.Equal(2, bow.Pieces);
        Assert.True(bow.IsSetItem);

        Wear(m, bow, Layer.TwoHanded);
        _out.WriteLine($"bow only: SetEquipped={bow.SetEquipped} WeaponDamage={AosAttributes.GetValue(m, AosAttribute.WeaponDamage)} " +
                       $"AttackChance={AosAttributes.GetValue(m, AosAttribute.AttackChance)}");

        Assert.False(bow.SetEquipped);
        Assert.Equal(40, AosAttributes.GetValue(m, AosAttribute.WeaponDamage)); // the bow's own
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.AttackChance));  // not the set's
        Assert.Equal(0, m.GetStatOffset(StatType.Dex));

        Wear(m, quiver, Layer.Cloak);
        _out.WriteLine($"both    : bow.SetEquipped={bow.SetEquipped} quiver.SetEquipped={quiver.SetEquipped} " +
                       $"LastEquipped bow={bow.LastEquipped} quiver={quiver.LastEquipped} " +
                       $"WeaponDamage={AosAttributes.GetValue(m, AosAttribute.WeaponDamage)} " +
                       $"AttackChance={AosAttributes.GetValue(m, AosAttribute.AttackChance)} " +
                       $"WeaponSpeed={AosAttributes.GetValue(m, AosAttribute.WeaponSpeed)} dex={m.GetStatOffset(StatType.Dex)}");

        Assert.True(bow.SetEquipped);
        Assert.True(quiver.SetEquipped);
        Assert.True(quiver.LastEquipped);   // the quiver completed the set, so its SetAttributes aggregate...
        Assert.False(bow.LastEquipped);
        Assert.Equal(15, AosAttributes.GetValue(m, AosAttribute.AttackChance));
        Assert.Equal(30, AosAttributes.GetValue(m, AosAttribute.WeaponSpeed));
        Assert.Equal(40 + 10 + 20, AosAttributes.GetValue(m, AosAttribute.WeaponDamage)); // bow 40 + quiver 10 + set 20, once
        Assert.Equal(8, m.GetStatOffset(StatType.Dex));
        Assert.Equal(0x594, bow.Hue);       // ...and the bow took the set hue through SetHelper
        Assert.Equal(0, bow.SetHue);

        // The other way round: break the set, re-wear so the bow is the last piece.
        m.RemoveItem(quiver);
        Assert.False(bow.SetEquipped);
        Assert.Equal(0x594, bow.SetHue);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.AttackChance));
        Assert.Equal(0, m.GetStatOffset(StatType.Dex));

        m.RemoveItem(bow);
        Wear(m, quiver, Layer.Cloak);
        Wear(m, bow, Layer.TwoHanded);
        _out.WriteLine($"re-worn : LastEquipped bow={bow.LastEquipped} quiver={quiver.LastEquipped} " +
                       $"AttackChance={AosAttributes.GetValue(m, AosAttribute.AttackChance)}");

        Assert.True(bow.LastEquipped);
        Assert.False(quiver.LastEquipped);
        Assert.Equal(15, AosAttributes.GetValue(m, AosAttribute.AttackChance));
        Assert.Equal(8, m.GetStatOffset(StatType.Dex));

        m.RemoveItem(bow);
        Assert.False(quiver.SetEquipped);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.AttackChance));

        m.Delete();
    }

    [Fact]
    public void SwiftflightSetStateSurvivesASerializationRoundTrip()
    {
        // The set fields live on Swiftflight itself, below Bow's and BaseRanged's generated serializers. This
        // proves the whole chain round-trips, including the inert SetSelfRepair (Q-038), which must keep its
        // value in the save so the day the OnHit hook lands nothing needs re-spawning.
        var original = new Swiftflight();
        original.SetEquipped = true;
        original.LastEquipped = true;
        original.SetPhysicalBonus = 4;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new Swiftflight();
        copy.SetHue = 0;               // make sure the values below come from the buffer
        copy.SetSelfRepair = 0;
        copy.SetAttributes.AttackChance = 0;
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetSelfRepair={copy.SetSelfRepair} SetPhysicalBonus={copy.SetPhysicalBonus} " +
                       $"SetAttributes.AttackChance={copy.SetAttributes.AttackChance} " +
                       $"Attributes.WeaponDamage={copy.Attributes.WeaponDamage} Layer={copy.Layer}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(0x594, copy.SetHue);
        Assert.Equal(3, copy.SetSelfRepair);
        Assert.Equal(4, copy.SetPhysicalBonus);
        Assert.Equal(15, copy.SetAttributes.AttackChance);
        Assert.Equal(8, copy.SetAttributes.BonusDex);
        Assert.Equal(40, copy.Attributes.WeaponDamage);
        Assert.Equal(Layer.TwoHanded, copy.Layer);

        original.Delete();
        copy.Delete();
    }
}
