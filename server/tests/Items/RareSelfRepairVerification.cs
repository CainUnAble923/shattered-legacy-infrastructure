// RareSelfRepairVerification.cs
//
// cc-P32 Part C, F-27: Self Repair becomes an extremely rare property. server/patches/
// BaseRunicTool-no-random-self-repair.patch takes it out of pinned's random property table for armor,
// shields and hats (BaseRunicTool.cs:635, :703-707, :894-898), which is also what runic crafting draws
// from; customizations/ClusterFRareSelfRepair.cs gives it back only on high-end corpses, 1 in 500 magic
// weapons and armor, intensity 1 or 2 almost always.
//
// Facts, on seeded rolls (BuiltInRng.Generator, as PinnedWaitingDropsVerification scripts it):
//   1. The ordinary table never rolls Self Repair: 20,000 five-property rolls each on plate, studded
//      leather, a shield and a hat, and weapons too.
//   2. A runic craft never rolls it: 20,000 Valorite runic hammer applications each on plate and a shield.
//   3. The high-end roll gives it to about 1 in 500 eligible items (500,000 rolls), at intensity 1 or 2
//      about 98% of the time and 3 to 5 the rest.
//   4. Only a high-end corpse rolls, and only for magic weapons and armor: an ordinary creature's corpse and
//      a non-magic weapon get nothing even when every roll hits, and an artifact's fixed value is unchanged.

using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RareSelfRepairVerification
{
    private readonly ITestOutputHelper _out;

    public RareSelfRepairVerification(ITestOutputHelper output) => _out = output;

    private static void Seeded(int seed, Action body)
    {
        var original = BuiltInRng.Generator;
        BuiltInRng.Generator = new System.Random(seed);
        try
        {
            body();
        }
        finally
        {
            BuiltInRng.Generator = original;
        }
    }

    // Every roll hits: Next(n) is 0, NextDouble is 0.
    private sealed class AlwaysZero : System.Random
    {
        public override int Next() => 0;
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) => minValue;
        public override double NextDouble() => 0.0;
        protected override double Sample() => 0.0;
    }

    private const int Rolls = 20_000;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheOrdinaryTableNeverRollsSelfRepair()
    {
        var plate = new PlateChest();
        var studded = new StuddedChest();
        var shield = new HeaterShield();
        var hat = new TricorneHat();
        var sword = new Longsword();
        var hits = new int[5];

        try
        {
            Seeded(32032, () =>
            {
                for (var i = 0; i < Rolls; i++)
                {
                    BaseRunicTool.ApplyAttributesTo(plate, 5, 0, 100);
                    BaseRunicTool.ApplyAttributesTo(studded, 5, 0, 100);
                    BaseRunicTool.ApplyAttributesTo(shield, 5, 0, 100);
                    BaseRunicTool.ApplyAttributesTo(hat, 5, 0, 100);
                    BaseRunicTool.ApplyAttributesTo(sword, 5, 0, 100);

                    if (plate.ArmorAttributes.SelfRepair != 0) { hits[0]++; plate.ArmorAttributes.SelfRepair = 0; }
                    if (studded.ArmorAttributes.SelfRepair != 0) { hits[1]++; studded.ArmorAttributes.SelfRepair = 0; }
                    if (shield.ArmorAttributes.SelfRepair != 0) { hits[2]++; shield.ArmorAttributes.SelfRepair = 0; }
                    if (hat.ClothingAttributes.SelfRepair != 0) { hits[3]++; hat.ClothingAttributes.SelfRepair = 0; }
                    if (sword.WeaponAttributes.SelfRepair != 0) { hits[4]++; sword.WeaponAttributes.SelfRepair = 0; }
                }
            });

            _out.WriteLine($"Self Repair in {Rolls} five-property rolls: plate {hits[0]}, studded {hits[1]}, " +
                           $"shield {hits[2]}, hat {hits[3]}, longsword {hits[4]}");

            // The table still rolled: the items carry properties.
            Assert.False(plate.Attributes.IsEmpty && plate.ArmorAttributes.IsEmpty);
            Assert.All(hits, h => Assert.Equal(0, h));
        }
        finally
        {
            plate.Delete();
            studded.Delete();
            shield.Delete();
            hat.Delete();
            sword.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ARunicCraftNeverRollsSelfRepair()
    {
        var hammer = new RunicHammer(CraftResource.Valorite, 50);
        var plate = new PlateChest();
        var shield = new HeaterShield();
        int plateHits = 0, shieldHits = 0;

        try
        {
            Seeded(32033, () =>
            {
                for (var i = 0; i < Rolls; i++)
                {
                    hammer.ApplyAttributesTo(plate);
                    hammer.ApplyAttributesTo(shield);

                    if (plate.ArmorAttributes.SelfRepair != 0) { plateHits++; plate.ArmorAttributes.SelfRepair = 0; }
                    if (shield.ArmorAttributes.SelfRepair != 0) { shieldHits++; shield.ArmorAttributes.SelfRepair = 0; }
                }
            });

            _out.WriteLine($"Self Repair in {Rolls} Valorite runic applications: plate {plateHits}, shield {shieldHits}");
            Assert.Equal(0, plateHits);
            Assert.Equal(0, shieldHits);
        }
        finally
        {
            hammer.Delete();
            plate.Delete();
            shield.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheHighEndRollIsAboutOneInFiveHundred()
    {
        const int n = 500_000;
        var plate = new PlateChest();
        plate.Attributes.Luck = 50; // magic
        var byIntensity = new int[6];

        try
        {
            Assert.True(ClusterFRareSelfRepair.IsEligible(plate));

            Seeded(32034, () =>
            {
                for (var i = 0; i < n; i++)
                {
                    if (ClusterFRareSelfRepair.TryRoll(plate))
                    {
                        byIntensity[plate.ArmorAttributes.SelfRepair]++;
                        plate.ArmorAttributes.SelfRepair = 0;
                    }
                }
            });

            var hits = byIntensity.Sum();
            var expected = n / ClusterFRareSelfRepair.OneIn;
            var low = byIntensity[1] + byIntensity[2];
            _out.WriteLine($"{hits} of {n} (expected {expected}); intensity 1..5: " +
                           string.Join(", ", byIntensity.Skip(1)) + $"; 1 or 2: {100.0 * low / hits:F1}%");

            Assert.InRange(hits, expected * 85 / 100, expected * 115 / 100); // about 4.7 standard deviations
            Assert.True(low >= hits * 95 / 100, "intensity 1 or 2 is not almost always");
            Assert.True(hits - low > 0, "intensity 3 to 5 never came up at all");
            Assert.True(hits - low <= hits * 5 / 100, "intensity 3 to 5 is not rare");
        }
        finally
        {
            plate.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void OnlyAHighEndCorpseRollsAndArtifactsKeepTheirValue()
    {
        var plain = new Ettin();
        var paragon = new Ettin { IsParagon = true };
        var champion = new Barracoon();
        var tamed = new Ettin { IsParagon = true, Controlled = true };

        Assert.False(ClusterFRareSelfRepair.IsHighEndSource(plain));
        Assert.True(ClusterFRareSelfRepair.IsHighEndSource(paragon));
        Assert.True(ClusterFRareSelfRepair.IsHighEndSource(champion));
        Assert.False(ClusterFRareSelfRepair.IsHighEndSource(tamed));

        Container Corpse(BaseCreature owner, out BaseArmor magic, out BaseWeapon mundane, out Aegis aegis)
        {
            var c = new Backpack();
            magic = new PlateChest();
            magic.Attributes.Luck = 50;
            mundane = new Longsword();
            aegis = new Aegis();
            c.DropItem(magic);
            c.DropItem(mundane);
            c.DropItem(aegis);
            c.MoveToWorld(new Point3D(1600, 1600, 0), Map.Trammel);
            owner.Corpse = c;
            return c;
        }

        var plainCorpse = Corpse(plain, out var plainMagic, out _, out _);
        var paragonCorpse = Corpse(paragon, out var paragonMagic, out var paragonMundane, out var aegis);

        try
        {
            Assert.Equal(5, aegis.ArmorAttributes.SelfRepair);

            var original = BuiltInRng.Generator;
            BuiltInRng.Generator = new AlwaysZero();
            try
            {
                ClusterFRareSelfRepair.OnCreatureDeath(plain);
                ClusterFRareSelfRepair.OnCreatureDeath(paragon);
            }
            finally
            {
                BuiltInRng.Generator = original;
            }

            _out.WriteLine($"every roll hitting: ordinary corpse plate {plainMagic.ArmorAttributes.SelfRepair}; paragon corpse " +
                           $"plate {paragonMagic.ArmorAttributes.SelfRepair}, plain longsword {paragonMundane.WeaponAttributes.SelfRepair}, " +
                           $"Aegis {aegis.ArmorAttributes.SelfRepair}");

            Assert.Equal(0, plainMagic.ArmorAttributes.SelfRepair);
            Assert.Equal(1, paragonMagic.ArmorAttributes.SelfRepair); // Random(1000) = 0 is intensity 1
            Assert.Equal(0, paragonMundane.WeaponAttributes.SelfRepair);
            Assert.Equal(5, aegis.ArmorAttributes.SelfRepair);
        }
        finally
        {
            plainCorpse.Delete();
            paragonCorpse.Delete();
            plain.Delete();
            paragon.Delete();
            champion.Delete();
            tamed.Delete();
        }
    }
}
