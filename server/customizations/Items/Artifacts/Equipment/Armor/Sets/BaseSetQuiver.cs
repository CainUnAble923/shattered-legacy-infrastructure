using ModernUO.Serialization;

namespace Server.Items
{
    /// <summary>
    ///     A quiver that can carry a Mondain's Legacy set bonus (S10).
    ///     <para>
    ///         ServUO puts this state directly on <see cref="BaseQuiver" />. Here it lives on this
    ///         intermediate class for the reason given on <see cref="BaseSetArmor" />. Set content
    ///         derives from it; stock ModernUO quivers are untouched.
    ///     </para>
    ///     <para>
    ///         ServUO's <c>BaseQuiver</c> carries neither <c>SetSelfRepair</c> nor
    ///         <c>SAAbsorptionAttributes</c>, so neither does this: the serialized layout is fields
    ///         0-9 as on <see cref="BaseSetArmor" /> and nothing after. Anything appended later gets a
    ///         save-flag bit and needs no migration.
    ///     </para>
    ///     <para>See <c>shard-migration/notes/s10-carriers.md</c> for the deviations.</para>
    /// </summary>
    [SerializationGenerator(0, false)]
    public abstract partial class BaseSetQuiver : BaseQuiver, ISetItem
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

        public BaseSetQuiver(int itemID = 0x2FB7) : base(itemID)
        {
            SetAttributes = new AosAttributes(this);
            SetSkillBonuses = new AosSkillBonuses(this);
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
                list.Add(1073491, Pieces.ToString()); // Part of a Weapon/Armor Set (~1_val~ pieces)

                if (BardMasteryBonus)
                {
                    list.Add(1151553); // Activate: Bard Mastery Bonus x2<br>(Effect: 1 min. Cooldown: 30 min.)
                }

                if (_setEquipped)
                {
                    list.Add(1073492); // Full Weapon/Armor Set Present
                    SetHelper.GetSetProperties(list, this);
                }
            }

            base.GetProperties(list);

            if (IsSetItem && !_setEquipped)
            {
                list.Add(1072378); // <br>Only when full set is present:
                SetHelper.GetSetProperties(list, this);
            }
        }

        // ServUO's BaseQuiver has no GetSetProperties virtual of its own; it calls SetHelper
        // directly from both places above, and so does this.

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
