// Regression test for the S10 weapon set carrier, BaseSetWeapon.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// The carrier is under test, not any content: the real Juggernaut pieces (Evocaricus, Maleki's
// Honor) are CC9 batch 4's to port, so the set pieces here are test-local subclasses. What has to
// keep working across a MODERNUO_COMMIT bump is that a completed set built on this carrier reaches
// the mobile through the two kind-agnostic S1 hooks: AOS-set-attribute-aggregation.patch (set
// attributes) and Mobile-set-item-resistance-hook.patch (resistances), neither of which names a
// weapon. See shard-migration/notes/s10-carriers.md.

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
public class WeaponSetCarrierVerification
{
    private readonly ITestOutputHelper _out;

    public WeaponSetCarrierVerification(ITestOutputHelper output) => _out = output;

    // Two of these make a set. Values are Evocaricus's, plus a test-only fire eater so the
    // absorption carrier is exercised through the same object.
    private sealed class JuggernautTestSword : BaseSetWeapon
    {
        public JuggernautTestSword() : base(0x13B9) // viking sword graphic
        {
            SetHue = 0x76D;

            Attributes.WeaponDamage = 50;

            SetSelfRepair = 3;
            SetAttributes.DefendChance = 10;
            SetAttributes.BonusStr = 10;
            SetAttributes.WeaponSpeed = 35;

            AbsorptionAttributes.EaterFire = 5;
        }

        public override SetItem SetID => SetItem.Juggernaut;
        public override int Pieces => 2;
    }

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    [Fact]
    public void ACompletedWeaponSetReachesTheMobile()
    {
        var m = new PlayerMobile();
        m.RawStr = 50;

        var first = new JuggernautTestSword();
        var second = new JuggernautTestSword();

        Wear(m, first, Layer.OneHanded);
        _out.WriteLine($"1 piece : SetID={first.SetID} Pieces={first.Pieces} IsSetItem={first.IsSetItem} " +
                       $"SetEquipped={first.SetEquipped} Hue={first.Hue} SetHue={first.SetHue}");

        Assert.Equal(SetItem.Juggernaut, first.SetID);
        Assert.True(first.IsSetItem);
        Assert.False(first.SetEquipped);
        Assert.Equal(50, AosAttributes.GetValue(m, AosAttribute.WeaponDamage)); // the piece's own
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusStr));       // not the set's
        Assert.Equal(0, m.GetStatOffset(StatType.Str));

        // Second piece completes the set.
        Wear(m, second, Layer.TwoHanded);

        _out.WriteLine($"2 pieces: first.SetEquipped={first.SetEquipped} second.SetEquipped={second.SetEquipped} " +
                       $"LastEquipped first={first.LastEquipped} second={second.LastEquipped}");

        Assert.True(first.SetEquipped);
        Assert.True(second.SetEquipped);
        Assert.True(second.LastEquipped);  // exactly one piece carries the set attributes
        Assert.False(first.LastEquipped);

        // Set attributes reach the mobile through the AOS aggregation patch, once, on top of the
        // per-piece attributes, which still stack.
        var bonusStr = AosAttributes.GetValue(m, AosAttribute.BonusStr);
        var defendChance = AosAttributes.GetValue(m, AosAttribute.DefendChance);
        var weaponDamage = AosAttributes.GetValue(m, AosAttribute.WeaponDamage);
        _out.WriteLine($"2 pieces: BonusStr={bonusStr} DefendChance={defendChance} WeaponDamage={weaponDamage} " +
                       $"strOffset={m.GetStatOffset(StatType.Str)}");

        Assert.Equal(10, bonusStr);
        Assert.Equal(10, defendChance);
        Assert.Equal(100, weaponDamage); // 2 x 50, per piece
        Assert.Equal(10, m.GetStatOffset(StatType.Str)); // SetHelper.AddStatBonuses, "SetStr" mod

        // The set hue is applied by swapping it with the item's hue.
        Assert.Equal(0x76D, first.Hue);
        Assert.Equal(0, first.SetHue);

        // Absorption rides on the same carrier: 2 pieces x 5%.
        Assert.Equal(10, SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterFire));

        // Breaking the set removes everything again.
        m.RemoveItem(second);
        _out.WriteLine($"1 piece : first.SetEquipped={first.SetEquipped} BonusStr=" +
                       $"{AosAttributes.GetValue(m, AosAttribute.BonusStr)} Hue={first.Hue} " +
                       $"strOffset={m.GetStatOffset(StatType.Str)}");

        Assert.False(first.SetEquipped);
        Assert.False(second.SetEquipped);
        Assert.False(second.LastEquipped);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusStr));
        Assert.Equal(0, m.GetStatOffset(StatType.Str));
        Assert.Equal(0, first.Hue);
        Assert.Equal(0x76D, first.SetHue);
        Assert.Equal(5, SAAbsorptionAttributes.GetValue(m, SAAbsorptionAttribute.EaterFire));

        m.Delete();
    }

    [Fact]
    public void StockWeaponIsUnaffected()
    {
        var sword = new VikingSword();

        _out.WriteLine($"stock VikingSword: ISetItem={sword is ISetItem} IAbsorptionItem={sword is IAbsorptionItem}");

        Assert.False(sword is ISetItem);
        Assert.False(sword is IAbsorptionItem);

        sword.Delete();
    }

    [Fact]
    public void WeaponSetStateSurvivesASerializationRoundTrip()
    {
        var original = new JuggernautTestSword();
        original.SetEquipped = true;
        original.LastEquipped = true;
        original.SetPhysicalBonus = 4;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new JuggernautTestSword();
        copy.SetHue = 0;               // make sure the values below come from the buffer
        copy.SetSelfRepair = 0;
        copy.AbsorptionAttributes.EaterFire = 0;
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetSelfRepair={copy.SetSelfRepair} " +
                       $"SetPhysicalBonus={copy.SetPhysicalBonus} " +
                       $"SetAttributes.BonusStr={copy.SetAttributes.BonusStr} " +
                       $"EaterFire={copy.AbsorptionAttributes.EaterFire} " +
                       $"Attributes.WeaponDamage={copy.Attributes.WeaponDamage}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(0x76D, copy.SetHue);
        Assert.Equal(3, copy.SetSelfRepair);
        Assert.Equal(4, copy.SetPhysicalBonus);
        Assert.Equal(10, copy.SetAttributes.BonusStr);
        Assert.Equal(35, copy.SetAttributes.WeaponSpeed);
        Assert.Equal(5, copy.AbsorptionAttributes.EaterFire);
        Assert.Equal(50, copy.Attributes.WeaponDamage);

        original.Delete();
        copy.Delete();
    }
}
