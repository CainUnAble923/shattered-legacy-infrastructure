// ServUO: Items/Functional/ChickenLizardEgg.cs (CC6 batch 4). Values and logic verbatim; serialization by the generator.
//
// The egg's whole state machine is here: five stages (New, Stage1, Stage2, Mature, Burnt), a water level poured in one
// unit per stage, a dryness derived from the two, an accumulated incubation time that advances the stage at 24/48/72 h
// and burns the egg at 120 h, a 10% (Dry 5%, Parched 1%, Dehydrated 0%) roll at maturity for a battle chicken lizard
// in one of twelve hiryu hues, and a hatch that places a ChickenLizard or a BattleChickenLizard at the hatcher's feet.
//
// Two of its inputs are not reachable on this shard, both recorded as deviations in notes/cc6-creatures-batch4.md:
//   - Incubating is only ever set true by ServUO's Incubator (Items/Functional/Incubator.cs, 158 lines, a house
//     container that is not ported), and CheckStatus is only ever called from it. Here nothing advances an egg past
//     New except a GameMaster setting TotalIncubationTime and calling CheckStatus (D-69). ServUO's DropToItem clause
//     `!(Parent is Incubator)` is therefore dropped with the type; the rest of that override is as written.
//   - Pour is called from ServUO's BaseBeverage.Pour_OnTarget (Beverage.cs:1489), a branch pinned ModernUO's
//     Beverage.cs:587 does not have (D-70). The method is here and works; no beverage reaches it.
// The two BaseConfirmGump subclasses need nothing: pinned Gumps/BaseConfirmGump.cs:5 has the same shape.
//
// Serialization: six members, in ServUO's order. IncubationStart, Stage, WaterLevel and IsBattleChicken are plain
// fields; TotalIncubationTime and Incubating are [SerializableProperty] because their setters do more than assign.

using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Items;

public enum EggStage
{
    New,
    Stage1,
    Stage2,
    Mature,
    Burnt
}

public enum Dryness
{
    Moist,
    Dry,
    Parched,
    Dehydrated
}

[SerializationGenerator(0, false)]
public partial class ChickenLizardEgg : Item
{
    public virtual bool CanMutate => true;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _incubationStart;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private EggStage _stage;

    [SerializableField(4)]
    private int _waterLevel;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _isBattleChicken;

    [Constructible]
    public ChickenLizardEgg() : base(0x41BD)
    {
        _incubating = false;
        _totalIncubationTime = TimeSpan.Zero;
        _stage = EggStage.New;
    }

    [SerializableProperty(1)]
    [CommandProperty(AccessLevel.GameMaster)]
    public TimeSpan TotalIncubationTime
    {
        get => _totalIncubationTime;
        set
        {
            _totalIncubationTime = value;
            _incubationStart = Core.Now;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(2)]
    [CommandProperty(AccessLevel.GameMaster)]
    public bool Incubating
    {
        get => _incubating;
        set
        {
            if (_incubating && !value)
            {
                if (_incubationStart < Core.Now)
                {
                    TotalIncubationTime += Core.Now - _incubationStart;
                }
            }

            _incubating = value;
            this.MarkDirty();
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public Dryness Dryness
    {
        get
        {
            var v = (int)_stage - _waterLevel;

            if (v >= 2 && _waterLevel == 0)
            {
                return Dryness.Dehydrated;
            }

            if (v >= 2)
            {
                return Dryness.Parched;
            }

            if (v >= 1)
            {
                return Dryness.Dry;
            }

            return Dryness.Moist;
        }
    }

    // WaterLevel is the generator's: a [SerializableField] always gets a public property named after its field, even
    // without [SerializedCommandProperty]. Build A of batch 4 went red on a hand-written duplicate.

    public override int LabelNumber
    {
        get
        {
            var c = 1112468;

            if (_stage == EggStage.Mature)
            {
                c = _isBattleChicken ? 1112468 : 1112467;
            }
            else if (_stage == EggStage.Burnt)
            {
                c = 1112466;
            }
            else
            {
                switch (Dryness)
                {
                    case Dryness.Moist:
                        c = 1112462;
                        break;
                    case Dryness.Dry:
                        c = 1112463;
                        break;
                    case Dryness.Parched:
                        c = 1112464;
                        break;
                    case Dryness.Dehydrated:
                        c = 1112465;
                        break;
                }
            }

            return c;
        }
    }

    public override bool DropToMobile(Mobile from, Mobile target, Point3D p)
    {
        var check = base.DropToMobile(from, target, p);

        if (check && _incubating)
        {
            Incubating = false;
        }

        return check;
    }

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        var check = base.DropToWorld(from, p);

        if (check && _incubating)
        {
            Incubating = false;
        }

        return check;
    }

    public override bool DropToItem(Mobile from, Item target, Point3D p)
    {
        var check = base.DropToItem(from, target, p);

        // ServUO: `check && !(Parent is Incubator) && m_Incubating`. There is no Incubator type here (D-69).
        if (check && _incubating)
        {
            Incubating = false;
        }

        return check;
    }

    public override void OnItemLifted(Mobile from, Item item)
    {
        if (_incubating)
        {
            Incubating = false;
        }

        base.OnItemLifted(from, item);
    }

    public void CheckStatus()
    {
        if (_stage == EggStage.Burnt)
        {
            return;
        }

        if (_incubating && _incubationStart < Core.Now)
        {
            TotalIncubationTime += Core.Now - _incubationStart;
        }

        if (_totalIncubationTime > TimeSpan.FromHours(24) && _stage == EggStage.New) // from new to stage 1
        {
            IncreaseStage();
            // Nothing, egg goes to stage 2 regardless if its watered or not
        }
        else if (_totalIncubationTime >= TimeSpan.FromHours(48) && _stage == EggStage.Stage1) // from stage 1 to stage 2
        {
            if (Dryness >= Dryness.Parched)
            {
                if (Utility.RandomBool())
                {
                    BurnEgg();
                }
            }

            IncreaseStage();
        }
        else if (_totalIncubationTime >= TimeSpan.FromHours(72) && _stage == EggStage.Stage2) // from stage 2 to mature egg
        {
            if (Dryness >= Dryness.Parched)
            {
                if (.25 < Utility.RandomDouble())
                {
                    BurnEgg();
                }
            }

            IncreaseStage();
        }
        else if (_totalIncubationTime >= TimeSpan.FromHours(120) && _stage == EggStage.Mature)
        {
            BurnEgg();
            IncreaseStage();
        }
    }

    public void Pour(Mobile from, BaseBeverage bev)
    {
        if (!bev.IsEmpty && bev.Pourable && bev.Content == BeverageType.Water && bev.ValidateUse(from, false))
        {
            if (_stage == EggStage.Burnt)
            {
                from.SendMessage("You decide not to water the burnt egg.");
            }
            else if (_waterLevel < (int)_stage)
            {
                bev.Quantity--;

                _waterLevel++;
                this.MarkDirty();
                from.PlaySound(0x4E);

                InvalidateProperties();
            }
            else
            {
                from.SendMessage("You decide not to water the egg since it doesn't need it.");
            }
        }
    }

    public void IncreaseStage()
    {
        if (_stage != EggStage.Burnt)
        {
            _stage++;
        }

        switch (_stage)
        {
            default:
            case EggStage.New:
            case EggStage.Stage1:
                ItemID = 0x41BE;
                break;
            case EggStage.Stage2:
                ItemID = 0x41BF;
                break;
            case EggStage.Mature:
                {
                    ItemID = 0x41BF;

                    Hue = 555;

                    var chance = .10;
                    if (Dryness == Dryness.Dry)
                    {
                        chance = .05;
                    }
                    else if (Dryness == Dryness.Parched)
                    {
                        chance = .01;
                    }
                    else if (Dryness == Dryness.Dehydrated)
                    {
                        chance = 0;
                    }

                    if (CanMutate && chance >= Utility.RandomDouble())
                    {
                        _isBattleChicken = true;
                        Hue = GetRandomHiryuHue();
                    }
                    else
                    {
                        Hue = 555;
                    }

                    break;
                }
            case EggStage.Burnt:
                ItemID = 0x41BF;
                Hue = 2026;
                break;
        }

        this.MarkDirty();
        InvalidateProperties();
    }

    private static int GetRandomHiryuHue()
    {
        return Utility.Random(12) switch
        {
            0  => 1173, // Cyan
            1  => 1160, // Strong Cyan
            2  => 675,  // Light Green
            3  => 72,   // Strong Green
            4  => 2213, // Gold
            5  => 1463, // Strong Yellow
            6  => 2425, // Agapite
            7  => 26,   // Strong Purple
            8  => 1151, // Ice Green
            9  => 1152, // Ice Blue
            10 => 101,  // Light Blue
            11 => 1159, // yellow blue
            _  => 0
        };
    }

    public void BurnEgg()
    {
        _stage = EggStage.Burnt;
        this.MarkDirty();
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            if (_stage == EggStage.Mature)
            {
                from.SendGump(new ConfirmHatchGump1(from, this));
            }
            else
            {
                from.SendGump(new ConfirmHatchGump2(from, this));
            }
        }
    }

    public void TryHatchEgg(Mobile from)
    {
        if (_stage == EggStage.Mature)
        {
            OnHatch(from);
        }
        else
        {
            CrumbleEgg(from);
        }
    }

    public virtual void OnHatch(Mobile from)
    {
        BaseCreature bc;

        if (_isBattleChicken)
        {
            from.SendLocalizedMessage(1112478); // You hatch a battle chicken lizard!!
            bc = new BattleChickenLizard();
            bc.Hue = Hue;
        }
        else
        {
            from.SendLocalizedMessage(1112477); // You hatch a chicken lizard.
            bc = new ChickenLizard();
        }

        bc.MoveToWorld(from.Location, from.Map);
        Delete();
    }

    public void CrumbleEgg(Mobile from)
    {
        from.SendLocalizedMessage(1112447); // You hatch the egg but it crumbles in your hands!
        Delete();
    }

    private class ConfirmHatchGump1 : BaseConfirmGump
    {
        private readonly ChickenLizardEgg _egg;

        public override int TitleNumber => 1112444;
        public override int LabelNumber => 1112446;

        public ConfirmHatchGump1(Mobile from, ChickenLizardEgg egg) => _egg = egg;

        public override void Confirm(Mobile from)
        {
            _egg?.TryHatchEgg(from);
        }
    }

    private class ConfirmHatchGump2 : BaseConfirmGump
    {
        private readonly ChickenLizardEgg _egg;

        public override int TitleNumber => 1112444;
        public override int LabelNumber => 1112445;

        public ConfirmHatchGump2(Mobile from, ChickenLizardEgg egg) => _egg = egg;

        public override void Confirm(Mobile from)
        {
            _egg?.TryHatchEgg(from);
        }
    }
}
