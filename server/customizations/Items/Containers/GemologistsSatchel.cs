using System;
using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class GemologistsSatchel : Bag
    {
        // ServUO reads this list from Server.SkillHandlers.Imbuing.IngredTypes
        // (Scripts/Services/LootGeneration/Imbuing/Core/Imbuing.cs:1735). ModernUO has no Imbuing
        // skill handler, so the table lives at its only consumer on this shard. Contents are the
        // ServUO array verbatim, all 42 entries, in source order. If Imbuing is ever ported, this
        // array is the thing to delete. See notes/s2-fountain.md and Q-020.
        private static readonly Type[] IngredTypes =
        {
            typeof(MagicalResidue),     typeof(EnchantedEssence),       typeof(RelicFragment),

            typeof(SeedOfRenewal),      typeof(ChagaMushroom),          typeof(CrystalShards),
            typeof(BottleIchor),        typeof(ReflectiveWolfEye),      typeof(FaeryDust),
            typeof(BouraPelt),          typeof(SilverSnakeSkin),        typeof(ArcanicRuneStone),
            typeof(SlithTongue),        typeof(VoidOrb),                typeof(RaptorTeeth),
            typeof(SpiderCarapace),     typeof(DaemonClaw),             typeof(VialOfVitriol),
            typeof(GoblinBlood),        typeof(LavaSerpentCrust),       typeof(UndyingFlesh),
            typeof(CrushedGlass),       typeof(CrystallineBlackrock),   typeof(PowderedIron),
            typeof(ElvenFletching),     typeof(DelicateScales),

            typeof(EssenceSingularity), typeof(EssenceBalance),         typeof(EssencePassion),
            typeof(EssenceDirection),   typeof(EssencePrecision),       typeof(EssenceControl),
            typeof(EssenceDiligence),   typeof(EssenceAchievement),     typeof(EssenceFeeling),
            typeof(EssenceOrder),

            typeof(ParasiticPlant),     typeof(LuminescentFungi),
            typeof(FireRuby),           typeof(WhitePearl),             typeof(BlueDiamond),
            typeof(Turquoise)
        };

        [Constructible]
        public GemologistsSatchel()
        {
            Hue = 1177;

            DropItem(new Amber(Utility.RandomMinMax(10, 25)));
            DropItem(new Citrine(Utility.RandomMinMax(10, 25)));
            DropItem(new Ruby(Utility.RandomMinMax(10, 25)));
            DropItem(new Tourmaline(Utility.RandomMinMax(10, 25)));
            DropItem(new Amethyst(Utility.RandomMinMax(10, 25)));
            DropItem(new Emerald(Utility.RandomMinMax(10, 25)));
            DropItem(new Sapphire(Utility.RandomMinMax(10, 25)));
            DropItem(new StarSapphire(Utility.RandomMinMax(10, 25)));
            DropItem(new Diamond(Utility.RandomMinMax(10, 25)));

            for (var i = 0; i < 5; i++)
            {
                var type = IngredTypes[Utility.Random(IngredTypes.Length)];

                if (type == null)
                {
                    continue;
                }

                var item = Loot.Construct(type);

                if (item != null)
                {
                    item.Amount = Utility.RandomMinMax(5, 12);
                    DropItem(item);
                }
            }
        }

        public override int LabelNumber => 1113378; // Gemologist's Satchel
    }
}
