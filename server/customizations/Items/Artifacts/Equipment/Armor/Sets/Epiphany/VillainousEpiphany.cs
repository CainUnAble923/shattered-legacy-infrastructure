using ModernUO.Serialization;
using Server.Mobiles;
using System.Linq;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class HelmOfVillainousEpiphany : DragonHelm, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public HelmOfVillainousEpiphany()
        {
            Resource = CraftResource.None;
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150253;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class GorgetOfVillainousEpiphany : PlateGorget, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GorgetOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150254;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class BreastplateOfVillainousEpiphany : DragonChest, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public BreastplateOfVillainousEpiphany()
        {
            Resource = CraftResource.None;
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150255;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class ArmsOfVillainousEpiphany : DragonArms, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public ArmsOfVillainousEpiphany()
        {
            Resource = CraftResource.None;
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150256;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class GauntletsOfVillainousEpiphany : DragonGloves, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GauntletsOfVillainousEpiphany()
        {
            Resource = CraftResource.None;
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150257;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class LegsOfVillainousEpiphany : DragonLegs, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public LegsOfVillainousEpiphany()
        {
            Resource = CraftResource.None;
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150258;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class KiltOfVillainousEpiphany : GargishPlateKilt, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public KiltOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150263;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class EarringsOfVillainousEpiphany : GargishEarrings, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public EarringsOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150260;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class GargishBreastplateOfVillainousEpiphany : GargishPlateChest, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GargishBreastplateOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150255;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class GargishArmsOfVillainousEpiphany : GargishPlateArms, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GargishArmsOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150256;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class NecklaceOfVillainousEpiphany : GargishNecklace, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public NecklaceOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150264;

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

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/VillainousEpiphany.cs (CC9 batch 5).
    // The Epiphany burst mechanic is not hooked into damage or karma change (D-24, see EpiphanyHelper.cs);
    // the pieces are armour with MageArmor until then, and EpiphanyHelper.AddProperties emits nothing.
    [SerializationGenerator(0, false)]
    public partial class GargishLegsOfVillainousEpiphany : GargishPlateLegs, IEpiphanyArmor
    {
        public Alignment Alignment => Alignment.Evil;
        public SurgeType Type => SurgeType.Mana;
        public int Frequency => EpiphanyHelper.GetFrequency(Parent as Mobile, this);
        public int Bonus => EpiphanyHelper.GetBonus(Parent as Mobile, this);

        [Constructible]
        public GargishLegsOfVillainousEpiphany()
        {
            Hue = 1778;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1150258;

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
