using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/BaseCostume.cs (CC9). Base for the 34 costume masks in
    // Items/Equipment/Suits. A shield-slot item that body-mods the wearer while equipped.
    //
    // ServUO's three save versions collapse to version 0: only CostumeBody and CostumeHue were
    // ever live state, and the v1 m_SaveHueMod was read and discarded.
    //
    // DEVIATION (CC9 D-5): ServUO calls BaseCostume.OnDamaged(m) from AOS.Damage (AOS.cs:417),
    // which knocks the costume into the wearer's backpack when they take damage. ModernUO's
    // AOS.Damage has no such call, so a costume stays on when its wearer is hit. The static
    // method is kept so a one-line hook can restore it; cost in notes/cc9-artifacts.md.
    [Flippable(0x19BC, 0x19BD)]
    [SerializationGenerator(0, false)]
    public partial class BaseCostume : BaseShield, IDyable
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _costumeBody;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _costumeHue;

        public BaseCostume() : base(0x19BC)
        {
            _costumeHue = -1;

            Resource = CraftResource.None;
            Attributes.SpellChanneling = 1;
            Layer = Layer.FirstValid;
            StrRequirement = 10;
        }

        public override double DefaultWeight => 4.0;

        public virtual string CreatureName { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Transformed { get; set; }

        private bool EnMask(Mobile from)
        {
            if (from.Mounted || from.Flying)
            {
                from.SendLocalizedMessage(1010097); // You cannot use this while mounted or flying.
            }
            else if (from.IsBodyMod || from.HueMod > -1)
            {
                from.SendLocalizedMessage(1158010); // You cannot use that item in this form.
            }
            else
            {
                from.BodyMod = _costumeBody;
                from.HueMod = _costumeHue;
                Transformed = true;

                return true;
            }

            return false;
        }

        private void DeMask(Mobile from)
        {
            from.BodyMod = 0;
            from.HueMod = -1;
            Transformed = false;
        }

        public virtual bool Dye(Mobile from, DyeTub sender)
        {
            if (Deleted)
            {
                return false;
            }

            if (RootParent is Mobile && from != RootParent)
            {
                return false;
            }

            Hue = sender.DyedHue;
            return true;
        }

        public override bool OnEquip(Mobile from)
        {
            if (!Transformed)
            {
                return EnMask(from);
            }

            return base.OnEquip(from);
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m && Transformed)
            {
                DeMask(m);
            }
        }

        public static void OnDamaged(Mobile m)
        {
            if (m.FindItemOnLayer(Layer.FirstValid) is BaseCostume costume)
            {
                m.AddToBackpack(costume);
            }
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (RootParent is Mobile m && m.Items.Contains(this))
            {
                EnMask(m);
            }
        }
    }
}
