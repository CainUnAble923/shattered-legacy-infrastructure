// cc-P69, F-33 Parts C and D: a mule's bloodline (stat quality, traits, lineage) and how a foal inherits it.
// The mule and the foal deed each save these as their own fields; this class carries them between the two and holds
// the inheritance rules. Odds are in MuleBreeding (MuleData.cs).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Mobiles
{
    public sealed class MuleGenes
    {
        public bool IsMale;

        // Stat quality, 0-100 each: where the mule sits in its tier's range for that stat.
        public int QualityHits;
        public int QualityStr;
        public int QualityStam;
        public int QualityResist;
        public int QualityPack;

        public List<(MuleTrait Id, int Grade)> Traits = new();

        public string FamilyName;
        public int Generation;
        public uint Parent1;
        public uint Parent2;

        // How many generations in a row, before this one, reached tier 5 (the Legendary count it was born with).
        public int InheritedTier5;

        public static MuleGenes Fresh() =>
            new()
            {
                IsMale = Utility.RandomBool(),
                QualityHits = Utility.Random(101),
                QualityStr = Utility.Random(101),
                QualityStam = Utility.Random(101),
                QualityResist = Utility.Random(101),
                QualityPack = Utility.Random(101)
            };

        // --- saved form of the traits: id * 10 + grade, at most three ---------------------------------------------

        public int[] EncodeTraits() => Traits.Select(t => (int)t.Id * 10 + t.Grade).ToArray();

        public static List<(MuleTrait, int)> DecodeTraits(int[] saved)
        {
            var list = new List<(MuleTrait, int)>();

            if (saved == null)
            {
                return list;
            }

            foreach (var v in saved)
            {
                var id = (MuleTrait)(v / 10);
                var grade = v % 10;

                if (id != MuleTrait.None && grade is >= 1 and <= MuleData.MaxGrade && list.Count < MuleData.MaxTraits &&
                    list.All(t => t.Item1 != id))
                {
                    list.Add((id, grade));
                }
            }

            return list;
        }

        public int GradeOf(MuleTrait id) => Traits.FirstOrDefault(t => t.Id == id).Grade;

        public string TraitsText() =>
            Traits.Count == 0
                ? "none"
                : string.Join(", ", Traits.Select(t => $"{MuleData.Trait(t.Id)?.Name} {MuleData.GradeName(t.Grade)}"));

        // --- inheritance ----------------------------------------------------------------------------------------------

        /// <summary>
        /// A foal of two mules (the genes are rolled now, when the deed is made). Lore is the breeder's Animal Lore.
        /// Foals start at tier 1; the parents' tiers cap the grades they can pass.
        /// </summary>
        public static MuleGenes Breed(PackMule a, PackMule b, double lore)
        {
            var lf = MuleBreeding.LoreFactor(lore);
            var foal = new MuleGenes
            {
                IsMale = Utility.RandomBool(),
                QualityHits = Inherit(a.QualityHits, b.QualityHits, lf),
                QualityStr = Inherit(a.QualityStr, b.QualityStr, lf),
                QualityStam = Inherit(a.QualityStam, b.QualityStam, lf),
                QualityResist = Inherit(a.QualityResist, b.QualityResist, lf),
                QualityPack = Inherit(a.QualityPack, b.QualityPack, lf),
                Generation = Math.Max(a.Generation, b.Generation) + 1,
                Parent1 = (uint)a.Serial,
                Parent2 = (uint)b.Serial,
                InheritedTier5 = a.Tier == MuleData.MaxTier && b.Tier == MuleData.MaxTier
                    ? Math.Max(a.LineTier5Generations, b.LineTier5Generations)
                    : 0
            };

            foal.Traits = InheritTraits(a.Genes, b.Genes, Math.Min(a.Tier, b.Tier), lf);
            foal.FamilyName = FamilyFor(a, b);
            return foal;
        }

        private static int Inherit(int qa, int qb, double lf)
        {
            if (Utility.RandomDouble() < MuleBreeding.BetterParentChance + lf * MuleBreeding.BetterParentLoreBonus)
            {
                return Math.Max(qa, qb);
            }

            var roll = Utility.RandomMinMax(-MuleBreeding.QualityRoll, MuleBreeding.QualityRoll);
            return Math.Clamp((qa + qb + 1) / 2 + roll, 0, 100);
        }

        public static List<(MuleTrait Id, int Grade)> InheritTraits(MuleGenes a, MuleGenes b, int lowerParentTier, double lf)
        {
            var cap = lowerParentTier >= MuleBreeding.GradeIIIMinParentTier ? 3 : 2;
            var bothTier5 = lowerParentTier >= MuleBreeding.CelestialTemperedParentTier;

            // (trait, grade, from both parents) candidates
            var picked = new List<(MuleTrait Id, int Grade, bool Both)>();

            foreach (var id in a.Traits.Select(t => t.Id).Union(b.Traits.Select(t => t.Id)))
            {
                if (id == MuleTrait.CelestialTempered && !bothTier5)
                {
                    continue; // only two tier 5 parents pass it on
                }

                var ga = a.GradeOf(id);
                var gb = b.GradeOf(id);
                var both = ga > 0 && gb > 0;
                var chance = (both ? MuleBreeding.TraitFromBothParents : MuleBreeding.TraitFromOneParent) +
                             lf * MuleBreeding.TraitLoreBonus;

                if (Utility.RandomDouble() >= chance)
                {
                    continue;
                }

                var grade = Math.Max(ga, gb);

                if (both && Utility.RandomDouble() < MuleBreeding.GradeUpChance + lf * MuleBreeding.GradeUpLoreBonus)
                {
                    grade++;
                }

                picked.Add((id, Math.Min(grade, cap), both));
            }

            // A new grade I trait now and then (never Celestial Tempered this way).
            if (Utility.RandomDouble() < MuleBreeding.NewTraitChance + lf * MuleBreeding.NewTraitLoreBonus)
            {
                var fresh = MuleData.Traits.Select(t => t.Id)
                    .Where(id => id != MuleTrait.CelestialTempered && picked.All(p => p.Id != id)).ToArray();

                if (fresh.Length > 0)
                {
                    picked.Add((fresh[Utility.Random(fresh.Length)], 1, false));
                }
            }

            // Celestial Tempered: rarely, and only from two tier 5 parents.
            if (bothTier5 && picked.All(p => p.Id != MuleTrait.CelestialTempered) &&
                Utility.RandomDouble() < MuleBreeding.CelestialTemperedChance)
            {
                picked.Add((MuleTrait.CelestialTempered, 1, false));
            }

            // More than three: the highest grades, then those both parents carried, then at random.
            return picked
                .OrderByDescending(p => p.Grade)
                .ThenByDescending(p => p.Both)
                .ThenBy(_ => Utility.RandomDouble())
                .Take(MuleData.MaxTraits)
                .Select(p => (p.Id, p.Grade))
                .ToList();
        }

        // The family name runs down the line: the higher-tier parent's, the dam's (female) on a tie. A line with none
        // draws a new one from MuleData.FamilyNames.
        private static string FamilyFor(PackMule a, PackMule b)
        {
            var first = a.Tier != b.Tier ? (a.Tier > b.Tier ? a : b) : (a.IsMale ? b : a);
            var second = first == a ? b : a;

            if (!string.IsNullOrEmpty(first.FamilyName))
            {
                return first.FamilyName;
            }

            if (!string.IsNullOrEmpty(second.FamilyName))
            {
                return second.FamilyName;
            }

            return MuleData.FamilyNames[Utility.Random(MuleData.FamilyNames.Length)];
        }
    }
}
