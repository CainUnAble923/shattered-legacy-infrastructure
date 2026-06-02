using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.BulkOrders
{
    [SerializationGenerator(0, false)]
    public partial class LargeSmithBOD : LargeBOD
    {
        public static double[] m_BlacksmithMaterialChances =
        {
            0.501953125, // None
            0.250000000, // Dull Copper
            0.125000000, // Shadow Iron
            0.062500000, // Copper
            0.031250000, // Bronze
            0.015625000, // Gold
            0.007812500, // Agapite
            0.003906250, // Verite
            0.001953125  // Valorite
        };

        // ClusterF: post-Valorite material distribution for large BODs
        public static double[] m_PostValMaterialChances =
        {
            0.501953125, // Platinum
            0.250000000, // Toxic
            0.125000000, // Blaze
            0.062500000, // Frost
            0.031250000, // Obsidian
            0.015625000, // Mythril
            0.007812500, // Adamantium
            0.001953125  // Celestial
        };

        [Constructible]
        public LargeSmithBOD()
        {
            LargeBulkEntry[] entries;
            var useMaterials = true;

            var rand = Utility.Random(8);

            entries = rand switch
            {
                0 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeRing),
                1 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargePlate),
                2 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeChain),
                3 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeAxes),
                4 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeFencing),
                5 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeMaces),
                6 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargePolearms),
                7 => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeSwords),
                _ => LargeBulkEntry.ConvertEntries(this, LargeBulkEntry.LargeRing)
            };

            if (rand > 2 && rand < 8)
            {
                useMaterials = false;
            }

            var hue = 0x44E;
            var amountMax = Utility.RandomList(10, 15, 20, 20);
            var reqExceptional = Utility.RandomDouble() < 0.825;

            var material = useMaterials
                ? GetRandomMaterial(BulkMaterialType.DullCopper, m_BlacksmithMaterialChances)
                : BulkMaterialType.None;

            Hue = hue;
            AmountMax = amountMax;
            Entries = entries;
            RequireExceptional = reqExceptional;
            Material = material;
        }

        public LargeSmithBOD(int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries)
            : base(0x44E, amountMax, reqExceptional, mat, entries)
        {
        }

        // ClusterF: factory method matching SmallSmithBOD.CreateRandomFor pattern
        public static LargeSmithBOD CreateRandomFor(Mobile m)
        {
            var theirSkill = m.Skills.Blacksmith.Base;

            // Only smiths with sufficient skill get large BODs
            if (theirSkill < 70.1)
                return null;

            var bod = new LargeSmithBOD();

            // ClusterF: post-Valorite upgrade — 15% chance for extended smiths (skill > 100)
            if (bod.Material != BulkMaterialType.None && theirSkill > 100.0 && Utility.RandomDouble() < 0.15)
            {
                var postVal = GetRandomMaterial(BulkMaterialType.Platinum, m_PostValMaterialChances);
                var postValReq = postVal switch
                {
                    BulkMaterialType.Platinum   => 105.0,
                    BulkMaterialType.Toxic      => 115.0,
                    BulkMaterialType.Blaze      => 130.0,
                    BulkMaterialType.Frost      => 150.0,
                    BulkMaterialType.Obsidian   => 175.0,
                    BulkMaterialType.Mythril    => 200.0,
                    BulkMaterialType.Adamantium => 250.0,
                    BulkMaterialType.Celestial  => 300.0,
                    _ => 999.0
                };
                if (theirSkill >= postValReq)
                    bod.Material = postVal;
            }

            return bod;
        }

        public override int ComputeFame() => SmithRewardCalculator.Instance.ComputeFame(this);

        public override int ComputeGold() => SmithRewardCalculator.Instance.ComputeGold(this);

        public override RewardGroup GetRewardGroup() =>
            SmithRewardCalculator.Instance.LookupRewards(SmithRewardCalculator.Instance.ComputePoints(this));
    }
}
