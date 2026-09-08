// Regression test for the S10 clothing set carrier, BaseSetClothing.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// The carrier is under test, not any content: the real Virtue pieces (Cloak of Humility,
// Sollerets of Sacrifice) are CC9 batch 4's to port, so the pieces here are test-local subclasses.
// Clothing is the one non-armour kind whose ServUO set state includes the resistance arithmetic,
// so this test also drives Mobile-set-item-resistance-hook.patch through a non-BaseArmor item.
// See shard-migration/notes/s10-carriers.md.

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
public class ClothingSetCarrierVerification
{
    private readonly ITestOutputHelper _out;

    public ClothingSetCarrierVerification(ITestOutputHelper output) => _out = output;

    // Cloak of Humility's set values on a two-piece Virtue set. Virtue is a per-piece set in
    // SetHelper.ResistsBonusPerPiece, so each worn piece contributes its own resistance plus the
    // set bonus. A base resistance is given so there is something to add the bonus to; a set
    // attribute and a test-only poison eater exercise the other two carriers.
    private abstract class VirtueTestClothing : BaseSetClothing
    {
        protected VirtueTestClothing(int itemID, Layer layer) : base(itemID, layer)
        {
            SetHue = 0;
            Hue = 0x226;

            SetSelfRepair = 5;
            SetPhysicalBonus = 5;
            SetFireBonus = 5;
            SetColdBonus = 5;
            SetPoisonBonus = 5;
            SetEnergyBonus = 5;

            SetAttributes.BonusMana = 10;

            AbsorptionAttributes.EaterPoison = 4;
        }

        public override SetItem SetID => SetItem.Virtue;
        public override int Pieces => 2;

        public override int BasePhysicalResistance => 2;
        public override int BaseColdResistance => 3;
    }

    private sealed class VirtueTestCloak : VirtueTestClothing
    {
        public VirtueTestCloak() : base(0x2B04, Layer.Cloak)
        {
        }
    }

    private sealed class VirtueTestBoots : VirtueTestClothing
    {
        public VirtueTestBoots() : base(0x2B03, Layer.Shoes)
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
    public void ACompletedClothingSetReachesTheMobile()
    {
        var m = new PlayerMobile();

        var cloak = new VirtueTestCloak();
        var boots = new VirtueTestBoots();

        Wear(m, cloak, Layer.Cloak);
        m.UpdateResistances();
        _out.WriteLine($"1 piece : SetID={cloak.SetID} IsSetItem={cloak.IsSetItem} SetEquipped={cloak.SetEquipped} " +
                       $"phys={m.PhysicalResistance} cold={m.ColdResistance} MixedSet={cloak.MixedSet}");

        Assert.Equal(SetItem.Virtue, cloak.SetID);
        Assert.True(cloak.IsSetItem);
        Assert.False(cloak.MixedSet);
        Assert.False(cloak.SetEquipped);
        Assert.Equal(2, m.PhysicalResistance); // the piece's own, no bonus yet
        Assert.Equal(3, m.ColdResistance);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusMana));

        Wear(m, boots, Layer.Shoes);
        m.UpdateResistances();

        _out.WriteLine($"2 pieces: cloak.SetEquipped={cloak.SetEquipped} boots.SetEquipped={boots.SetEquipped} " +
                       $"LastEquipped cloak={cloak.LastEquipped} boots={boots.LastEquipped} " +
                       $"phys={m.PhysicalResistance} cold={m.ColdResistance} " +
                       $"BonusMana={AosAttributes.GetValue(m, AosAttribute.BonusMana)}");

        Assert.True(cloak.SetEquipped);
        Assert.True(boots.SetEquipped);
        Assert.True(boots.LastEquipped);
        Assert.False(cloak.LastEquipped);

        // Per-piece: each piece is (own + bonus), through PlayerMobile.GetItemResistance.
        Assert.True(SetHelper.ResistsBonusPerPiece(cloak));
        Assert.Equal(2 + 5, cloak.SetResistBonus(ResistanceType.Physical));
        Assert.Equal(3 + 5, cloak.SetResistBonus(ResistanceType.Cold));
        Assert.Equal(2 * (2 + 5), m.PhysicalResistance);
        Assert.Equal(2 * (3 + 5), m.ColdResistance);

        Assert.Equal(10, AosAttributes.GetValue(m, AosAttribute.BonusMana));
        Assert.Equal(8, SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterPoison));

        // SetHue 0 swapped with Hue 0x226: the set hue for Virtue is "keep the item's own".
        Assert.Equal(0, cloak.Hue);
        Assert.Equal(0x226, cloak.SetHue);

        m.RemoveItem(boots);
        m.UpdateResistances();
        _out.WriteLine($"1 piece : cloak.SetEquipped={cloak.SetEquipped} phys={m.PhysicalResistance} " +
                       $"BonusMana={AosAttributes.GetValue(m, AosAttribute.BonusMana)} Hue={cloak.Hue}");

        Assert.False(cloak.SetEquipped);
        Assert.False(boots.LastEquipped);
        Assert.Equal(2, m.PhysicalResistance);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusMana));
        Assert.Equal(0x226, cloak.Hue);

        m.Delete();
    }

    [Fact]
    public void StockClothingIsUnaffected()
    {
        var m = new PlayerMobile();
        var cloak = Wear(m, new Cloak(), Layer.Cloak);

        m.UpdateResistances();
        _out.WriteLine($"stock Cloak: ISetItem={cloak is ISetItem} IAbsorptionItem={cloak is IAbsorptionItem} " +
                       $"phys={m.PhysicalResistance}");

        Assert.False(cloak is ISetItem);
        Assert.False(cloak is IAbsorptionItem);
        Assert.Equal(0, m.PhysicalResistance);

        m.Delete();
    }

    [Fact]
    public void ClothingSetStateSurvivesASerializationRoundTrip()
    {
        var original = new VirtueTestCloak();
        original.SetEquipped = true;
        original.LastEquipped = true;
        original.SetHue = 1234;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new VirtueTestCloak();
        copy.SetSelfRepair = 0;
        copy.SetPhysicalBonus = 0;
        copy.AbsorptionAttributes.EaterPoison = 0;
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetSelfRepair={copy.SetSelfRepair} " +
                       $"SetPhysicalBonus={copy.SetPhysicalBonus} SetEnergyBonus={copy.SetEnergyBonus} " +
                       $"SetAttributes.BonusMana={copy.SetAttributes.BonusMana} " +
                       $"EaterPoison={copy.AbsorptionAttributes.EaterPoison}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(1234, copy.SetHue);
        Assert.Equal(5, copy.SetSelfRepair);
        Assert.Equal(5, copy.SetPhysicalBonus);
        Assert.Equal(5, copy.SetEnergyBonus);
        Assert.Equal(10, copy.SetAttributes.BonusMana);
        Assert.Equal(4, copy.AbsorptionAttributes.EaterPoison);

        original.Delete();
        copy.Delete();
    }
}
