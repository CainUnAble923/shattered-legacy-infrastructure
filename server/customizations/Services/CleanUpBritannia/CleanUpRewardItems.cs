// cc-P175 Part C (Chase 2026-10-18: "finish the rewards"): Clean Up Britannia's rewards that need nothing the shard lacks,
// ported from ServUO pub57 Services/CleanUpBritannia/Items/ (one ServUO file each, named below). Art, hue, cliloc and
// numbers as ServUO; ServUO's hand-written Serialize/Deserialize become [SerializationGenerator] (version 0, nothing
// saved beyond the base item). Changes, each one token: [Constructable] -> [Constructible], [Flipable] -> [Flippable],
// Weight -> DefaultWeight, IsArtifact dropped (AGENTS.md: pinned has no consumer of it), ServUO's parameterless
// BaseInstrument() (a random harp, lute or drum, BaseInstrument.cs:468-503) -> pinned's (itemID, wellSound, badlySound)
// with the same three picks.
//
// The store lists them at OSI's prices (CleanUpBritanniaRewards.cs). Notes: shard-migration
// notes/cc-P175-cont-clean-up-britannia-finish.md, Part C.

using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    // -- Decor: LillyPad.cs, LillyPads.cs, Mushrooms.cs, YuccaTree.cs, Bamboo.cs, NestWithEggs.cs, the two tapestries -----

    [SerializationGenerator(0, false)]
    public partial class LillyPad : Item
    {
        [Constructible]
        public LillyPad() : base(0xDBC)
        {
        }

        public override double DefaultWeight => 1.0;
    }

    [SerializationGenerator(0, false)]
    public partial class LillyPads : Item
    {
        [Constructible]
        public LillyPads() : base(0xDBE)
        {
        }

        public override double DefaultWeight => 1.0;
    }

    [SerializationGenerator(0, false)]
    public partial class Mushrooms1 : Item
    {
        [Constructible]
        public Mushrooms1() : base(0x0D0F)
        {
        }

        public override double DefaultWeight => 1.0;
    }

    [SerializationGenerator(0, false)]
    public partial class Mushrooms2 : Item
    {
        [Constructible]
        public Mushrooms2() : base(0x0D12)
        {
        }

        public override double DefaultWeight => 1.0;
    }

    [SerializationGenerator(0, false)]
    public partial class Mushrooms3 : Item
    {
        [Constructible]
        public Mushrooms3() : base(0x0D10)
        {
        }

        public override double DefaultWeight => 1.0;
    }

    [SerializationGenerator(0, false)]
    public partial class Mushrooms4 : Item
    {
        [Constructible]
        public Mushrooms4() : base(0x0D13)
        {
        }

        public override double DefaultWeight => 1.0;
    }

    [SerializationGenerator(0, false)]
    public partial class YuccaTree : Item
    {
        [Constructible]
        public YuccaTree() : base(0x0D37)
        {
        }

        public override double DefaultWeight => 10.0;
        public override int LabelNumber => 1023383; // yucca
    }

    [SerializationGenerator(0, false)]
    public partial class Bamboo : Item
    {
        [Constructible]
        public Bamboo() : base(0x246D)
        {
        }

        public override double DefaultWeight => 10.0;
        public override int LabelNumber => 1029324; // bamboo
    }

    [SerializationGenerator(0, false)]
    public partial class NestWithEggs : Item
    {
        [Constructible]
        public NestWithEggs() : base(0x1AD4) => Hue = 2415;

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1026868; // nest with eggs
    }

    [Flippable(0x2D71, 0x2D72)]
    [SerializationGenerator(0, false)]
    public partial class OrnateElvenTapestry : Item
    {
        [Constructible]
        public OrnateElvenTapestry() : base(0x2D72)
        {
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1031633; // ornate elven tapestry
    }

    [Flippable(0x2D6F, 0x2D70)]
    [SerializationGenerator(0, false)]
    public partial class SophisticatedElvenTapestry : Item
    {
        [Constructible]
        public SophisticatedElvenTapestry() : base(0x2D70)
        {
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1151222; // sophisticated elven tapestry
    }

    // -- TableLamp.cs: double-click turns it on and off -------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class TableLamp : Item
    {
        [Constructible]
        public TableLamp() : base(0x49C2)
        {
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1151220; // table lamp

        public override void OnDoubleClick(Mobile from)
        {
            if (ItemID == 0x49C2)
            {
                ItemID = 0x49C1;
            }
            else if (ItemID == 0x49C1)
            {
                ItemID = 0x49C2;
            }
        }
    }

    // -- ChestOfDrawers.cs, FootedChestOfDrawers.cs: lockable furniture (pinned's Drawer and FancyDrawer share the art but
    // are not lockable and carry another cliloc; cc-P33 1.2) ----------------------------------------------------------------

    [Furniture]
    [Flippable(0x0A2C, 0x0A34)]
    [SerializationGenerator(0, false)]
    public partial class ChestOfDrawers : LockableContainer
    {
        [Constructible]
        public ChestOfDrawers() : base(0x0A2C)
        {
        }

        public override double DefaultWeight => 25.0;
        public override int DefaultGumpID => 0x51;
        public override int DefaultDropSound => 0x42;
        public override int LabelNumber => 1022604; // chest of drawers
    }

    [Furniture]
    [Flippable(0x0A30, 0x0A38)]
    [SerializationGenerator(0, false)]
    public partial class FootedChestOfDrawers : LockableContainer
    {
        [Constructible]
        public FootedChestOfDrawers() : base(0x0A30)
        {
        }

        public override double DefaultWeight => 25.0;
        public override int DefaultGumpID => 0x48;
        public override int DefaultDropSound => 0x42;
        public override int LabelNumber => 1151221; // footed chest of drawers
    }

    // -- PresentationStone.cs, SherryTheMouseStatue.cs --------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class PresentationStone : Item
    {
        [Constructible]
        public PresentationStone() : base(0x32F2)
        {
        }

        public override double DefaultWeight => 5.0;
        public override int LabelNumber => 1154745; // Presentation Stone
    }

    // Locked down in a house, it answers "news" with a Town Crier entry, a line every 3 seconds (ServUO's timer; pinned's
    // TownCrier.cs:444-468 is the same shape).
    [SerializationGenerator(0, false)]
    public partial class SherryTheMouseStatue : Item
    {
        private TimerExecutionToken _newsTimer;

        [Constructible]
        public SherryTheMouseStatue() : base(0x20D0) => LootType = LootType.Blessed;

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1080171; // Sherry the Mouse Statue
        public override bool HandlesOnSpeech => true;

        public override void OnSpeech(SpeechEventArgs e)
        {
            if (!IsLockedDown || !e.HasKeyword(0x30) || !e.Mobile.Alive || !e.Mobile.InLOS(Location) || !e.Mobile.InRange(this, 12)) // *news*
            {
                return;
            }

            var tce = GlobalTownCrierEntryList.Instance.GetRandomEntry();

            if (tce == null)
            {
                PublicOverheadMessage(MessageType.Regular, 0x3B2, 1005643); // I have no news at this time.
                return;
            }

            _newsTimer.Cancel();
            var index = 0;
            Timer.StartTimer(
                TimeSpan.FromSeconds(1.0),
                TimeSpan.FromSeconds(3.0),
                tce.Lines.Length,
                () =>
                {
                    if (!Deleted && index < tce.Lines.Length)
                    {
                        PublicOverheadMessage(MessageType.Regular, 0x3B2, false, tce.Lines[index++]);
                    }
                },
                out _newsTimer
            );

            PublicOverheadMessage(MessageType.Regular, 0x3B2, 502978); // Some of the latest news!
        }

        public override void OnDelete()
        {
            _newsTimer.Cancel();
            base.OnDelete();
        }
    }

    // -- NocturneEarrings.cs, SnakeSkinBoots.cs and the four BootsOfThe*.cs ----------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class NocturneEarrings : SilverEarrings
    {
        [Constructible]
        public NocturneEarrings()
        {
            Hue = 0x3E5;
            Attributes.NightSight = 1;
        }

        public override int LabelNumber => 1080189; // Nocturne Earrings
    }

    [SerializationGenerator(0, false)]
    public partial class SnakeSkinBoots : Boots
    {
        [Constructible]
        public SnakeSkinBoots() : base(0x7D9) => Resistances.Poison = 2;

        public override int LabelNumber => 1080122; // Snake Skin Boots
    }

    [SerializationGenerator(0, false)]
    public partial class BootsOfTheLavaLizard : Boots
    {
        [Constructible]
        public BootsOfTheLavaLizard() : base(0x674) => Resistances.Fire = 2;

        public override int LabelNumber => 1151207; // Boots Of The Lava Lizard
    }

    [SerializationGenerator(0, false)]
    public partial class BootsOfTheIceWyrm : Boots
    {
        [Constructible]
        public BootsOfTheIceWyrm() : base(0x482) => Resistances.Cold = 2;

        public override int LabelNumber => 1151208; // Boots of the Ice Wyrm
    }

    [SerializationGenerator(0, false)]
    public partial class BootsOfTheCrystalHydra : Boots
    {
        [Constructible]
        public BootsOfTheCrystalHydra() : base(0x47E) => Resistances.Energy = 2;

        public override int LabelNumber => 1151209; // Boots of the Crystal Hydra
    }

    [SerializationGenerator(0, false)]
    public partial class BootsOfTheThrasher : Boots
    {
        [Constructible]
        public BootsOfTheThrasher() : base(0x497) => Resistances.Physical = 2;

        public override int LabelNumber => 1151210; // ServUO's comment says Snake Skin Boots; the cliloc is the Thrasher's
    }

    // -- NamedRobes.cs: five blessed robes in a random dyed hue ------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class HumansAndElvesRobe : Robe
    {
        [Constructible]
        public HumansAndElvesRobe() : base(Utility.RandomDyedHue()) => LootType = LootType.Blessed;

        public override int LabelNumber => 1151202; // Humans & Elves are our friends!
    }

    [SerializationGenerator(0, false)]
    public partial class GargoylesAreOurFriendsRobe : Robe
    {
        [Constructible]
        public GargoylesAreOurFriendsRobe() : base(Utility.RandomDyedHue()) => LootType = LootType.Blessed;

        public override int LabelNumber => 1151203; // Gargoyles are our friends!
    }

    [SerializationGenerator(0, false)]
    public partial class WeArePiratesRobe : Robe
    {
        [Constructible]
        public WeArePiratesRobe() : base(Utility.RandomDyedHue()) => LootType = LootType.Blessed;

        public override int LabelNumber => 1151204; // We are pirates!
    }

    [SerializationGenerator(0, false)]
    public partial class FollowerOfBaneRobe : Robe
    {
        [Constructible]
        public FollowerOfBaneRobe() : base(Utility.RandomDyedHue()) => LootType = LootType.Blessed;

        public override int LabelNumber => 1151205; // Follower of Bane
    }

    [SerializationGenerator(0, false)]
    public partial class QueenDawnForeverRobe : Robe
    {
        [Constructible]
        public QueenDawnForeverRobe() : base(Utility.RandomDyedHue()) => LootType = LootType.Blessed;

        public override int LabelNumber => 1151206; // Queen Dawn Forever
    }

    // -- NaturesTears.cs, PrimordialDecay.cs, ArachnidDoom.cs: slayer instruments, 450 uses -------------------------------

    public static class CleanUpInstruments
    {
        // ServUO BaseInstrument.RandomInstrument (BaseInstrument.cs:483-503): a lap harp, a lute or drums, with their sounds
        // (pinned's LapHarp.cs, Lute.cs, Drums.cs use the same three).
        public static (int ItemID, int Well, int Badly) Random() =>
            Utility.Random(3) switch
            {
                0 => (0xEB2, 0x45, 0x46),
                1 => (0xEB3, 0x4C, 0x4D),
                _ => (0xE9C, 0x38, 0x39)
            };
    }

    [SerializationGenerator(0, false)]
    public partial class NaturesTears : BaseInstrument
    {
        [Constructible]
        public NaturesTears() : this(CleanUpInstruments.Random())
        {
        }

        private NaturesTears((int ItemID, int Well, int Badly) i) : base(i.ItemID, i.Well, i.Badly)
        {
            Hue = 2075;
            Slayer = SlayerName.Fey;
            UsesRemaining = 450;
        }

        public override double DefaultWeight => 5.0;
        public override int LabelNumber => 1154373; // Nature's Tears
        public override int InitMinUses => 450;
        public override int InitMaxUses => 450;
    }

    [SerializationGenerator(0, false)]
    public partial class PrimordialDecay : BaseInstrument
    {
        [Constructible]
        public PrimordialDecay() : this(CleanUpInstruments.Random())
        {
        }

        private PrimordialDecay((int ItemID, int Well, int Badly) i) : base(i.ItemID, i.Well, i.Badly)
        {
            Hue = 1927;
            Slayer = SlayerName.ElementalBan;
            UsesRemaining = 450;
        }

        public override double DefaultWeight => 4.0;
        public override int LabelNumber => 1154723; // Primordial Decay
        public override int InitMinUses => 450;
        public override int InitMaxUses => 450;
    }

    [SerializationGenerator(0, false)]
    public partial class ArachnidDoom : BaseInstrument
    {
        [Constructible]
        public ArachnidDoom() : this(CleanUpInstruments.Random())
        {
        }

        private ArachnidDoom((int ItemID, int Well, int Badly) i) : base(i.ItemID, i.Well, i.Badly)
        {
            Hue = 1944;
            Slayer = SlayerName.ArachnidDoom;
            UsesRemaining = 450;
        }

        public override double DefaultWeight => 4.0;
        public override int LabelNumber => 1154724; // Arachnid Doom
        public override int InitMinUses => 450;
        public override int InitMaxUses => 450;
    }

    // -- The six 300,000 charms: LuckyCharm.cs, SoldiersMedal.cs, DuelistsEdge.cs, NecromancersPhylactery.cs,
    // WizardsCurio.cs, MysticsMemento.cs (talismans) -------------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class LuckyCharm : BaseTalisman
    {
        [Constructible]
        public LuckyCharm() : base(0x2F5B)
        {
            Hue = 1923;
            Attributes.RegenHits = 1;
            Attributes.RegenStam = 1;
            Attributes.RegenMana = 1;
            Attributes.Luck = 150;
        }

        public override int LabelNumber => 1154725; // Lucky Charm
    }

    [SerializationGenerator(0, false)]
    public partial class SoldiersMedal : BaseTalisman
    {
        [Constructible]
        public SoldiersMedal() : base(0x2F5B)
        {
            Hue = 1902;
            SkillBonuses.SetValues(0, SkillName.Tactics, 10.0);
            Attributes.AttackChance = 5;
            Attributes.RegenStam = 2;
            Attributes.WeaponDamage = 20;
        }

        public override int LabelNumber => 1154726; // Soldier's Medal
        public override bool ForceShowName => true;
    }

    [SerializationGenerator(0, false)]
    public partial class DuelistsEdge : BaseTalisman
    {
        [Constructible]
        public DuelistsEdge() : base(0x2F58)
        {
            Hue = 1902;
            SkillBonuses.SetValues(0, SkillName.Anatomy, 10.0);
            Attributes.RegenStam = 2;
            Attributes.AttackChance = 5;
            Attributes.WeaponDamage = 20;
        }

        public override int LabelNumber => 1154727; // Duelist's Edge
        public override bool ForceShowName => true;
    }

    [SerializationGenerator(0, false)]
    public partial class NecromancersPhylactery : BaseTalisman
    {
        [Constructible]
        public NecromancersPhylactery() : base(0x2F5A)
        {
            Hue = 1912;
            SkillBonuses.SetValues(0, SkillName.SpiritSpeak, 10.0);
            Attributes.RegenMana = 1;
            Attributes.LowerRegCost = 10;
            Attributes.SpellDamage = 5;
        }

        public override int LabelNumber => 1154728; // Necromancer's Phylactery
        public override bool ForceShowName => true;
    }

    [SerializationGenerator(0, false)]
    public partial class WizardsCurio : BaseTalisman
    {
        [Constructible]
        public WizardsCurio() : base(0x2F58)
        {
            Hue = 1912;
            SkillBonuses.SetValues(0, SkillName.EvalInt, 10.0);
            Attributes.RegenMana = 1;
            Attributes.LowerRegCost = 10;
            Attributes.SpellDamage = 5;
        }

        public override int LabelNumber => 1154729; // Wizard's Curio
        public override bool ForceShowName => true;
    }

    [SerializationGenerator(0, false)]
    public partial class MysticsMemento : BaseTalisman
    {
        [Constructible]
        public MysticsMemento() : base(0x2F5B)
        {
            Hue = 1912;
            SkillBonuses.SetValues(0, SkillName.Focus, 10.0);
            Attributes.RegenMana = 1;
            Attributes.LowerRegCost = 10;
            Attributes.SpellDamage = 5;
        }

        public override int LabelNumber => 1154730; // Mystic's Memento
        public override bool ForceShowName => true;
    }
}
