// Regression test for the S10 shield set carrier, BaseSetShield.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// The carrier is under test, not any content: Maleki's Honor is CC9 batch 4's to port, so the
// shield here is a test-local subclass. Its partner is a test-local BaseSetArmor piece, for the
// reason given in QuiverSetCarrierVerification.cs. Juggernaut is NOT a per-piece set in
// SetHelper.ResistsBonusPerPiece, so this is also the test of the last-equipped resistance path
// on a carrier other than BaseSetArmor. See shard-migration/notes/s10-carriers.md.

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
public class ShieldSetCarrierVerification
{
    private readonly ITestOutputHelper _out;

    public ShieldSetCarrierVerification(ITestOutputHelper output) => _out = output;

    // Maleki's Honor's values plus a test-only set physical bonus, so the last-equipped
    // arithmetic has something to show, and a test-only energy eater.
    private sealed class JuggernautTestShield : BaseSetShield
    {
        public JuggernautTestShield() : base(0x1B74) // metal kite shield graphic
        {
            SetHue = 0x76D;

            SetSelfRepair = 3;
            SetAttributes.DefendChance = 10;
            SetAttributes.BonusStr = 10;
            SetAttributes.WeaponSpeed = 35;

            SetPhysicalBonus = 5;

            AbsorptionAttributes.EaterEnergy = 4;
        }

        public override SetItem SetID => SetItem.Juggernaut;
        public override int Pieces => 2;

        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 3;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 3;
    }

    // Stands in for Evocaricus. On the S1 carrier, deliberately.
    private sealed class JuggernautTestArmor : BaseSetArmor
    {
        public JuggernautTestArmor() : base(0x1415) // plate chest graphic
        {
            SetHue = 0x76D;
            SetAttributes.DefendChance = 10;
            SetAttributes.BonusStr = 10;
            SetPhysicalBonus = 5;
        }

        public override SetItem SetID => SetItem.Juggernaut;
        public override int Pieces => 2;

        public override int BasePhysicalResistance => 3;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    [Fact]
    public void ACompletedShieldSetReachesTheMobile()
    {
        var m = new PlayerMobile();
        m.RawStr = 50;

        var shield = new JuggernautTestShield();
        var chest = new JuggernautTestArmor();

        Wear(m, shield, Layer.TwoHanded);
        m.UpdateResistances();
        _out.WriteLine($"1 piece : SetID={shield.SetID} IsSetItem={shield.IsSetItem} SetEquipped={shield.SetEquipped} " +
                       $"phys={m.PhysicalResistance} nrgy={m.EnergyResistance}");

        Assert.Equal(SetItem.Juggernaut, shield.SetID);
        Assert.True(shield.IsSetItem);
        Assert.False(shield.SetEquipped);
        Assert.Equal(3, m.PhysicalResistance); // the shield's own
        Assert.Equal(3, m.EnergyResistance);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.DefendChance));
        Assert.Equal(0, m.GetStatOffset(StatType.Str));

        Wear(m, chest, Layer.InnerTorso);
        m.UpdateResistances();

        _out.WriteLine($"2 pieces: shield.SetEquipped={shield.SetEquipped} chest.SetEquipped={chest.SetEquipped} " +
                       $"LastEquipped shield={shield.LastEquipped} chest={chest.LastEquipped} " +
                       $"phys={m.PhysicalResistance} nrgy={m.EnergyResistance} " +
                       $"DefendChance={AosAttributes.GetValue(m, AosAttribute.DefendChance)} " +
                       $"strOffset={m.GetStatOffset(StatType.Str)}");

        Assert.True(shield.SetEquipped);
        Assert.True(chest.SetEquipped);
        Assert.True(chest.LastEquipped);
        Assert.False(shield.LastEquipped);

        // Not per-piece: the last-equipped piece contributes (own x Pieces + bonus) and the
        // other contributes nothing, through PlayerMobile.GetItemResistance.
        Assert.False(SetHelper.ResistsBonusPerPiece(shield));
        Assert.Equal(0, shield.SetResistBonus(ResistanceType.Physical));
        Assert.Equal(3 * 2 + 5, chest.SetResistBonus(ResistanceType.Physical));
        Assert.Equal(3 * 2 + 5, m.PhysicalResistance);
        Assert.Equal(0, m.EnergyResistance); // the shield's 3 is replaced by the set arithmetic

        Assert.Equal(10, AosAttributes.GetValue(m, AosAttribute.DefendChance));
        Assert.Equal(10, m.GetStatOffset(StatType.Str));
        Assert.Equal(4, SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterEnergy));
        Assert.Equal(0x76D, shield.Hue);

        // Re-order so the shield is the last-equipped piece and carries the arithmetic itself.
        m.RemoveItem(shield);
        m.UpdateResistances();
        Assert.False(chest.SetEquipped);
        Assert.Equal(3, m.PhysicalResistance);

        Wear(m, shield, Layer.TwoHanded);
        m.UpdateResistances();
        _out.WriteLine($"re-worn : LastEquipped shield={shield.LastEquipped} chest={chest.LastEquipped} " +
                       $"phys={m.PhysicalResistance} nrgy={m.EnergyResistance} " +
                       $"WeaponSpeed={AosAttributes.GetValue(m, AosAttribute.WeaponSpeed)}");

        Assert.True(shield.LastEquipped);
        Assert.False(chest.LastEquipped);
        Assert.Equal(3 * 2 + 5, shield.SetResistBonus(ResistanceType.Physical));
        Assert.Equal(3 * 2 + 5, m.PhysicalResistance);
        Assert.Equal(3 * 2, m.EnergyResistance); // shield's energy x Pieces, no energy bonus
        Assert.Equal(35, AosAttributes.GetValue(m, AosAttribute.WeaponSpeed)); // the shield's SetAttributes now

        m.RemoveItem(chest);
        m.UpdateResistances();
        Assert.False(shield.SetEquipped);
        Assert.Equal(3, m.PhysicalResistance);
        Assert.Equal(0, m.GetStatOffset(StatType.Str));
        Assert.Equal(0, shield.Hue);

        m.Delete();
    }

    [Fact]
    public void StockShieldIsUnaffected()
    {
        var m = new PlayerMobile();
        var shield = Wear(m, new MetalKiteShield(), Layer.TwoHanded);

        m.UpdateResistances();
        _out.WriteLine($"stock MetalKiteShield: ISetItem={shield is ISetItem} IAbsorptionItem={shield is IAbsorptionItem} " +
                       $"nrgy={m.EnergyResistance}");

        Assert.False(shield is ISetItem);
        Assert.False(shield is IAbsorptionItem);
        Assert.Equal(1, m.EnergyResistance);

        m.Delete();
    }

    [Fact]
    public void ShieldSetStateSurvivesASerializationRoundTrip()
    {
        var original = new JuggernautTestShield();
        original.SetEquipped = true;
        original.LastEquipped = true;
        original.SetColdBonus = 9;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new JuggernautTestShield();
        copy.SetHue = 0;
        copy.SetSelfRepair = 0;
        copy.SetPhysicalBonus = 0;
        copy.AbsorptionAttributes.EaterEnergy = 0;
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetSelfRepair={copy.SetSelfRepair} " +
                       $"SetPhysicalBonus={copy.SetPhysicalBonus} SetColdBonus={copy.SetColdBonus} " +
                       $"SetAttributes.WeaponSpeed={copy.SetAttributes.WeaponSpeed} " +
                       $"EaterEnergy={copy.AbsorptionAttributes.EaterEnergy}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(0x76D, copy.SetHue);
        Assert.Equal(3, copy.SetSelfRepair);
        Assert.Equal(5, copy.SetPhysicalBonus);
        Assert.Equal(9, copy.SetColdBonus);
        Assert.Equal(35, copy.SetAttributes.WeaponSpeed);
        Assert.Equal(4, copy.AbsorptionAttributes.EaterEnergy);

        original.Delete();
        copy.Delete();
    }
}
