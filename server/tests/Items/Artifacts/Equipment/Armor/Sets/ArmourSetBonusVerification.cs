// Regression tests for the S1 armour set-bonus subsystem.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run by docker/uo/run-tests.sh. These four tests were
// written and passed during S1; S4 committed them unchanged apart from the namespace.
//
// They are what a MODERNUO_COMMIT bump has to re-run: all three S1 patches can still
// apply while the set bonus stops reaching the mobile, and nothing else would notice.
// See shard-migration/notes/s1-armour-sets.md section 3 and notes/s4-test-route.md.

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
public class ArmourSetBonusVerification
{
    private readonly ITestOutputHelper _out;

    public ArmourSetBonusVerification(ITestOutputHelper output) => _out = output;

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    [Fact]
    public void AloronSetGrantsItsSetBonus()
    {
        var m = new PlayerMobile();

        var tunic = new AloronsTunic();
        var helm = new AloronsHelm();
        var legs = new AloronsLegs();
        var gorget = new AloronsGorget();

        // A single piece is a set item but the set is not complete.
        Wear(m, tunic, Layer.InnerTorso);
        _out.WriteLine($"1 piece : SetID={tunic.SetID} Pieces={tunic.Pieces} IsSetItem={tunic.IsSetItem} " +
                       $"SetEquipped={tunic.SetEquipped} LastEquipped={tunic.LastEquipped}");

        Assert.Equal(SetItem.Aloron, tunic.SetID);
        Assert.Equal(4, tunic.Pieces);
        Assert.True(tunic.IsSetItem);
        Assert.False(tunic.SetEquipped);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusMana));

        m.UpdateResistances();
        var physOnePiece = m.PhysicalResistance;
        _out.WriteLine($"1 piece : phys={m.PhysicalResistance} fire={m.FireResistance} cold={m.ColdResistance} " +
                       $"pois={m.PoisonResistance} nrgy={m.EnergyResistance}");

        Wear(m, helm, Layer.Helm);
        Wear(m, legs, Layer.Pants);

        Assert.False(tunic.SetEquipped);

        // Fourth piece completes the set.
        Wear(m, gorget, Layer.Neck);

        _out.WriteLine($"4 pieces: tunic.SetEquipped={tunic.SetEquipped} helm.SetEquipped={helm.SetEquipped} " +
                       $"legs.SetEquipped={legs.SetEquipped} gorget.SetEquipped={gorget.SetEquipped}");
        _out.WriteLine($"4 pieces: LastEquipped tunic={tunic.LastEquipped} helm={helm.LastEquipped} " +
                       $"legs={legs.LastEquipped} gorget={gorget.LastEquipped}");

        Assert.True(tunic.SetEquipped);
        Assert.True(helm.SetEquipped);
        Assert.True(legs.SetEquipped);
        Assert.True(gorget.SetEquipped);

        // Exactly one piece carries the set attributes.
        Assert.True(gorget.LastEquipped);
        Assert.False(tunic.LastEquipped);
        Assert.False(helm.LastEquipped);
        Assert.False(legs.LastEquipped);

        // The set attributes reach the mobile through AosAttributes.GetValue.
        var bonusMana = AosAttributes.GetValue(m, AosAttribute.BonusMana);
        var lowerManaCost = AosAttributes.GetValue(m, AosAttribute.LowerManaCost);
        var bonusDex = AosAttributes.GetValue(m, AosAttribute.BonusDex);
        _out.WriteLine($"4 pieces: BonusMana={bonusMana} LowerManaCost={lowerManaCost} BonusDex={bonusDex}");

        Assert.Equal(15, bonusMana);     // SetAttributes.BonusMana = 15, from the last-equipped piece only
        Assert.Equal(20, lowerManaCost); // SetAttributes.LowerManaCost = 20, likewise
        Assert.Equal(16, bonusDex);      // Attributes.BonusDex = 4 on each of the four pieces

        // Set resistance bonus reaches the mobile through the patched ComputeResistances.
        m.UpdateResistances();
        _out.WriteLine($"4 pieces: phys={m.PhysicalResistance} fire={m.FireResistance} cold={m.ColdResistance} " +
                       $"pois={m.PoisonResistance} nrgy={m.EnergyResistance}");
        _out.WriteLine($"piece    : phys={tunic.PhysicalResistance} setBonus={tunic.SetPhysicalBonus} " +
                       $"setResist={tunic.SetResistBonus(ResistanceType.Physical)}");

        // Aloron is a per-piece set: each worn piece contributes its own resist plus its set bonus.
        Assert.Equal(7 + 8, tunic.SetResistBonus(ResistanceType.Physical));
        Assert.Equal(6 + 9, tunic.SetResistBonus(ResistanceType.Cold));
        Assert.Equal(4 * (7 + 8), m.PhysicalResistance);
        Assert.Equal(4 * (6 + 9), m.ColdResistance);
        Assert.Equal(7, physOnePiece); // sanity: the one-piece reading carried no set bonus

        // Set self repair is offered to BaseArmor.OnHit through the patched hook.
        _out.WriteLine($"selfrepair: base={tunic.ArmorAttributes.SelfRepair} effective={tunic.EffectiveSelfRepair}");
        Assert.Equal(3, tunic.EffectiveSelfRepair);

        // Breaking the set removes the bonus again.
        m.RemoveItem(gorget); // calls Item.OnRemoved
        m.UpdateResistances();
        _out.WriteLine($"3 pieces: tunic.SetEquipped={tunic.SetEquipped} BonusMana=" +
                       $"{AosAttributes.GetValue(m, AosAttribute.BonusMana)} phys={m.PhysicalResistance}");

        Assert.False(tunic.SetEquipped);
        Assert.False(gorget.SetEquipped);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusMana));
        Assert.Equal(3 * 7, m.PhysicalResistance);

        m.Delete();
    }

    [Fact]
    public void StockArmourIsUnaffected()
    {
        var m = new PlayerMobile();
        var chest = Wear(m, new LeatherChest(), Layer.InnerTorso);

        m.UpdateResistances();
        _out.WriteLine($"stock LeatherChest: phys={m.PhysicalResistance} isSetItem={chest is ISetItem}");

        Assert.False(chest is ISetItem);
        Assert.Equal(2, m.PhysicalResistance);
        Assert.Equal(0, AosAttributes.GetValue(m, AosAttribute.BonusMana));

        m.Delete();
    }

    [Fact]
    public void TigerPeltAloneIsNotASetItem()
    {
        var m = new PlayerMobile();
        var chest = Wear(m, new TigerPeltChest(), Layer.InnerTorso);

        m.UpdateResistances();
        _out.WriteLine($"TigerPeltChest: SetID={chest.SetID} IsSetItem={chest.IsSetItem} phys={m.PhysicalResistance}");

        Assert.Equal(SetItem.None, chest.SetID);
        Assert.False(chest.IsSetItem);
        Assert.Equal(2, m.PhysicalResistance);

        m.Delete();
    }

    [Fact]
    public void SetStateSurvivesASerializationRoundTrip()
    {
        var original = new AloronsTunic();
        original.SetHue = 1234;
        original.SetEquipped = true;
        original.LastEquipped = true;

        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = new AloronsTunic(original.Serial);
        copy.Deserialize(new BufferReader(buffer));

        _out.WriteLine($"round trip: SetEquipped={copy.SetEquipped} LastEquipped={copy.LastEquipped} " +
                       $"SetHue={copy.SetHue} SetSelfRepair={copy.SetSelfRepair} " +
                       $"SetPhysicalBonus={copy.SetPhysicalBonus} SetColdBonus={copy.SetColdBonus} " +
                       $"SetAttributes.BonusMana={copy.SetAttributes.BonusMana} " +
                       $"SetAttributes.LowerManaCost={copy.SetAttributes.LowerManaCost}");

        Assert.True(copy.SetEquipped);
        Assert.True(copy.LastEquipped);
        Assert.Equal(1234, copy.SetHue);
        Assert.Equal(3, copy.SetSelfRepair);
        Assert.Equal(8, copy.SetPhysicalBonus);
        Assert.Equal(9, copy.SetColdBonus);
        Assert.Equal(15, copy.SetAttributes.BonusMana);
        Assert.Equal(20, copy.SetAttributes.LowerManaCost);
        Assert.Equal(4, copy.Attributes.BonusDex);

        original.Delete();
        copy.Delete();
    }
}
