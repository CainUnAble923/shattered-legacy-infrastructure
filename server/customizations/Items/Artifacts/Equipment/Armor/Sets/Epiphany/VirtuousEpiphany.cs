using ModernUO.Serialization;
using Server.Mobiles;
using System.Linq;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VirtuousEpiphany.cs (CC9 close, 2026-09-29). The twelve
    // good-aligned twins of VillainousEpiphany.cs (ours, batch 5), ported the same way on the same helper. Reached
    // only through Niporailem's unique list (Mobiles/Normal/Niporailem.cs, ported with it as its gate).
    //
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs); the pieces
    // are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    //
    // ServUO's values kept as it has them, including two that look like slips (bug-list section 3's kind, cosmetic):
    //   - EarringsOfVirtuousEpiphany carries 1150262, the kilt's number. Villainous uses a distinct one (1150260).
    //   - The four gargish body pieces (breastplate, arms, necklace, legs) have NO MageArmor; Villainous's do.
    // Unlike Villainous, no piece sets Resource = CraftResource.None: its six human pieces are plate, not dragon.
    [SerializationGenerator(0, false)]
    public partial class HelmOfVirtuousEpiphany : PlateHelm, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public HelmOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150233; // Helm of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GorgetOfVirtuousEpiphany : PlateGorget, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GorgetOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150234; // Gorget of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class BreastplateOfVirtuousEpiphany : PlateChest, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public BreastplateOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150235; // Breastplate of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class ArmsOfVirtuousEpiphany : PlateArms, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public ArmsOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150236; // Arms of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GauntletsOfVirtuousEpiphany : PlateGloves, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GauntletsOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150237; // Gauntlets of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class LegsOfVirtuousEpiphany : PlateLegs, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public LegsOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150238; // Leggings of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class KiltOfVirtuousEpiphany : GargishPlateKilt, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public KiltOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150262; // Kilt of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class EarringsOfVirtuousEpiphany : GargishEarrings, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public EarringsOfVirtuousEpiphany()
        {
            Hue = 2076;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150262; // Earrings of Virtuous Epiphany; ServUO's number, the kilt's (header)

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GargishBreastplateOfVirtuousEpiphany : GargishPlateChest, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GargishBreastplateOfVirtuousEpiphany()
        {
            Hue = 2076;
        }

        public override int LabelNumber => 1150235; // Breastplate of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GargishArmsOfVirtuousEpiphany : GargishPlateArms, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GargishArmsOfVirtuousEpiphany()
        {
            Hue = 2076;
        }

        public override int LabelNumber => 1150236; // Arms of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class NecklaceOfVirtuousEpiphany : GargishNecklace, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public NecklaceOfVirtuousEpiphany()
        {
            Hue = 2076;
        }

        public override int LabelNumber => 1150261; // Necklace of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GargishLegsOfVirtuousEpiphany : GargishPlateLegs, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Good;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GargishLegsOfVirtuousEpiphany()
        {
            Hue = 2076;
        }

        public override int LabelNumber => 1150238; // Legs of Virtuous Epiphany

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);

            EpiphanyHelper.AddProperties(this, list);
        }

        public override bool OnEquip(Mobile from)
        {
            var canEquip = base.OnEquip(from);

            if (canEquip)
            {
                foreach (var armor in from.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }

            return canEquip;
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                foreach (var armor in m.Items.Where(i => i is IEpiphanyArmor))
                {
                    armor.InvalidateProperties();
                }
            }
        }
    }
}
