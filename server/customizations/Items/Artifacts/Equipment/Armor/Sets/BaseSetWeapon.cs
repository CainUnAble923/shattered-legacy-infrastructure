using ModernUO.Serialization;

namespace Server.Items
{
    /// <summary>
    ///     A weapon that can carry a Mondain's Legacy set bonus (S10).
    ///     <para>
    ///         ServUO puts this state directly on <see cref="BaseWeapon" />, where every weapon carries
    ///         it and weapons with <see cref="SetItem.None" /> are inert. Doing that here would mean
    ///         bumping <see cref="BaseWeapon" />'s serialization version and writing a migration for a
    ///         file ModernUO maintains, so the state lives on this intermediate class instead, exactly
    ///         as <see cref="BaseSetArmor" /> does for armour. Set content derives from it; stock
    ///         ModernUO weapons are untouched. No stock ModernUO weapon is a set item, so nothing is
    ///         lost.
    ///     </para>
    ///     <para>
    ///         Everything that reads a set item is already kind-agnostic: <see cref="SetHelper" />
    ///         dispatches on <see cref="ISetItem" /> and branches on <see cref="BaseWeapon" /> for the
    ///         piece's own attributes, and the AOS aggregation patch keys on
    ///         <see cref="ISetItem.LastEquipped" />. This class adds nothing to either. Zero patches.
    ///     </para>
    ///     <para>See <c>shard-migration/notes/s10-carriers.md</c> for the deviations.</para>
    /// </summary>
    [SerializationGenerator(0, false)]
    public abstract partial class BaseSetWeapon : BaseWeapon, ISetItem, IAbsorptionItem
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

        /// <summary>
        ///     Stored and settable so that ported content compiles and the value survives a save, but
        ///     INERT: ServUO adds it to <c>WeaponAttributes.SelfRepair</c> inside
        ///     <c>BaseWeapon.OnHit</c>, and ModernUO's <c>BaseWeapon.OnHit</c> reads
        ///     <c>WeaponAttributes.SelfRepair</c> directly with no hook. Giving it effect needs a
        ///     three-line patch in the shape of <c>BaseArmor-set-self-repair.patch</c>, and S10 was
        ///     approved on zero patches, so the tooltip line is not emitted either: advertising a
        ///     bonus the player does not get is worse than omitting it (S1, D-2). Deviation D-1 in
        ///     <c>notes/s10-carriers.md</c>.
        /// </summary>
        [EncodedInt]
        [InvalidateProperties]
        [SerializableField(10)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _setSelfRepair;

        [SerializableFieldSaveFlag(10)]
        private bool ShouldSerializeSetSelfRepair() => _setSelfRepair != 0;

        /// <summary>
        ///     The Stygian Abyss absorption properties (S6). ServUO carries these on
        ///     <see cref="BaseWeapon" /> itself; here they ride on the carrier, as on
        ///     <see cref="BaseSetArmor" />.
        /// </summary>
        [SerializedIgnoreDupe]
        [SerializableField(11, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, canModify: true)]
        private SAAbsorptionAttributes _absorptionAttributes;

        [SerializableFieldSaveFlag(11)]
        private bool ShouldSerializeAbsorptionAttributes() => !_absorptionAttributes.IsEmpty;

        [SerializableFieldDefault(11)]
        private SAAbsorptionAttributes AbsorptionAttributesDefaultValue() => new(this);

        public BaseSetWeapon(int itemID) : base(itemID)
        {
            SetAttributes = new AosAttributes(this);
            SetSkillBonuses = new AosSkillBonuses(this);
            AbsorptionAttributes = new SAAbsorptionAttributes(this);
        }

        public virtual SetItem SetID => SetItem.None;

        public virtual int Pieces => 0;

        public virtual bool BardMasteryBonus => SetID == SetItem.Virtuoso;

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

        // No OnDragLift override: ServUO's BaseWeapon has none (its BaseArmor, BaseJewel,
        // BaseClothing and BaseQuiver all do). OnRemoved covers the lift once the item leaves the
        // mobile, so nothing is lost; matched to ServUO rather than "improved".

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

            // ServUO emits this block from inside BaseWeapon.AddNameProperties. Same clilocs, same
            // order within the block, a few lines lower in the tooltip. Cosmetic; the judgement is
            // D-4 in notes/s1-armour-sets.md.
            SAAbsorptionAttributes.GetProperties(list, _absorptionAttributes);

            if (IsSetItem && !_setEquipped)
            {
                list.Add(1072378); // <br>Only when full set is present:
                GetSetProperties(list);
            }
        }

        public virtual void GetSetProperties(IPropertyList list)
        {
            // ServUO adds "self repair ~1_val~" (1060450) here when SetSelfRepair != 0 and the
            // weapon's own SelfRepair is 0. Not emitted while the value is inert; see _setSelfRepair.

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
