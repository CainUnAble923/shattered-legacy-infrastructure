// cc-P199: ServUO pub57 Scripts/Items/Tools/ForgedMetalOfArtifacts.cs. Forged metal of artifacts (ServUO sells it in
// its Ultima Store with 5 or 10 uses, UltimaStore.cs:312-313, :518-519): a blessed 5-stone tool (art 4023, hue 51).
// Used from the backpack it opens a gump; OKAY spends a use and makes the user's next item enhancement succeed at 100%
// whatever the skill and intensity. With the aura already on, the gump offers to REMOVE it instead and refunds the
// use to this tool if it is in the backpack. Worn out at 0 uses. ServUO's classic Gumps become pinned's DynamicGump;
// layout, buttons and messages are ServUO's.
//
// The aura is read by the enhance code: patches/Enhance-forged-metal-of-artifacts.patch adds ServUO's check to pinned's
// Enhance.Invoke after the last CheckResult (ServUO Services/Craft/Core/Enhance.cs:253-258): it clears the aura, says
// 1149969 and forces Success. ServUO also changes the enhance target prompt to 1149869 with "100"
// while the aura is on (ServUO Enhance.cs:355-362; pinned Enhance.cs:358-360 sends 1061004 only).
//
// Differences from ServUO:
// - The aura. ServUO keeps it in PlayerMobile.NextEnhanceSuccess (ServUO PlayerMobile.cs:244-247), a field it never
//   saves. Pinned's PlayerMobile has none, so it is a set of players kept here (GetNextEnhanceSuccess /
//   SetNextEnhanceSuccess); like ServUO's it is lost on restart.
// - ServUO's base(0xE8A) then ItemID = 4023 (:255-259) is base(4023). Weight 5.0 is DefaultWeight.
// - ServUO's two constructors are one with uses = 5. A double-click by a non-player does nothing; ServUO would read
//   NextEnhanceSuccess off a null PlayerMobile (:314).

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ForgedMetalOfArtifacts : Item
    {
        private static readonly HashSet<PlayerMobile> _nextEnhanceSuccess = new();

        [InvalidateProperties]
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _usesRemaining;

        [Constructible]
        public ForgedMetalOfArtifacts(int uses = 5) : base(4023)
        {
            Hue = 51;
            _usesRemaining = uses;
            LootType = LootType.Blessed;
        }

        public override int LabelNumber => 1149868; // Forged Metal of Artifacts

        public override double DefaultWeight => 5.0;

        public static bool GetNextEnhanceSuccess(PlayerMobile pm) => pm != null && _nextEnhanceSuccess.Contains(pm);

        public static void SetNextEnhanceSuccess(PlayerMobile pm, bool value)
        {
            if (pm == null)
            {
                return;
            }

            if (value)
            {
                _nextEnhanceSuccess.Add(pm);
            }
            else
            {
                _nextEnhanceSuccess.Remove(pm);
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1060584, _usesRemaining.ToString()); // uses remaining: ~1_val~
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            from.CloseGump<InternalGump>();
            from.CloseGump<CancelGump>();

            if (IsChildOf(from.Backpack))
            {
                if (GetNextEnhanceSuccess(pm))
                {
                    from.SendGump(new CancelGump(pm, this));
                }
                else
                {
                    from.SendGump(new InternalGump(pm, this));
                }
            }
            else
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            }
        }

        public void Use(PlayerMobile from)
        {
            if (IsChildOf(from.Backpack))
            {
                SetNextEnhanceSuccess(from, true);
                // A magical aura surrounds you and you feel your next item enhancing attempt will most certainly be successful.
                from.SendLocalizedMessage(1149956);
                UsesRemaining -= 1;

                if (_usesRemaining <= 0)
                {
                    Delete();
                    from.SendLocalizedMessage(1044038); // You have worn out your tool!
                }
            }
            else
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            }
        }

        public class InternalGump : DynamicGump
        {
            private readonly PlayerMobile m_Mobile;
            private readonly ForgedMetalOfArtifacts m_Tool;

            public InternalGump(PlayerMobile from, ForgedMetalOfArtifacts tool) : base(50, 50)
            {
                m_Mobile = from;
                m_Tool = tool;
            }

            protected override void BuildLayout(ref DynamicGumpBuilder builder)
            {
                builder.AddPage();

                builder.AddBackground(0, 0, 400, 230, 0x13BE);

                builder.AddImageTiled(5, 5, 390, 25, 0xA40);
                // <CENTER>Forged Metal of Artifacts</CENTER>
                builder.AddHtmlLocalized(5, 8, 390, 25, 1113302, "#1149868", 0x7FFF);

                builder.AddImageTiled(5, 35, 390, 125, 0xA40);

                /*
                 * The next time that you try to enhance an item the success rate will be the value listed
                 * below regardless of your skill and total intensity of the item. Skill requirements still
                 * apply to material usage.  One charge on this item will be consumed per use and only one
                 * instance of this buff can be active at any time.
                 */
                builder.AddHtmlLocalized(10, 40, 380, 125, 1149920, 0x7FFF);

                builder.AddImageTiled(5, 165, 120, 30, 0xA40);
                builder.AddHtmlLocalized(10, 170, 120, 30, 1149921, 0x7FFF); // Skill:
                builder.AddImageTiled(125, 165, 140, 30, 0xA40);
                builder.AddHtmlLocalized(130, 170, 140, 30, 1114057, "#1149992", 0x7FFF); // All Crafting Skills

                builder.AddImageTiled(5, 195, 120, 30, 0xA40);
                builder.AddHtmlLocalized(10, 200, 120, 30, 1149933, 0x7FFF); // Success Rate:
                builder.AddImageTiled(125, 195, 140, 30, 0xA40);
                builder.AddHtmlLocalized(130, 200, 140, 30, 1149934, "100", 0x7FFF); // ~1_VAL~%

                builder.AddImageTiled(270, 165, 125, 60, 0xA40);
                builder.AddButton(275, 170, 0xFB7, 0xFB8, 1);
                builder.AddHtmlLocalized(310, 172, 85, 30, 1149935, 0x7FFF); // OKAY
                builder.AddButton(275, 200, 0xFB1, 0xFB3, 0);
                builder.AddHtmlLocalized(310, 202, 85, 30, 1060051, 0x7FFF); // CANCEL
            }

            public override void OnResponse(NetState sender, in RelayInfo info)
            {
                if (info.ButtonID == 1)
                {
                    m_Tool.Use(m_Mobile);
                }
            }
        }

        public class CancelGump : DynamicGump
        {
            private readonly PlayerMobile m_Mobile;
            private readonly ForgedMetalOfArtifacts m_Tool;

            public CancelGump(PlayerMobile from, ForgedMetalOfArtifacts tool) : base(50, 50)
            {
                m_Mobile = from;
                m_Tool = tool;
            }

            protected override void BuildLayout(ref DynamicGumpBuilder builder)
            {
                builder.AddPage();

                builder.AddBackground(0, 0, 400, 155, 0x13BE);
                builder.AddImageTiled(5, 5, 390, 25, 0xA40);
                builder.AddHtmlLocalized(5, 8, 390, 25, 1113302, "#1149868", 0x7FFF); // <CENTER>~1_VAL~</CENTER>
                builder.AddImageTiled(5, 35, 390, 52, 0xA40);
                // *CAUTION* You are under the following ENHANCE ITEM buff. Do you want to remove it?
                builder.AddHtmlLocalized(10, 40, 380, 52, 1149967, 0x7FFF);
                builder.AddImageTiled(5, 92, 120, 29, 0xA40);
                builder.AddHtmlLocalized(10, 97, 120, 29, 1149921, 0x7FFF); // Skill:
                builder.AddImageTiled(5, 121, 120, 29, 0xA40);
                builder.AddHtmlLocalized(10, 126, 120, 29, 1149933, 0x7FFF); // Success Rate:
                builder.AddImageTiled(125, 92, 140, 29, 0xA40);
                builder.AddHtmlLocalized(130, 97, 140, 29, 1114057, "#1149992", 0x7FFF); // ~1_val~
                builder.AddImageTiled(125, 121, 140, 29, 0xA40);
                builder.AddHtmlLocalized(130, 126, 140, 29, 1149934, "100", 0x7FFF); // ~1_VAL~%
                builder.AddImageTiled(270, 92, 125, 58, 0xA40);
                builder.AddButton(275, 97, 0xFB7, 0xFB8, 1);
                builder.AddHtmlLocalized(310, 99, 85, 29, 1149968, 0x7FFF); // REMOVE
            }

            public override void OnResponse(NetState sender, in RelayInfo info)
            {
                if (info.ButtonID != 1)
                {
                    return;
                }

                if (m_Tool?.Deleted != false)
                {
                    // A charge could not be refunded to the last Forged Metal of Artifacts tool you used, you must use this charge.
                    m_Mobile.SendLocalizedMessage(1155766);
                }
                else if (!m_Tool.IsChildOf(m_Mobile.Backpack))
                {
                    // A charge could not be refunded to the last Forged Metal of Artifacts tool you used because the item is not in your backpack.
                    m_Mobile.SendLocalizedMessage(1155767);
                }
                else
                {
                    SetNextEnhanceSuccess(m_Mobile, false);
                    // The magical aura that surrounded you disipates and you feel that your item enhancement chances have returned to normal.
                    m_Mobile.SendLocalizedMessage(1149969);

                    m_Tool.UsesRemaining += 1;
                    m_Mobile.SendLocalizedMessage(1155768); // A charged has been refunded to your Forged Metal of Artifacts tool.
                }
            }
        }
    }
}
