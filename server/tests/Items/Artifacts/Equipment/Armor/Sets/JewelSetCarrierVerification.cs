// Regression test for the S10 jewellery set carrier, BaseSetJewel.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// The carrier is under test, not any content: the real Luck set (Etoile Bleue, Lune Rouge, Novo
// Bleue, Soleil Rouge) is CC9 batch 4's to port, so the pieces here are test-local subclasses.
// See WeaponSetCarrierVerification.cs for what a bump has to keep working, and
// shard-migration/notes/s10-carriers.md.

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
public class JewelSetCarrierVerification
{
    private readonly ITestOutputHelper _out;

    public JewelSetCarrierVerification(ITestOutputHelper output) => _out = output;

    // Etoile Bleue's values, on a ring and a bracelet, plus a test-only cold eater. Etoile Bleue
    // also sets Hue = 1165 so the swap on completion is invisible; these leave Hue at 0 so the
    // swap can be seen.
    private abstract class LuckTestJewel : BaseSetJewel
    {
        protected LuckTestJewel(int itemID, Layer layer) : base(itemID, layer)
        {
            Attributes.Luck = 150;
            Attributes.CastSpeed = 1;
            Attributes.CastRecovery = 1;

            SetHue = 1165;
            SetAttributes.Luck = 100;
            SetAttributes.RegenHits = 2;
            SetAttributes.RegenMana = 2;
            SetAttributes.CastSpeed = 1;
            SetAttributes.CastRecovery = 4;

            AbsorptionAttributes.EaterCold = 3;
        }

        public override SetItem SetID => SetItem.Luck;
        public override int Pieces => 2;
    }

    private sealed class LuckTestRing : LuckTestJewel
    {
        public LuckTestRing() : base(0x108A, Layer.Ring)
        {
        }
    }

    private sealed class LuckTestBracelet : LuckTestJewel
    {
        public LuckTestBracelet() : base(0x1086, Layer.Bracelet)
        {
        }
    }

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    [Fact]
    public void ACompletedJewelSetReachesTheMobile()
    {
        var m = new PlayerMobile();

        var ring = new LuckTestRing();
        var bracelet = new LuckTestBracelet();

        Wear(m, ring, Layer.Ring);
        _out.WriteLine($"1 piece : SetID={ring.SetID} IsSetItem={ring.IsSetItem} SetEquipped={ring.SetEquipped} " +
                       $"Luck={AosAttributes.GetValue(m, AosAttribute.Luck)}");

        Assert.Equal(SetItem.Luck, ring.SetID);
        Assert.True(ring.IsSetItem);
        Assert.False(ring.SetEquipped);
        Assert.Equal(150, AosAttributes.GetValue(m, AosAttribute.Luck));    // the piece's own
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.RegenHits)); // not the set's
        Assert.Equal(0, ring.Hue);

        Wear(m, bracelet, Layer.Bracelet);

        _out.WriteLine($"2 pieces: ring.SetEquipped={ring.SetEquipped} bracelet.SetEquipped={bracelet.SetEquipped} " +
                       $"LastEquipped ring={ring.LastEquipped} bracelet={bracelet.LastEquipped}");

        Assert.True(ring.SetEquipped);
        Assert.True(bracelet.SetEquipped);
        Assert.True(bracelet.LastEquipped);
        Assert.False(ring.LastEquipped);

        var luck = AosAttributes.GetValue(m, AosAttribute.Luck);
        var regenHits = AosAttributes.GetValue(m, AosAttribute.RegenHits);
        var castRecovery = AosAttributes.GetValue(m, AosAttribute.CastRecovery);
        _out.WriteLine($"2 pieces: Luck={luck} RegenHits={regenHits} CastRecovery={castRecovery} " +
                       $"ring.Hue={ring.Hue} ring.SetHue={ring.SetHue}");

        Assert.Equal(2 * 150 + 100, luck);      // per-piece stacks, set bonus once
        Assert.Equal(2, regenHits);             // set only
        Assert.Equal(2 * 1 + 4, castRecovery);

        Assert.Equal(1165, ring.Hue);           // set hue swapped in
        Assert.Equal(0, ring.SetHue);

        Assert.Equal(6, SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterCold));

        // Jewellery has no set resistance bonus; the carrier reports the piece's own resistance
        // through the resistance hook, which for a plain jewel is zero.
        m.UpdateResistances();
        Assert.Equal(0, ring.SetResistBonus(ResistanceType.Physical));
        Assert.Equal(0, m.PhysicalResistance);

        m.RemoveItem(bracelet);
        _out.WriteLine($"1 piece : ring.SetEquipped={ring.SetEquipped} Luck={AosAttributes.GetValue(m, AosAttribute.Luck)} " +
                       $"ring.Hue={ring.Hue}");

        Assert.False(ring.SetEquipped);
        Assert.False(bracelet.LastEquipped);
        Assert.Equal(150, AosAttributes.GetValue(m, AosAttribute.Luck));
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.RegenHits));
        Assert.Equal(0, ring.Hue);
        Assert.Equal(1165, ring.SetHue);

        m.Delete();
    }

    [Fact]
    public void StockJewelIsUnaffected()
    {
        var ring = new GoldRing();

        _out.WriteLine($"stock GoldRing: ISetItem={ring is ISetItem} IAbsorptionItem={ring is IAbsorptionItem}");

        Assert.False(ring is ISetItem);
        Assert.False(ring is IAbsorptionItem);

        ring.Delete();
    }

    [Fact]
    public void JewelSetStateSurvivesASerializationRoundTrip()
    {
        var original = new LuckTestRing();
        original.SetEquipped = true;
        original.LastEquipped = true;
        original.SetFireBonus = 6;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new LuckTestRing();
        copy.SetHue = 0;
        copy.AbsorptionAttributes.EaterCold = 0;
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetFireBonus={copy.SetFireBonus} " +
                       $"SetAttributes.Luck={copy.SetAttributes.Luck} " +
                       $"SetAttributes.CastRecovery={copy.SetAttributes.CastRecovery} " +
                       $"EaterCold={copy.AbsorptionAttributes.EaterCold} Attributes.Luck={copy.Attributes.Luck}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(1165, copy.SetHue);
        Assert.Equal(6, copy.SetFireBonus);
        Assert.Equal(100, copy.SetAttributes.Luck);
        Assert.Equal(4, copy.SetAttributes.CastRecovery);
        Assert.Equal(3, copy.AbsorptionAttributes.EaterCold);
        Assert.Equal(150, copy.Attributes.Luck);

        original.Delete();
        copy.Delete();
    }
}
