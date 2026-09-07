using System;

namespace Server
{
    /// <summary>
    ///     The Stygian Abyss absorption property set: damage eaters, resonance, and casting focus.
    ///     Ported from ServUO <c>Scripts/Misc/AOS.cs:2726-3058</c>.
    ///     <para>
    ///         <see cref="BaseAttributes" /> already exists in ModernUO, so the enum and the class are
    ///         purely additive. Nothing upstream changes to declare them.
    ///     </para>
    ///     <para>
    ///         The property itself does nothing without the damage-time hook. The eater is applied by
    ///         <c>DamageEaterContext</c> in <c>Abilities/SAPropEffects.cs</c>, reached from
    ///         <c>AOS.Damage</c> through <c>server/patches/AOS-damage-eater-hook.patch</c>.
    ///     </para>
    ///     <para>See <c>shard-migration/notes/s6-absorption.md</c> for the deviation list.</para>
    /// </summary>
    [Flags]
    public enum SAAbsorptionAttribute
    {
        EaterFire = 0x00000001,
        EaterCold = 0x00000002,
        EaterPoison = 0x00000004,
        EaterEnergy = 0x00000008,
        EaterKinetic = 0x00000010,
        EaterDamage = 0x00000020,
        ResonanceFire = 0x00000040,
        ResonanceCold = 0x00000080,
        ResonancePoison = 0x00000100,
        ResonanceEnergy = 0x00000200,
        ResonanceKinetic = 0x00000400,

        /*Soul Charge is wrong.
         * Do not use these types.
         * Use AosArmorAttribute type only.
         * Fill these in with any new attributes.*/
        SoulChargeFire = 0x00000800,
        SoulChargeCold = 0x00001000,
        SoulChargePoison = 0x00002000,
        SoulChargeEnergy = 0x00004000,
        SoulChargeKinetic = 0x00008000,
        CastingFocus = 0x00010000
    }

    /// <summary>
    ///     An item that carries <see cref="SAAbsorptionAttributes" />.
    ///     <para>
    ///         ServUO puts the property directly on <c>BaseArmor</c>, <c>BaseWeapon</c>,
    ///         <c>BaseJewel</c> and <c>BaseClothing</c>, and reads it back through
    ///         <c>RunicReforging.GetSAAbsorptionAttributes(Item)</c>, a four-branch type test.
    ///         Adding a field to those four ModernUO classes would bump four upstream serialization
    ///         versions and make four upstream migrations ours to maintain - the single most
    ///         expensive thing this repo can do to itself, and the reason S1 introduced
    ///         <c>BaseSetArmor</c> rather than touching <c>BaseArmor</c>.
    ///     </para>
    ///     <para>
    ///         So the type test becomes an interface test. Ported content that needs an eater
    ///         implements this; stock ModernUO items do not, and never could have - ModernUO's loot
    ///         generator has no absorption property to roll.
    ///     </para>
    /// </summary>
    public interface IAbsorptionItem
    {
        SAAbsorptionAttributes AbsorptionAttributes { get; }
    }

    public sealed class SAAbsorptionAttributes : BaseAttributes
    {
        public static bool IsValid(SAAbsorptionAttribute attribute) => Core.SA;

        public static int[] GetValues(Mobile m, params SAAbsorptionAttribute[] attributes)
        {
            var values = new int[attributes.Length];

            for (var i = 0; i < attributes.Length; i++)
            {
                values[i] = GetValue(m, attributes[i]);
            }

            return values;
        }

        /// <summary>
        ///     Sums one absorption attribute across everything the mobile has equipped.
        ///     <para>
        ///         ServUO (<c>Misc/AOS.cs:2780</c>) sums three terms here. Two of them are dropped,
        ///         and both were verified to contribute exactly zero rather than assumed to:
        ///     </para>
        ///     <list type="bullet">
        ///         <item>
        ///             <c>Enhancement.GetValue(m, attribute)</c> reads a dictionary of temporary
        ///             bonuses. The only writer that ever puts an <see cref="SAAbsorptionAttribute" />
        ///             into it is <c>Scripts/Abilities/EnhancementTimer.cs:62</c>, and
        ///             <c>new EnhancementTimer</c> appears nowhere in ServUO - it is dead code there
        ///             too. The term is zero on ServUO as well as here.
        ///         </item>
        ///         <item>
        ///             <c>SkillMasterySpell.GetAttributeBonus(m, attribute)</c>
        ///             (<c>Core/SkillMasterySpell.cs:1174</c>) is a switch with exactly one arm,
        ///             <see cref="SAAbsorptionAttribute.CastingFocus" />, fed by Perseverance. It
        ///             cannot return non-zero for any Eater or Resonance attribute in ServUO either.
        ///             If Skill Masteries is ever built, this term comes back for Casting Focus only.
        ///         </item>
        ///     </list>
        ///     <para>
        ///         The third term, <c>RunicReforging.GetSAAbsorptionAttributes(m.Items[i])</c>
        ///         (<c>RunicReforging.cs:2765-2780</c>), is not the reforging system: it is a
        ///         four-branch type test returning the item's own attributes. It is inlined here as
        ///         an <see cref="IAbsorptionItem" /> test, per the interface's own remarks.
        ///     </para>
        /// </summary>
        public static int GetValue(Mobile m, SAAbsorptionAttribute attribute)
        {
            if (m == null || World.Loading || !IsValid(attribute))
            {
                return 0;
            }

            var value = 0;
            var items = m.Items;

            for (var i = 0; i < items.Count; ++i)
            {
                if (items[i] is IAbsorptionItem { AbsorptionAttributes: not null } absorptionItem)
                {
                    value += absorptionItem.AbsorptionAttributes[attribute];
                }
            }

            return value;
        }

        /// <summary>
        ///     The tooltip block ServUO emits from <c>BaseArmor.GetProperties</c>
        ///     (<c>Items/Equipment/Armor/BaseArmor.cs:2905-2939</c>), verbatim and in the same order.
        ///     Carriers call this so the twelve lines are written once rather than per item kind.
        /// </summary>
        public static void GetProperties(IPropertyList list, SAAbsorptionAttributes attrs)
        {
            if (attrs == null)
            {
                return;
            }

            int prop;

            if ((prop = attrs.EaterFire) != 0)
            {
                list.Add(1113593, prop.ToString()); // Fire Eater ~1_Val~%
            }

            if ((prop = attrs.EaterCold) != 0)
            {
                list.Add(1113594, prop.ToString()); // Cold Eater ~1_Val~%
            }

            if ((prop = attrs.EaterPoison) != 0)
            {
                list.Add(1113595, prop.ToString()); // Poison Eater ~1_Val~%
            }

            if ((prop = attrs.EaterEnergy) != 0)
            {
                list.Add(1113596, prop.ToString()); // Energy Eater ~1_Val~%
            }

            if ((prop = attrs.EaterKinetic) != 0)
            {
                list.Add(1113597, prop.ToString()); // Kinetic Eater ~1_Val~%
            }

            if ((prop = attrs.EaterDamage) != 0)
            {
                list.Add(1113598, prop.ToString()); // Damage Eater ~1_Val~%
            }

            if ((prop = attrs.ResonanceFire) != 0)
            {
                list.Add(1113691, prop.ToString()); // Fire Resonance ~1_val~%
            }

            if ((prop = attrs.ResonanceCold) != 0)
            {
                list.Add(1113692, prop.ToString()); // Cold Resonance ~1_val~%
            }

            if ((prop = attrs.ResonancePoison) != 0)
            {
                list.Add(1113693, prop.ToString()); // Poison Resonance ~1_val~%
            }

            if ((prop = attrs.ResonanceEnergy) != 0)
            {
                list.Add(1113694, prop.ToString()); // Energy Resonance ~1_val~%
            }

            if ((prop = attrs.ResonanceKinetic) != 0)
            {
                list.Add(1113695, prop.ToString()); // Kinetic Resonance ~1_val~%
            }

            if ((prop = attrs.CastingFocus) != 0)
            {
                list.Add(1113696, prop.ToString()); // Casting Focus ~1_val~%
            }
        }

        public SAAbsorptionAttributes(Item owner) : base(owner)
        {
        }

        public SAAbsorptionAttributes(Item owner, SAAbsorptionAttributes other) : base(owner, other)
        {
        }

        public int this[SAAbsorptionAttribute attribute]
        {
            get => GetValue((int)attribute);
            set => SetValue((int)attribute, value);
        }

        public override string ToString() => "...";

        [CommandProperty(AccessLevel.GameMaster)]
        public int EaterFire
        {
            get => this[SAAbsorptionAttribute.EaterFire];
            set => this[SAAbsorptionAttribute.EaterFire] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int EaterCold
        {
            get => this[SAAbsorptionAttribute.EaterCold];
            set => this[SAAbsorptionAttribute.EaterCold] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int EaterPoison
        {
            get => this[SAAbsorptionAttribute.EaterPoison];
            set => this[SAAbsorptionAttribute.EaterPoison] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int EaterEnergy
        {
            get => this[SAAbsorptionAttribute.EaterEnergy];
            set => this[SAAbsorptionAttribute.EaterEnergy] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int EaterKinetic
        {
            get => this[SAAbsorptionAttribute.EaterKinetic];
            set => this[SAAbsorptionAttribute.EaterKinetic] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int EaterDamage
        {
            get => this[SAAbsorptionAttribute.EaterDamage];
            set => this[SAAbsorptionAttribute.EaterDamage] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ResonanceFire
        {
            get => this[SAAbsorptionAttribute.ResonanceFire];
            set => this[SAAbsorptionAttribute.ResonanceFire] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ResonanceCold
        {
            get => this[SAAbsorptionAttribute.ResonanceCold];
            set => this[SAAbsorptionAttribute.ResonanceCold] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ResonancePoison
        {
            get => this[SAAbsorptionAttribute.ResonancePoison];
            set => this[SAAbsorptionAttribute.ResonancePoison] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ResonanceEnergy
        {
            get => this[SAAbsorptionAttribute.ResonanceEnergy];
            set => this[SAAbsorptionAttribute.ResonanceEnergy] = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int ResonanceKinetic
        {
            get => this[SAAbsorptionAttribute.ResonanceKinetic];
            set => this[SAAbsorptionAttribute.ResonanceKinetic] = value;
        }

        // Soul Charge is deliberately read-only in ServUO: the setters are commented out there
        // because the working property is AosArmorAttribute.SoulCharge. Kept identical so the enum
        // members stay in place for anything that reads them.
        public int SoulChargeFire
        {
            get => this[SAAbsorptionAttribute.SoulChargeFire];
            set { }
        }

        public int SoulChargeCold
        {
            get => this[SAAbsorptionAttribute.SoulChargeCold];
            set { }
        }

        public int SoulChargePoison
        {
            get => this[SAAbsorptionAttribute.SoulChargePoison];
            set { }
        }

        public int SoulChargeEnergy
        {
            get => this[SAAbsorptionAttribute.SoulChargeEnergy];
            set { }
        }

        public int SoulChargeKinetic
        {
            get => this[SAAbsorptionAttribute.SoulChargeKinetic];
            set { }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int CastingFocus
        {
            get => this[SAAbsorptionAttribute.CastingFocus];
            set => this[SAAbsorptionAttribute.CastingFocus] = value;
        }
    }
}
