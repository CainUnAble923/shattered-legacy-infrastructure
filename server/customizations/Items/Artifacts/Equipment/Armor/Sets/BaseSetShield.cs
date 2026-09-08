using ModernUO.Serialization;

namespace Server.Items
{
    /// <summary>
    ///     A shield that can carry a Mondain's Legacy set bonus (S10).
    ///     <para>
    ///         In ServUO a shield is a <c>BaseArmor</c> and inherits the set state from it. In
    ///         ModernUO <see cref="BaseShield" /> is also a <see cref="BaseArmor" />, but our set state
    ///         lives on <see cref="BaseSetArmor" />, a sibling of <see cref="BaseShield" /> rather than
    ///         an ancestor, and C# has no way to be both. So this is <see cref="BaseSetArmor" />
    ///         against <see cref="BaseShield" />: the same fields in the same order, the same hooks,
    ///         the same tooltip. Keep the two in step.
    ///     </para>
    ///     <para>
    ///         The one difference: no <c>EffectiveSelfRepair</c> override. <see cref="BaseShield" />
    ///         overrides <c>OnHit</c> wholesale and reads <c>ArmorAttributes.SelfRepair</c> itself, so
    ///         the hook <c>BaseArmor-set-self-repair.patch</c> adds is never reached for a shield.
    ///         <c>SetSelfRepair</c> is therefore stored but inert here, as on
    ///         <see cref="BaseSetWeapon" />; see the remarks on <c>_setSelfRepair</c>.
    ///     </para>
    ///     <para>See <c>shard-migration/notes/s10-carriers.md</c> for the deviations.</para>
    /// </summary>
    [SerializationGenerator(0, false)]
    public abstract partial class BaseSetShield : BaseShield, ISetItem, IAbsorptionItem
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
        ///     INERT: ModernUO's <c>BaseShield.OnHit</c> reads <c>ArmorAttributes.SelfRepair</c>
        ///     directly and never consults <c>BaseArmor.EffectiveSelfRepair</c>. Giving it effect
        ///     needs a one-line patch to <c>BaseShield.OnHit</c>, and S10 was approved on zero
        ///     patches, so the tooltip line is not emitted either (S1, D-2). Deviation D-1 in
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
        ///     The Stygian Abyss absorption properties (S6), inherited by every ServUO shield from
        ///     its <c>BaseArmor</c>; here they ride on the carrier, as on <see cref="BaseSetArmor" />.
        /// </summary>
        [SerializedIgnoreDupe]
        [SerializableField(11, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, canModify: true)]
        private SAAbsorptionAttributes _absorptionAttributes;

        [SerializableFieldSaveFlag(11)]
        private bool ShouldSerializeAbsorptionAttributes() => !_absorptionAttributes.IsEmpty;

        [SerializableFieldDefault(11)]
        private SAAbsorptionAttributes AbsorptionAttributesDefaultValue() => new(this);

        public BaseSetShield(int itemID) : base(itemID)
        {
            SetAttributes = new AosAttributes(this);
            SetSkillBonuses = new AosSkillBonuses(this);
            AbsorptionAttributes = new SAAbsorptionAttributes(this);
        }

        public virtual SetItem SetID => SetItem.None;

        public virtual bool MixedSet => false;

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

        public override bool OnDragLift(Mobile from)
        {
            if (Parent is Mobile && from == Parent)
            {
                if (IsSetItem && _setEquipped)
                {
                    SetHelper.RemoveSetBonus(from, SetID, this);
                }
            }

            return base.OnDragLift(from);
        }

        public override void GetProperties(IPropertyList list)
        {
            if (IsSetItem)
            {
                if (MixedSet)
                {
                    list.Add(1073491, Pieces.ToString()); // Part of a Weapon/Armor Set (~1_val~ pieces)
                }
                else
                {
                    list.Add(1072376, Pieces.ToString()); // Part of an Armor Set (~1_val~ pieces)
                }

                if (BardMasteryBonus)
                {
                    list.Add(1151553); // Activate: Bard Mastery Bonus x2<br>(Effect: 1 min. Cooldown: 30 min.)
                }

                if (_setEquipped)
                {
                    if (MixedSet)
                    {
                        list.Add(1073492); // Full Weapon/Armor Set Present
                    }
                    else
                    {
                        list.Add(1072377); // Full Armor Set Present
                    }

                    GetSetProperties(list);
                }
            }

            base.GetProperties(list);

            // Same placement judgement as BaseSetArmor: D-4 in notes/s1-armour-sets.md.
            SAAbsorptionAttributes.GetProperties(list, _absorptionAttributes);

            if (IsSetItem && !_setEquipped)
            {
                list.Add(1072378); // <br>Only when full set is present:
                GetSetProperties(list);
            }
        }

        public virtual void GetSetProperties(IPropertyList list)
        {
            SetHelper.GetSetProperties(list, this);

            if (!_setEquipped)
            {
                if (_setPhysicalBonus != 0)
                {
                    list.Add(1072382, _setPhysicalBonus.ToString()); // physical resist +~1_val~%
                }

                if (_setFireBonus != 0)
                {
                    list.Add(1072383, _setFireBonus.ToString()); // fire resist +~1_val~%
                }

                if (_setColdBonus != 0)
                {
                    list.Add(1072384, _setColdBonus.ToString()); // cold resist +~1_val~%
                }

                if (_setPoisonBonus != 0)
                {
                    list.Add(1072385, _setPoisonBonus.ToString()); // poison resist +~1_val~%
                }

                if (_setEnergyBonus != 0)
                {
                    list.Add(1072386, _setEnergyBonus.ToString()); // energy resist +~1_val~%
                }
            }
            else if (SetHelper.ResistsBonusPerPiece(this) && RootParent is Mobile m)
            {
                if (_setPhysicalBonus != 0)
                {
                    // physical resist ~1_val~% (total)
                    list.Add(1080361, SetHelper.GetSetTotalResist(m, ResistanceType.Physical).ToString());
                }

                if (_setFireBonus != 0)
                {
                    // fire resist ~1_val~% (total)
                    list.Add(1080362, SetHelper.GetSetTotalResist(m, ResistanceType.Fire).ToString());
                }

                if (_setColdBonus != 0)
                {
                    // cold resist ~1_val~% (total)
                    list.Add(1080363, SetHelper.GetSetTotalResist(m, ResistanceType.Cold).ToString());
                }

                if (_setPoisonBonus != 0)
                {
                    // poison resist ~1_val~% (total)
                    list.Add(1080364, SetHelper.GetSetTotalResist(m, ResistanceType.Poison).ToString());
                }

                if (_setEnergyBonus != 0)
                {
                    // energy resist ~1_val~% (total)
                    list.Add(1080365, SetHelper.GetSetTotalResist(m, ResistanceType.Energy).ToString());
                }
            }
            else
            {
                if (_setPhysicalBonus != 0)
                {
                    // physical resist ~1_val~% (total)
                    list.Add(1080361, (BasePhysicalResistance * Pieces + _setPhysicalBonus).ToString());
                }

                if (_setFireBonus != 0)
                {
                    // fire resist ~1_val~% (total)
                    list.Add(1080362, (BaseFireResistance * Pieces + _setFireBonus).ToString());
                }

                if (_setColdBonus != 0)
                {
                    // cold resist ~1_val~% (total)
                    list.Add(1080363, (BaseColdResistance * Pieces + _setColdBonus).ToString());
                }

                if (_setPoisonBonus != 0)
                {
                    // poison resist ~1_val~% (total)
                    list.Add(1080364, (BasePoisonResistance * Pieces + _setPoisonBonus).ToString());
                }

                if (_setEnergyBonus != 0)
                {
                    // energy resist ~1_val~% (total)
                    list.Add(1080365, (BaseEnergyResistance * Pieces + _setEnergyBonus).ToString());
                }
            }

            // ServUO adds "self repair ~1_val~" (1060450) here when SetSelfRepair != 0 and the
            // shield's own SelfRepair is 0. Not emitted while the value is inert; see _setSelfRepair.
        }

        public int SetResistBonus(ResistanceType resist)
        {
            if (SetHelper.ResistsBonusPerPiece(this))
            {
                return resist switch
                {
                    ResistanceType.Physical => _setEquipped
                        ? PhysicalResistance + _setPhysicalBonus
                        : PhysicalResistance,
                    ResistanceType.Fire   => _setEquipped ? FireResistance + _setFireBonus : FireResistance,
                    ResistanceType.Cold   => _setEquipped ? ColdResistance + _setColdBonus : ColdResistance,
                    ResistanceType.Poison => _setEquipped ? PoisonResistance + _setPoisonBonus : PoisonResistance,
                    ResistanceType.Energy => _setEquipped ? EnergyResistance + _setEnergyBonus : EnergyResistance,
                    _                     => 0
                };
            }

            return resist switch
            {
                ResistanceType.Physical => _setEquipped
                    ? _lastEquipped ? PhysicalResistance * Pieces + _setPhysicalBonus : 0
                    : PhysicalResistance,
                ResistanceType.Fire => _setEquipped
                    ? _lastEquipped ? FireResistance * Pieces + _setFireBonus : 0
                    : FireResistance,
                ResistanceType.Cold => _setEquipped
                    ? _lastEquipped ? ColdResistance * Pieces + _setColdBonus : 0
                    : ColdResistance,
                ResistanceType.Poison => _setEquipped
                    ? _lastEquipped ? PoisonResistance * Pieces + _setPoisonBonus : 0
                    : PoisonResistance,
                ResistanceType.Energy => _setEquipped
                    ? _lastEquipped ? EnergyResistance * Pieces + _setEnergyBonus : 0
                    : EnergyResistance,
                _ => 0
            };
        }
    }
}
