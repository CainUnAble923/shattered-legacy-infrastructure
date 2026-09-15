using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Marksman/Swiftflight.cs (CC9 batch 5).
    //
    // ServUO derives from Bow and reads its set state off BaseWeapon. Bow sits on BaseRanged, which carries the
    // whole ranged-combat implementation (ammo, OnSwing, OnFired) and its own serialized fields, so none of the
    // S10 carriers can sit above it (Q-041). Per the Q-041 answer this is a one-off ISetItem implementer on Bow
    // rather than a sixth carrier: everything that reads a set item already dispatches on the interface, not on
    // a carrier class. AOS-set-attribute-aggregation.patch keys on `ISetItem { LastEquipped: true }`, and
    // SetHelper.GetSetProperties tests `setItem is BaseWeapon` first, which a Bow is. ServUO uses the same shape
    // itself where a piece cannot sit on a set base (BestialArms : GargishLeatherArms, ISetItem).
    //
    // The members are BaseSetWeapon's (S10), field for field, minus the absorption block: ServUO's Swiftflight
    // sets no SAAbsorptionAttributes, so IAbsorptionItem is not implemented. Feathernock (BaseSetQuiver, batch 4)
    // is the other half of the Marksman set, and with this file the set can complete.
    //
    // Dropped: IsArtifact (D-1). SetSelfRepair = 3 is stored and settable but inert on weapons here (Q-038; the
    // gap register's D-16), so the "self repair" tooltip line is not emitted, per the Aloron precedent.
    [SerializationGenerator(0, false)]
    public partial class Swiftflight : Bow, ISetItem
    {
        [SerializedIgnoreDupe]
        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, canModify: true)]
        private AosAttributes _setAttributes;

        [SerializableFieldSaveFlag(0)]
        private bool ShouldSerializeSetAttributes() => !_setAttributes.IsEmpty;

        [SerializableFieldDefault(0)]
        private AosAttributes SetAttributesDefaultValue() => new(this);

        [SerializedIgnoreDupe]
        [SerializableField(1, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, canModify: true)]
        private AosSkillBonuses _setSkillBonuses;

        [SerializableFieldSaveFlag(1)]
        private bool ShouldSerializeSetSkillBonuses() => !_setSkillBonuses.IsEmpty;

        [SerializableFieldDefault(1)]
        private AosSkillBonuses SetSkillBonusesDefaultValue() => new(this);

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(2)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setPhysicalBonus;

        [SerializableFieldSaveFlag(2)]
        private bool ShouldSerializeSetPhysicalBonus() => _setPhysicalBonus != 0;

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(3)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setFireBonus;

        [SerializableFieldSaveFlag(3)]
        private bool ShouldSerializeSetFireBonus() => _setFireBonus != 0;

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(4)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setColdBonus;

        [SerializableFieldSaveFlag(4)]
        private bool ShouldSerializeSetColdBonus() => _setColdBonus != 0;

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(5)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setPoisonBonus;

        [SerializableFieldSaveFlag(5)]
        private bool ShouldSerializeSetPoisonBonus() => _setPoisonBonus != 0;

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(6)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setEnergyBonus;

        [SerializableFieldSaveFlag(6)]
        private bool ShouldSerializeSetEnergyBonus() => _setEnergyBonus != 0;

        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(7)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setHue;

        [SerializableFieldSaveFlag(7)]
        private bool ShouldSerializeSetHue() => _setHue != 0;

        [SerializableField(8)]
        private bool _setEquipped;

        [SerializableFieldSaveFlag(8)]
        private bool ShouldSerializeSetEquipped() => _setEquipped;

        [SerializableField(9)]
        private bool _lastEquipped;

        [SerializableFieldSaveFlag(9)]
        private bool ShouldSerializeLastEquipped() => _lastEquipped;

        // Stored and settable, INERT: ServUO adds it to WeaponAttributes.SelfRepair inside BaseWeapon.OnHit
        // (BaseWeapon.cs:2320) and pinned ModernUO's OnHit reads SelfRepair with no hook. Same status as on
        // BaseSetWeapon (Q-038); the tooltip line is not emitted either.
        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(10)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setSelfRepair;

        [SerializableFieldSaveFlag(10)]
        private bool ShouldSerializeSetSelfRepair() => _setSelfRepair != 0;

        [Constructible]
        public Swiftflight() : base()
        {
            SetAttributes = new AosAttributes(this);
            SetSkillBonuses = new AosSkillBonuses(this);

            SetHue = 0x594;
            Attributes.WeaponDamage = 40;
            SetSelfRepair = 3;
            SetAttributes.AttackChance = 15;
            SetAttributes.BonusDex = 8;
            SetAttributes.WeaponSpeed = 30;
            SetAttributes.WeaponDamage = 20;
        }

        public override int LabelNumber => 1074308; // Swiftflight (Marksman Set)

        public SetItem SetID => SetItem.Marksman;

        public int Pieces => 2;

        public bool BardMasteryBonus => SetID == SetItem.Virtuoso;

        public bool IsSetItem => SetID != SetItem.None;

        public override void OnAdded(IEntity parent)
        {
            base.OnAdded(parent);

            if (parent is Mobile from && IsSetItem)
            {
                SetEquipped = SetHelper.FullSetEquipped(from, SetID, Pieces);

                if (_setEquipped)
                {
                    LastEquipped = true;
                    SetHelper.AddSetBonus(from, SetID);
                }
            }
        }

        public override void OnRemoved(IEntity parent)
        {
            if (parent is Mobile m && IsSetItem && _setEquipped)
            {
                SetHelper.RemoveSetBonus(m, SetID, this);
            }

            base.OnRemoved(parent);
        }

        public override void GetProperties(IPropertyList list)
        {
            if (IsSetItem)
            {
                list.Add(1073491, Pieces.ToString()); // Part of a Weapon/Armor Set (~1_val~ pieces)

                if (BardMasteryBonus)
                {
                    list.Add(1151553); // Activate: Bard Mastery Bonus x2<br>(Effect: 1 min. Cooldown: 30 min.)
                }

                if (_setEquipped)
                {
                    list.Add(1073492); // Full Weapon/Armor Set Present
                    GetSetProperties(list);
                }
            }

            base.GetProperties(list);

            if (IsSetItem && !_setEquipped)
            {
                list.Add(1072378); // <br>Only when full set is present:
                GetSetProperties(list);
            }
        }

        public virtual void GetSetProperties(IPropertyList list)
        {
            // ServUO adds "self repair ~1_val~" (1060450) here when SetSelfRepair != 0 and the weapon's own
            // SelfRepair is 0. Not emitted while the value is inert; see _setSelfRepair.

            SetHelper.GetSetProperties(list, this);
        }

        public int SetResistBonus(ResistanceType resist) =>
            resist switch
            {
                ResistanceType.Physical => PhysicalResistance,
                ResistanceType.Fire     => FireResistance,
                ResistanceType.Cold     => ColdResistance,
                ResistanceType.Poison   => PoisonResistance,
                ResistanceType.Energy   => EnergyResistance,
                _                       => 0
            };
    }
}
