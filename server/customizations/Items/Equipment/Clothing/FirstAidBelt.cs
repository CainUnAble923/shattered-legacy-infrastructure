using System;
using System.Linq;
using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/FirstAidBelt.cs (CC9 batch 5). A one-slot waist container that holds up to
    // 1,000 bandages of one kind, reduces their weight, and carries its own AosAttributes and a bandage healing
    // bonus. Three things about it on this shard, all logged (D-27):
    //   - HealingBonus is stored but INERT: ServUO reads it from Bandage.cs:493 (`m_HealingBonus += belt.HealingBonus`)
    //     and pinned ModernUO's bandage code has no such read. The tooltip line is suppressed (Aloron precedent).
    //   - Attributes are stored and shown, exactly as on ServUO, where AosAttributes.GetValue never aggregates a
    //     FirstAidBelt either (grep of Misc/AOS.cs: no reference). Behaviour identical; the lines are cosmetic on
    //     both emulators.
    //   - The container behaviour (bandage-only, one stack, weight reduction) is live and is the reason to port it.
    // ServUO saves at version 1 with the attributes behind a flag; version 0 here with the generator's own flags.
    [SerializationGenerator(0, false)]
    public partial class FirstAidBelt : Container
    {
        private static readonly Type[] _bandageTypes = { typeof(Bandage), typeof(EnhancedBandage) };

        [SerializedIgnoreDupe]
        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, canModify: true)]
        private AosAttributes _attributes;

        [SerializableFieldSaveFlag(0)]
        private bool ShouldSerializeAttributes() => !_attributes.IsEmpty;

        [SerializableFieldDefault(0)]
        private AosAttributes AttributesDefaultValue() => new(this);

        [InvalidateProperties]
        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _healingBonus;

        [InvalidateProperties]
        [SerializableField(2)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _weightReduction;

        [Constructible]
        public FirstAidBelt() : base(0xA1F6)
        {
            Layer = Layer.Waist;
            Attributes = new AosAttributes(this);
        }

        public override int LabelNumber => 1158681; // First Aid Belt

        public override int DefaultGumpID => 0x3C;
        public override int DefaultMaxItems => 1;
        public override int DefaultMaxWeight => 100;
        public override double DefaultWeight => 2.0;
        public override bool DisplaysContent => false;

        public Item Bandage => Items.Count > 0 ? Items[0] : null;
        public int MaxBandage => DefaultMaxWeight * 10;

        public override void OnAfterDuped(Item newItem)
        {
            if (newItem is FirstAidBelt belt)
            {
                belt.Attributes = new AosAttributes(newItem, _attributes);
            }

            base.OnAfterDuped(newItem);
        }

        public override void UpdateTotal(Item sender, TotalType type, int delta)
        {
            InvalidateProperties();

            base.UpdateTotal(sender, type, delta);
        }

        public override int GetTotal(TotalType type)
        {
            var total = base.GetTotal(type);

            if (type == TotalType.Weight)
            {
                total -= total * _weightReduction / 100;
            }

            return total;
        }

        public bool CheckType(Item item)
        {
            var type = item.GetType();
            var bandage = Bandage;

            if (bandage != null)
            {
                return bandage.GetType() == type;
            }

            for (var i = 0; i < _bandageTypes.Length; i++)
            {
                if (type == _bandageTypes[i])
                {
                    return true;
                }
            }

            return false;
        }

        public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
        {
            if (!Movable)
            {
                return false;
            }

            if (!CheckType(item))
            {
                if (message)
                {
                    m.SendLocalizedMessage(1074836); // The container can not hold that type of object.
                }

                return false;
            }

            if (!checkItems || Items.Count < DefaultMaxItems)
            {
                var currentAmount = Items.Sum(i => i.Amount);

                if (item.Amount + currentAmount <= MaxBandage)
                {
                    return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
                }

                m.SendLocalizedMessage(1080017); // That container cannot hold more items.
            }

            return false;
        }

        // ServUO also overrides Container.CheckStack(Mobile, Item) with the same type-and-amount gate. ModernUO has
        // no CheckStack: both of its stacking paths (Container.TryDropItem and OnStackAttempt) call CheckHold with
        // checkItems false before StackWith, and the override above applies the gate on that path too.

        public override void AddItem(Item dropped)
        {
            base.AddItem(dropped);
            InvalidateWeight();
        }

        public override void RemoveItem(Item dropped)
        {
            base.RemoveItem(dropped);
            InvalidateWeight();
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            // ServUO: "~1_VALUE~% Bandage Healing Bonus" (1158679) when _healingBonus > 0. Not emitted while
            // the value is inert; see the header.

            int prop;

            if ((prop = _attributes.DefendChance) != 0)
            {
                list.Add(1060408, prop.ToString()); // defense chance increase ~1_val~%
            }

            if ((prop = _attributes.BonusDex) != 0)
            {
                list.Add(1060409, prop.ToString()); // dexterity bonus ~1_val~
            }

            if ((prop = _attributes.EnhancePotions) != 0)
            {
                list.Add(1060411, prop.ToString()); // enhance potions ~1_val~%
            }

            if ((prop = _attributes.CastRecovery) != 0)
            {
                list.Add(1060412, prop.ToString()); // faster cast recovery ~1_val~
            }

            if ((prop = _attributes.CastSpeed) != 0)
            {
                list.Add(1060413, prop.ToString()); // faster casting ~1_val~
            }

            if ((prop = _attributes.AttackChance) != 0)
            {
                list.Add(1060415, prop.ToString()); // hit chance increase ~1_val~%
            }

            if ((prop = _attributes.BonusHits) != 0)
            {
                list.Add(1060431, prop.ToString()); // hit point increase ~1_val~
            }

            if ((prop = _attributes.BonusInt) != 0)
            {
                list.Add(1060432, prop.ToString()); // intelligence bonus ~1_val~
            }

            if ((prop = _attributes.LowerManaCost) != 0)
            {
                list.Add(1060433, prop.ToString()); // lower mana cost ~1_val~%
            }

            if ((prop = _attributes.LowerRegCost) != 0)
            {
                list.Add(1060434, prop.ToString()); // lower reagent cost ~1_val~%
            }

            if ((prop = _attributes.Luck) != 0)
            {
                list.Add(1060436, prop.ToString()); // luck ~1_val~
            }

            if ((prop = _attributes.BonusMana) != 0)
            {
                list.Add(1060439, prop.ToString()); // mana increase ~1_val~
            }

            if ((prop = _attributes.RegenMana) != 0)
            {
                list.Add(1060440, prop.ToString()); // mana regeneration ~1_val~
            }

            if (_attributes.NightSight != 0)
            {
                list.Add(1060441); // night sight
            }

            if ((prop = _attributes.ReflectPhysical) != 0)
            {
                list.Add(1060442, prop.ToString()); // reflect physical damage ~1_val~%
            }

            if ((prop = _attributes.RegenStam) != 0)
            {
                list.Add(1060443, prop.ToString()); // stamina regeneration ~1_val~
            }

            if ((prop = _attributes.RegenHits) != 0)
            {
                list.Add(1060444, prop.ToString()); // hit point regeneration ~1_val~
            }

            if ((prop = _attributes.SpellDamage) != 0)
            {
                list.Add(1060483, prop.ToString()); // spell damage increase ~1_val~%
            }

            if ((prop = _attributes.BonusStam) != 0)
            {
                list.Add(1060484, prop.ToString()); // stamina increase ~1_val~
            }

            if ((prop = _attributes.BonusStr) != 0)
            {
                list.Add(1060485, prop.ToString()); // strength bonus ~1_val~
            }

            if ((prop = _attributes.WeaponSpeed) != 0)
            {
                list.Add(1060486, prop.ToString()); // swing speed increase ~1_val~%
            }

            // ServUO also emits "Lower Ammo Cost ~1_Percentage~%" (1075208) for Attributes.LowerAmmoCost; ModernUO's
            // AosAttribute has no such member (it lives on BaseQuiver), so there is nothing to read here.

            double weight = 0;

            if (Bandage != null)
            {
                weight = Bandage.Weight * Bandage.Amount;
            }

            // Contents: ~1_COUNT~/~2_MAXCOUNT items, ~3_WEIGHT~/~4_MAXWEIGHT~ stones
            list.Add(1072241, $"{Items.Count}\t{DefaultMaxItems}\t{(int)weight}\t{DefaultMaxWeight}");

            if ((prop = _weightReduction) != 0)
            {
                list.Add(1072210, prop.ToString()); // Weight reduction: ~1_PERCENTAGE~%
            }
        }

        public void InvalidateWeight()
        {
            if (RootParent is Mobile m)
            {
                m.UpdateTotals();
            }
        }
    }
}
