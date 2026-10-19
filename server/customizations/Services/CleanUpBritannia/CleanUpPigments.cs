// cc-P175 Part C 2: Clean Up Britannia's 24 pigments (the 250,000 tier), ported from ServUO pub57
// Services/CleanUpBritannia/Items/Pigments/CompassionPigment.cs (the pigment, its 28 kinds and their hue and cliloc table)
// and CleanupPigments.cs (one class per store pigment). Generated from those two files: the kinds, hues and clilocs are
// ServUO's, in ServUO's order. A pigment dyes an artifact or enhanced magic item in the backpack, as Pigments of Tokuno
// do: the same item rule (pinned BasePigmentsOfTokuno.IsValidItem, BasePigmentsOfTokuno.cs:188) and the same refusals
// (another pigment; locked down). 5 uses (ServUO's default).
//
// Changes from ServUO: [SerializationGenerator] in place of its hand-written version 0 (the same two fields, kind and
// uses); Weight -> DefaultWeight; uses in GetProperties (pinned's IPropertyList) in place of AddUsesRemainingProperties.

using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items
{
    public enum CompassionPigmentType
    {
        PhoenixRed,
        AuraOfAmber,
        DeepViolet,
        PolishedBronze,
        VibrantCrimson,
        Lavender,
        GleamingFuchsia,
        DeepBlue,
        GlossyFuchsia,
        DarkVoid,
        MurkySeagreen,
        ReflectiveShadow,
        LiquidSunshine,
        ShadowyBlue,
        BlackAndGreen,
        GlossyBlue,
        HunterGreen,
        SlateBlue,
        MotherOfPearl,
        StarBlue,
        MurkyAmber,
        VibranSeagreen,
        VibrantOcher,
        MossyGreen,
        OliveGreen,
        MottledSunsetBlue,
        TyrianPurple,
        IntenseTeal,
    }

    public static class CompassionPigmentInfo
    {
        // ServUO CompassionPigment.cs, the table: kind, hue, cliloc.
        private static readonly (int Hue, int LabelNumber)[] _table =
        [
            (1964, 1151651), // PhoenixRed: Phoenix Red Pigment
            (1967, 1152308), // AuraOfAmber: Aura of Amber Pigment
            (1929, 1151912), // DeepViolet: Deep Violet Pigment
            (1944, 1151909), // PolishedBronze: Polished Bronze Pigment
            (1964, 1153386), // VibrantCrimson: Vibrant Crimson Pigment
            (1951, 1151650), // Lavender: Lavender Pigment
            (1930, 1152311), // GleamingFuchsia: Gleaming Fuchsia Pigment
            (1939, 1152348), // DeepBlue: Deep Blue Pigment
            (1919, 1152347), // GlossyFuchsia: Glossy Fuchsia Pigment
            (2068, 1154214), // DarkVoid: Dark Void Pigment
            (1992, 1152309), // MurkySeagreen: Murky Seagreen Pigment
            (1910, 1153387), // ReflectiveShadow: Reflective Shadow Pigment
            (1923, 1154213), // LiquidSunshine: Liquid Sunshine Pigment
            (1960, 1152310), // ShadowyBlue: Shadowy Blue Pigment
            (1979, 1151911), // BlackAndGreen: Black And Green Pigment
            (1916, 1151910), // GlossyBlue: Glossy Blue Pigment
            (1936, 1151649), // HunterGreen: Hunter Green Pigment
            (1983, 1151653), // SlateBlue: Slate Blue Pigment
            (2720, 1154120), // MotherOfPearl: Mother Of Pearl Pigment
            (2723, 1154121), // StarBlue: Star Blue Pigment
            (1989, 1152350), // MurkyAmber: Murky Amber Pigment
            (1970, 1152349), // VibranSeagreen: Vibran Seagreen Pigment
            (2725, 1154736), // VibrantOcher: Vibrant Ocher Pigment
            (2684, 1154731), // MossyGreen: Mossy Green Pigment
            (2709, 1154733), // OliveGreen: Olive Green Pigment
            (2714, 1154734), // MottledSunsetBlue: Mottled Sunset Blue Pigment
            (2716, 1154735), // TyrianPurple: Tyrian Purple Pigment
            (2691, 1154732), // IntenseTeal: Intense Teal Pigment
        ];

        public static (int Hue, int LabelNumber) Get(CompassionPigmentType type)
        {
            var v = (int)type;
            return _table[v < 0 || v >= _table.Length ? 0 : v];
        }
    }

    [SerializationGenerator(0, false)]
    public partial class CompassionPigment : Item, IUsesRemaining
    {
        [SerializableField(0, setter: "private")]
        private CompassionPigmentType _type;

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _usesRemaining;

        [Constructible]
        public CompassionPigment() : this((CompassionPigmentType)Utility.Random(28))
        {
        }

        [Constructible]
        public CompassionPigment(CompassionPigmentType type, int uses = 5) : base(0xEFF)
        {
            _type = type;
            _usesRemaining = uses;
            Hue = CompassionPigmentInfo.Get(type).Hue;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public CompassionPigmentType Kind
        {
            get => _type;
            set
            {
                Type = value;
                Hue = CompassionPigmentInfo.Get(value).Hue;
                InvalidateProperties();
            }
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => CompassionPigmentInfo.Get(_type).LabelNumber;

        bool IUsesRemaining.ShowUsesRemaining
        {
            get => true;
            set { }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add(1060584, _usesRemaining); // uses remaining: ~1_val~
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendLocalizedMessage(1070929); // Select the artifact or enhanced magic item to dye.
            from.Target = new DyeTarget(this);
        }

        // ServUO's DyeTarget, unchanged in its order of checks.
        private class DyeTarget : Target
        {
            private readonly CompassionPigment _pigment;

            public DyeTarget(CompassionPigment pigment) : base(8, false, TargetFlags.None) => _pigment = pigment;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is not Item item || _pigment.Deleted)
                {
                    return;
                }

                if (!item.IsChildOf(from.Backpack))
                {
                    from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
                }
                else if (item is MetalPigmentsOfTokuno or LesserPigmentsOfTokuno or PigmentsOfTokuno or CompassionPigment)
                {
                    from.SendLocalizedMessage(1042083); // You cannot dye that.
                }
                else if (item.IsLockedDown)
                {
                    from.SendLocalizedMessage(1070932); // You may not dye artifacts and enhanced magic items which are locked down.
                }
                else if (BasePigmentsOfTokuno.IsValidItem(item))
                {
                    item.Hue = _pigment.Hue;

                    if (--_pigment.UsesRemaining <= 0)
                    {
                        _pigment.Delete();
                    }
                }
                else
                {
                    from.SendLocalizedMessage(1070931); // You can only dye artifacts and enhanced magic items with this tub.
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class IntenseTealPigment : CompassionPigment
    {
        [Constructible]
        public IntenseTealPigment() : base(CompassionPigmentType.IntenseTeal)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class TyrianPurplePigment : CompassionPigment
    {
        [Constructible]
        public TyrianPurplePigment() : base(CompassionPigmentType.TyrianPurple)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class MottledSunsetBluePigment : CompassionPigment
    {
        [Constructible]
        public MottledSunsetBluePigment() : base(CompassionPigmentType.MottledSunsetBlue)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class MossyGreenPigment : CompassionPigment
    {
        [Constructible]
        public MossyGreenPigment() : base(CompassionPigmentType.MossyGreen)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class VibrantOcherPigment : CompassionPigment
    {
        [Constructible]
        public VibrantOcherPigment() : base(CompassionPigmentType.VibrantOcher)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class OliveGreenPigment : CompassionPigment
    {
        [Constructible]
        public OliveGreenPigment() : base(CompassionPigmentType.OliveGreen)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class PolishedBronzePigment : CompassionPigment
    {
        [Constructible]
        public PolishedBronzePigment() : base(CompassionPigmentType.PolishedBronze)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GlossyBluePigment : CompassionPigment
    {
        [Constructible]
        public GlossyBluePigment() : base(CompassionPigmentType.GlossyBlue)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class BlackAndGreenPigment : CompassionPigment
    {
        [Constructible]
        public BlackAndGreenPigment() : base(CompassionPigmentType.BlackAndGreen)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class DeepVioletPigment : CompassionPigment
    {
        [Constructible]
        public DeepVioletPigment() : base(CompassionPigmentType.DeepViolet)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class AuraOfAmberPigment : CompassionPigment
    {
        [Constructible]
        public AuraOfAmberPigment() : base(CompassionPigmentType.AuraOfAmber)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class MurkySeagreenPigment : CompassionPigment
    {
        [Constructible]
        public MurkySeagreenPigment() : base(CompassionPigmentType.MurkySeagreen)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class ShadowyBluePigment : CompassionPigment
    {
        [Constructible]
        public ShadowyBluePigment() : base(CompassionPigmentType.ShadowyBlue)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GleamingFuchsiaPigment : CompassionPigment
    {
        [Constructible]
        public GleamingFuchsiaPigment() : base(CompassionPigmentType.GleamingFuchsia)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GlossyFuchsiaPigment : CompassionPigment
    {
        [Constructible]
        public GlossyFuchsiaPigment() : base(CompassionPigmentType.GlossyFuchsia)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class DeepBluePigment : CompassionPigment
    {
        [Constructible]
        public DeepBluePigment() : base(CompassionPigmentType.DeepBlue)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class VibranSeagreenPigment : CompassionPigment
    {
        [Constructible]
        public VibranSeagreenPigment() : base(CompassionPigmentType.VibranSeagreen)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class MurkyAmberPigment : CompassionPigment
    {
        [Constructible]
        public MurkyAmberPigment() : base(CompassionPigmentType.MurkyAmber)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class VibrantCrimsonPigment : CompassionPigment
    {
        [Constructible]
        public VibrantCrimsonPigment() : base(CompassionPigmentType.VibrantCrimson)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class ReflectiveShadowPigment : CompassionPigment
    {
        [Constructible]
        public ReflectiveShadowPigment() : base(CompassionPigmentType.ReflectiveShadow)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class StarBluePigment : CompassionPigment
    {
        [Constructible]
        public StarBluePigment() : base(CompassionPigmentType.StarBlue)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class MotherOfPearlPigment : CompassionPigment
    {
        [Constructible]
        public MotherOfPearlPigment() : base(CompassionPigmentType.MotherOfPearl)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class LiquidSunshinePigment : CompassionPigment
    {
        [Constructible]
        public LiquidSunshinePigment() : base(CompassionPigmentType.LiquidSunshine)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class DarkVoidPigment : CompassionPigment
    {
        [Constructible]
        public DarkVoidPigment() : base(CompassionPigmentType.DarkVoid)
        {
        }
    }

}
