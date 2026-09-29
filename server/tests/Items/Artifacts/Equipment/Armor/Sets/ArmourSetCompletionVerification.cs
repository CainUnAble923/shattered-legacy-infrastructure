// Regression test for the armour-set completion task: the three sets that could not be finished.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh.
//
// Virtue: pinned ships SpiritualityHelm and ValorGauntlets as standalone BaseArmor with the set bonus folded in,
// so the six ported pieces counted six of eight forever. Virtue-stock-pieces-set-carrier.patch re-parents both onto
// BaseSetArmor with ServUO's values. If a MODERNUO_COMMIT bump reverts either file, or the patch stops reaching the
// build, the first two facts go red. Bestial and Virtuoso: each set's gargoyle half is four pieces and the kilt was
// the missing fourth. The tooltip facts use PropertyListReader, the first shard test to assert a property list.
// See shard-migration/notes/armour-set-completion.md.

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ArmourSetCompletionVerification
{
    private readonly ITestOutputHelper _out;

    public ArmourSetCompletionVerification(ITestOutputHelper output) => _out = output;

    private static T Wear<T>(Mobile m, T item, Layer layer) where T : Item
    {
        item.Layer = layer;
        m.AddItem(item);
        return item;
    }

    private static readonly ResistanceType[] Resists =
    [
        ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Poison, ResistanceType.Energy
    ];

    private static int[] OwnResists(Item i) =>
        [i.PhysicalResistance, i.FireResistance, i.ColdResistance, i.PoisonResistance, i.EnergyResistance];

    [Fact]
    public void TheStockVirtuePiecesCarryServUOsValuesAndTheSetId()
    {
        var helm = new SpiritualityHelm();
        var gauntlets = new ValorGauntlets();

        foreach (var piece in new BaseSetArmor[] { helm, gauntlets })
        {
            Assert.Equal(SetItem.Virtue, piece.SetID);
            Assert.Equal(8, piece.Pieces);
            Assert.True(piece.IsSetItem);
            Assert.Equal(5, piece.SetSelfRepair);
            Assert.Equal(0, piece.ArmorAttributes.SelfRepair); // set-only now, not always-on as stock had it
            Assert.Equal(0, piece.EffectiveSelfRepair);         // ...and so zero until the set is worn
            Assert.Equal(0x226, piece.Hue);
            Assert.Equal(LootType.Blessed, piece.LootType);
            Assert.Equal(ArmorMaterialType.Plate, piece.MaterialType);
            foreach (var r in Resists)
            {
                Assert.Equal(5, BonusFor(piece, r));
            }
        }

        // ServUO's, not pinned's: pinned's were these plus the set's 5 (13/13/12/14/13 and 11/11/13/14/10).
        Assert.Equal(new[] { 8, 8, 7, 9, 8 }, OwnResists(helm));
        Assert.Equal(new[] { 6, 6, 8, 9, 6 }, OwnResists(gauntlets));
        Assert.Equal(0x2B10, helm.ItemID);
        Assert.Equal(0x2B0C, gauntlets.ItemID);
        Assert.Equal(25, helm.AosStrReq);
        Assert.Equal(50, gauntlets.AosStrReq);
        Assert.Equal(1075237, helm.LabelNumber);
        Assert.Equal(1075238, gauntlets.LabelNumber);

        // Still what the heritage token hands out: the gump resolves the type, and nothing else names it.
        Assert.Same(typeof(SpiritualityHelm), AssemblyHandler.FindTypeByName("SpiritualityHelm"));
        Assert.Same(typeof(ValorGauntlets), AssemblyHandler.FindTypeByName("ValorGauntlets"));

        helm.Delete();
        gauntlets.Delete();
    }

    private static int BonusFor(ISetItem i, ResistanceType r) => r switch
    {
        ResistanceType.Physical => i.SetPhysicalBonus,
        ResistanceType.Fire     => i.SetFireBonus,
        ResistanceType.Cold     => i.SetColdBonus,
        ResistanceType.Poison   => i.SetPoisonBonus,
        _                       => i.SetEnergyBonus
    };

    [Fact]
    public void AllEightVirtuePiecesCompleteTheSet()
    {
        var m = new PlayerMobile();

        var pieces = new (Item Item, Layer Layer)[]
        {
            (new CompassionArms(), Layer.Arms),
            (new HonestyGorget(), Layer.Neck),
            (new HonorLegs(), Layer.Pants),
            (new HumilityCloak(), Layer.Cloak),
            (new JusticeBreastplate(), Layer.InnerTorso),
            (new SacrificeSollerets(), Layer.Shoes),
            (new SpiritualityHelm(), Layer.Helm),
            (new ValorGauntlets(), Layer.Gloves)
        };

        // Seven worn, the stock gauntlets held back: this is what Chase's character had in effect before the patch.
        for (var i = 0; i < 7; i++)
        {
            Wear(m, pieces[i].Item, pieces[i].Layer);
        }

        m.UpdateResistances();
        var helm = (SpiritualityHelm)pieces[6].Item;
        Assert.False(SetHelper.FullSetEquipped(m, SetItem.Virtue, 8));
        Assert.False(helm.SetEquipped);
        var physSeven = m.GetItemResistance(helm, ResistanceType.Physical);
        Assert.Equal(8, physSeven); // own only

        var gauntlets = Wear(m, (ValorGauntlets)pieces[7].Item, pieces[7].Layer);
        m.UpdateResistances();

        _out.WriteLine($"eight worn: full={SetHelper.FullSetEquipped(m, SetItem.Virtue, 8)} helm.SetEquipped={helm.SetEquipped} " +
                       $"gauntlets.SetEquipped={gauntlets.SetEquipped} helm phys via mobile={m.GetItemResistance(helm, ResistanceType.Physical)} " +
                       $"self repair helm={helm.EffectiveSelfRepair} hue helm={helm.Hue:X} set hue={helm.SetHue:X}");

        Assert.True(SetHelper.FullSetEquipped(m, SetItem.Virtue, 8));

        foreach (var (item, _) in pieces)
        {
            var set = (ISetItem)item;
            Assert.True(set.SetEquipped, $"{item.GetType().Name} did not see the full set");

            // Virtue is per-piece (SetHelper.ResistsBonusPerPiece): each piece contributes its own plus 5.
            foreach (var r in Resists)
            {
                Assert.Equal(OwnResists(item)[Array.IndexOf(Resists, r)] + 5, m.GetItemResistance(item, r));
            }
        }

        // The set's self repair reaches the two re-parented pieces through BaseArmor-set-self-repair.patch.
        Assert.Equal(5, helm.EffectiveSelfRepair);
        Assert.Equal(5, gauntlets.EffectiveSelfRepair);

        // The hue swaps exactly as it does on the six ported pieces (the Virtue set hue is "keep the item's own").
        var arms = (CompassionArms)pieces[0].Item;
        Assert.Equal(arms.Hue, helm.Hue);
        Assert.Equal(arms.SetHue, helm.SetHue);
        Assert.Equal(arms.Hue, gauntlets.Hue);

        // Taking one piece off breaks it for all.
        m.RemoveItem(gauntlets);
        m.UpdateResistances();
        Assert.False(helm.SetEquipped);
        Assert.Equal(8, m.GetItemResistance(helm, ResistanceType.Physical));

        foreach (var (item, _) in pieces)
        {
            item.Delete();
        }

        m.Delete();
    }

    [Fact]
    public void TheVirtueTooltipSaysEightPiecesAndThenFullSet()
    {
        var m = new PlayerMobile();
        var helm = new SpiritualityHelm();

        var alone = PropertyListReader.Read(helm);
        _out.WriteLine($"helm alone: {alone.Describe()}");

        Assert.True(alone.Has(1075237));            // Helm of Spirituality (Virtue Armor Set)
        Assert.True(alone.Has(1072376, "8"));       // Part of an Armor Set (8 pieces)
        Assert.False(alone.Has(1072377));           // Full Armor Set Present
        Assert.True(alone.Has(1072378));            // Only when full set is present:
        Assert.True(alone.Has(1072382, "5"));       // physical resist +5%
        Assert.True(alone.Has(1060450, "5"));       // self repair 5, in the set block
        Assert.True(alone.Has(1060448, "8"));       // physical resist 8% - ServUO's own, not pinned's 13

        Wear(m, helm, Layer.Helm);
        var rest = new (Item Item, Layer Layer)[]
        {
            (new CompassionArms(), Layer.Arms), (new HonestyGorget(), Layer.Neck), (new HonorLegs(), Layer.Pants),
            (new HumilityCloak(), Layer.Cloak), (new JusticeBreastplate(), Layer.InnerTorso),
            (new SacrificeSollerets(), Layer.Shoes), (new ValorGauntlets(), Layer.Gloves)
        };
        foreach (var (item, layer) in rest)
        {
            Wear(m, item, layer);
        }

        m.UpdateResistances();
        var full = PropertyListReader.Read(helm);
        _out.WriteLine($"helm, full set: {full.Describe()}");

        Assert.True(full.Has(1072376, "8"));
        Assert.True(full.Has(1072377));             // Full Armor Set Present
        Assert.False(full.Has(1072378));
        Assert.True(full.Has(1080361));             // physical resist ~1_val~% (total)

        foreach (var (item, _) in rest)
        {
            item.Delete();
        }

        helm.Delete();
        m.Delete();
    }

    [Fact]
    public void TheTwoKiltsAreGargoyleKiltsOnTheClothingCarrier()
    {
        var bestial = new BestialKilt();
        var virtuoso = new VirtuososKilt();

        foreach (var kilt in new BaseSetClothing[] { bestial, virtuoso })
        {
            Assert.Equal(0x408, kilt.ItemID);
            Assert.Equal(Layer.Gloves, kilt.Layer); // the gargoyle kilt slot on both emulators, not a typo
            Assert.Equal(Race.AllowGargoylesOnly, kilt.RequiredRaces);
            Assert.Equal(5.0, kilt.Weight);
            Assert.Equal(4, kilt.Pieces);
            Assert.Equal(125, kilt.MaxHitPoints);
        }

        Assert.Equal(SetItem.Bestial, bestial.SetID);
        Assert.Equal(2010, bestial.Hue);
        Assert.Equal(1151546, bestial.LabelNumber);
        Assert.Equal(new[] { 24, 10, 9, 10, 9 }, OwnResists(bestial));
        Assert.False(bestial.BardMasteryBonus);

        Assert.Equal(SetItem.Virtuoso, virtuoso.SetID);
        Assert.Equal(1374, virtuoso.Hue);
        Assert.Equal(1374, virtuoso.SetHue);
        Assert.Equal(1151559, virtuoso.LabelNumber);
        Assert.Equal(new[] { 7, 8, 21, 8, 8 }, OwnResists(virtuoso));
        Assert.True(virtuoso.BardMasteryBonus);

        // Resolvable by the names a spawner, a reward table or [add uses; and the ServUO parent's name still resolves
        // to pinned's stock kilt through its [TypeAlias], so porting these did not put a second type under that name.
        Assert.Same(typeof(BestialKilt), AssemblyHandler.FindTypeByName("BestialKilt"));
        Assert.Same(typeof(VirtuososKilt), AssemblyHandler.FindTypeByName("VirtuososKilt"));
        Assert.Same(typeof(GargishClothKiltType1), AssemblyHandler.FindTypeByName("GargishClothKilt"));

        var tip = PropertyListReader.Read(bestial);
        _out.WriteLine($"bestial kilt: {tip.Describe()}");
        Assert.True(tip.Has(1151546));            // Bestial Kilt
        Assert.True(tip.Has(1072376, "4"));       // Part of an Armor Set (4 pieces)
        Assert.True(tip.Has(1151542, "5"));       // Berserk 5 (total): the tooltip line D-14 leaves without a mechanic

        tip = PropertyListReader.Read(virtuoso);
        _out.WriteLine($"virtuoso kilt: {tip.Describe()}");
        Assert.True(tip.Has(1151559));            // Virtuoso's Kilt
        Assert.True(tip.Has(1072376, "4"));
        Assert.True(tip.Has(1151553));            // Activate: Bard Mastery Bonus x2

        bestial.Delete();
        virtuoso.Delete();
    }

    [Fact]
    public void EachGargoyleHalfCompletesWithItsKilt()
    {
        var m = new PlayerMobile();

        // Not through Wear: BestialArms sets its own layer since D28, and a hand-set one here is what hid that it was
        // wrong. AddItem is layer-blind, so this fact never depended on it (notes/d27-worn-item-facts.md 3.2).
        var arms = new BestialArms();
        m.AddItem(arms);
        var earrings = Wear(m, new BestialEarrings(), Layer.Earrings);
        var necklace = Wear(m, new BestialNecklace(), Layer.Neck);
        Assert.False(arms.SetEquipped); // three of four: where the gargoyle half stopped before this task

        var kilt = Wear(m, new BestialKilt(), Layer.Gloves);
        m.UpdateResistances();

        Assert.True(kilt.SetEquipped);
        Assert.True(arms.SetEquipped);
        Assert.True(earrings.SetEquipped);
        Assert.True(necklace.SetEquipped);
        Assert.True(kilt.LastEquipped); // the kilt completed it, so its set attributes are the ones that aggregate
        Assert.Equal(4, BestialSetHelper.TotalPieces(m));

        // Off again: the set breaks and the helper's hue rule leaves the piece at its own hue.
        m.RemoveItem(kilt);
        Assert.False(arms.SetEquipped);
        Assert.Equal(2010, kilt.Hue);

        foreach (var i in new Item[] { arms, earrings, necklace, kilt })
        {
            i.Delete();
        }

        var armbands = Wear(m, new VirtuososArmbands(), Layer.Arms);
        var earpieces = Wear(m, new VirtuososEarpieces(), Layer.Earrings);
        var vneck = Wear(m, new VirtuososNecklace(), Layer.Neck);
        Assert.False(armbands.SetEquipped);

        var vkilt = Wear(m, new VirtuososKilt(), Layer.Gloves);

        Assert.True(vkilt.SetEquipped);
        Assert.True(armbands.SetEquipped);
        Assert.True(earpieces.SetEquipped);
        Assert.True(vneck.SetEquipped);

        foreach (var i in new Item[] { armbands, earpieces, vneck, vkilt })
        {
            i.Delete();
        }

        m.Delete();
    }

    // D28 (Q-065). ServUO's BestialArms is a GargishLeatherArms, whose constructor builds on 0x302 (an Arms row) and
    // then sets Layer = Layer.Arms itself (ServUO GargishLeatherArms.cs:19); only after that does BestialArms switch its
    // ItemID to 0x4052, which does not touch the layer. Ours passes 0x4052 to the base call, whose row is a chest plate,
    // so without its own Layer line it lands on InnerTorso and collides with any chest piece.
    //
    // The host has no tiledata (D27, TestHostTileData.cs), so the three rows this depends on are seeded for the fact's
    // duration by TestTileRows (Route/TestTileRows.cs has the values and where they came from).
    [Fact]
    public void BestialArmsIsOnTheArmsLayerAndGoesOnOverAGargishChest()
    {
        // 0x302 is ServUO's parent graphic (Arms), 0x304 the chest the arms go on beside (InnerTorso), 0x4052 the arms'
        // own graphic (InnerTorso, the row that put them on the wrong layer). All three are read in the constructors.
        using var tiles = TestTileRows.Seed(
            TileRows.GargishLeatherArmsType2,
            TileRows.GargishLeatherChestType1,
            TileRows.BestialArms
        );

        PlayerMobile m = null;
        Item chest = null, stock = null, arms = null;

        try
        {
            m = new PlayerMobile { Race = Race.Gargoyle, Player = true, RawStr = 50, RawDex = 50, RawInt = 50 };

            chest = new GargishLeatherChestType1();
            stock = new GargishLeatherArmsType2();
            arms = new BestialArms();

            // The player's order: the chest first, then the arms over it.
            var chestOn = m.EquipItem(chest);
            var armsOn = m.EquipItem(arms);

            _out.WriteLine(
                $"chest 0x{chest.ItemID:X} layer={chest.Layer} equipped={chestOn}; " +
                $"GargishLeatherArmsType2 0x{stock.ItemID:X} layer={stock.Layer}; " +
                $"BestialArms 0x{arms.ItemID:X} layer={arms.Layer} equipped={armsOn}"
            );

            Assert.Equal(Layer.Arms, arms.Layer);
            Assert.Equal(stock.Layer, arms.Layer); // the seeded rows are live: ServUO's parent reads Arms from 0x302
            Assert.Equal(0x4052, arms.ItemID);     // the graphic is right; only the computed layer was wrong

            Assert.True(chestOn);
            Assert.Same(chest, m.FindItemOnLayer(Layer.InnerTorso));

            Assert.True(armsOn, "a gargoyle wearing a gargish leather chest could not put Bestial Arms on");
            Assert.Same(m, arms.Parent);
            Assert.Same(arms, m.FindItemOnLayer(Layer.Arms));
            Assert.Same(chest, m.FindItemOnLayer(Layer.InnerTorso));
        }
        finally
        {
            arms?.Delete();
            stock?.Delete();
            chest?.Delete();
            m?.Delete();
        }
    }
}
