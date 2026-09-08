// Regression test for the S10 quiver set carrier, BaseSetQuiver.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// The carrier is under test, not any content: the real Marksman pieces (Feathernock, Swiftflight)
// are CC9 batch 4's to port, so the quiver here is a test-local subclass. Its partner is a
// test-local BaseSetArmor piece rather than a BaseSetWeapon one, so that this gate depends on the
// S1 carrier that is already proven and not on another S10 carrier. It also proves a set may span
// two carriers, which every real mixed set relies on. See shard-migration/notes/s10-carriers.md.

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
public class QuiverSetCarrierVerification
{
    private readonly ITestOutputHelper _out;

    public QuiverSetCarrierVerification(ITestOutputHelper output) => _out = output;

    // Feathernock's values.
    private sealed class MarksmanTestQuiver : BaseSetQuiver
    {
        public MarksmanTestQuiver()
        {
            SetHue = 0x594;

            Attributes.WeaponDamage = 10;

            SetAttributes.AttackChance = 15;
            SetAttributes.BonusDex = 8;
            SetAttributes.WeaponSpeed = 30;
            SetAttributes.WeaponDamage = 20;
        }

        public override SetItem SetID => SetItem.Marksman;
        public override int Pieces => 2;
    }

    // Stands in for Swiftflight. On the S1 carrier, deliberately.
    private sealed class MarksmanTestArmor : BaseSetArmor
    {
        public MarksmanTestArmor() : base(0x13BB) // chainmail coif graphic
        {
            SetHue = 0x594;
            SetAttributes.AttackChance = 15;
            SetAttributes.BonusDex = 8;
        }

        public override SetItem SetID => SetItem.Marksman;
        public override int Pieces => 2;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    [Fact]
    public void ACompletedQuiverSetReachesTheMobile()
    {
        var m = new PlayerMobile();
        m.RawDex = 50;

        var quiver = new MarksmanTestQuiver();
        var helm = new MarksmanTestArmor();

        Wear(m, quiver, Layer.Cloak);
        _out.WriteLine($"1 piece : SetID={quiver.SetID} IsSetItem={quiver.IsSetItem} SetEquipped={quiver.SetEquipped} " +
                       $"WeaponDamage={AosAttributes.GetValue(m, AosAttribute.WeaponDamage)}");

        Assert.Equal(SetItem.Marksman, quiver.SetID);
        Assert.True(quiver.IsSetItem);
        Assert.False(quiver.SetEquipped);
        Assert.Equal(10, AosAttributes.GetValue(m, AosAttribute.WeaponDamage)); // the quiver's own
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.AttackChance));  // not the set's
        Assert.Equal(0, m.GetStatOffset(StatType.Dex));

        Wear(m, helm, Layer.Helm);

        _out.WriteLine($"2 pieces: quiver.SetEquipped={quiver.SetEquipped} helm.SetEquipped={helm.SetEquipped} " +
                       $"LastEquipped quiver={quiver.LastEquipped} helm={helm.LastEquipped}");

        Assert.True(quiver.SetEquipped);
        Assert.True(helm.SetEquipped);
        Assert.True(helm.LastEquipped);   // the armour piece completed the set...
        Assert.False(quiver.LastEquipped);

        var attackChance = AosAttributes.GetValue(m, AosAttribute.AttackChance);
        var weaponDamage = AosAttributes.GetValue(m, AosAttribute.WeaponDamage);
        _out.WriteLine($"2 pieces: AttackChance={attackChance} WeaponDamage={weaponDamage} " +
                       $"dexOffset={m.GetStatOffset(StatType.Dex)} quiver.Hue={quiver.Hue}");

        Assert.Equal(15, attackChance);   // ...so its SetAttributes are the ones aggregated, once
        Assert.Equal(10, weaponDamage);   // the quiver's own still counts; helm's set damage does not exist
        Assert.Equal(8, m.GetStatOffset(StatType.Dex));
        Assert.Equal(0x594, quiver.Hue);  // the quiver still took the set hue
        Assert.Equal(0, quiver.SetHue);

        // Now the other way round, so the quiver is the last-equipped piece and its own
        // SetAttributes are what reaches the mobile.
        m.RemoveItem(quiver);
        Assert.False(helm.SetEquipped);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.AttackChance));
        Assert.Equal(0, m.GetStatOffset(StatType.Dex));

        Wear(m, quiver, Layer.Cloak);
        _out.WriteLine($"re-worn : LastEquipped quiver={quiver.LastEquipped} helm={helm.LastEquipped} " +
                       $"WeaponDamage={AosAttributes.GetValue(m, AosAttribute.WeaponDamage)} " +
                       $"WeaponSpeed={AosAttributes.GetValue(m, AosAttribute.WeaponSpeed)}");

        Assert.True(quiver.LastEquipped);
        Assert.False(helm.LastEquipped);
        Assert.Equal(10 + 20, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));
        Assert.Equal(30, AosAttributes.GetValue(m, AosAttribute.WeaponSpeed));
        Assert.Equal(8, m.GetStatOffset(StatType.Dex));

        m.RemoveItem(helm);
        Assert.False(quiver.SetEquipped);
        Assert.False(quiver.LastEquipped);
        Assert.Equal(10, AosAttributes.GetValue(m, AosAttribute.WeaponDamage));
        Assert.Equal(0x594, quiver.SetHue);

        m.Delete();
    }

    [Fact]
    public void StockQuiverIsUnaffected()
    {
        var quiver = new ElvenQuiver();

        _out.WriteLine($"stock ElvenQuiver: ISetItem={quiver is ISetItem}");

        Assert.False(quiver is ISetItem);

        quiver.Delete();
    }

    [Fact]
    public void QuiverSetStateSurvivesASerializationRoundTrip()
    {
        var original = new MarksmanTestQuiver();
        original.SetEquipped = true;
        original.LastEquipped = true;
        original.SetEnergyBonus = 7;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new MarksmanTestQuiver();
        copy.SetHue = 0;
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetEnergyBonus={copy.SetEnergyBonus} " +
                       $"SetAttributes.AttackChance={copy.SetAttributes.AttackChance} " +
                       $"SetAttributes.WeaponDamage={copy.SetAttributes.WeaponDamage} " +
                       $"Attributes.WeaponDamage={copy.Attributes.WeaponDamage}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(0x594, copy.SetHue);
        Assert.Equal(7, copy.SetEnergyBonus);
        Assert.Equal(15, copy.SetAttributes.AttackChance);
        Assert.Equal(20, copy.SetAttributes.WeaponDamage);
        Assert.Equal(10, copy.Attributes.WeaponDamage);

        original.Delete();
        copy.Delete();
    }
}
