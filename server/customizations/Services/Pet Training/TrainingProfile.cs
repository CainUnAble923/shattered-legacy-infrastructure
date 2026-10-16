// cc-P130: ServUO Scripts/Services/Pet Training/TrainingProfile.cs -> ours. A pet's training state: whether training has
// begun, its points, the progress bar that fills in battle (CheckProgress, called from AOS.Damage through
// PetTrainingHooks.OnDamage), the power hour, the control-slot step (OnTrain) and the plan. Line for line, with:
//   - pinned's gump calls (HasGump<T>, FindGump<T>, SendGump) for ServUO's BaseGump statics;
//   - Core.Now for DateTime.UtcNow;
//   - LeadingIntoBattleQuest.CheckComplete dropped: that ML quest is not on this shard;
//   - storage in PetTrainingStore (decision 2a), the constructor-reader pair as Serialize/Deserialize on IGenericWriter.
// The per-creature progress table is not saved in ServUO either.

using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Mobiles;

public enum TrainingMode
{
    Regular,
    Planning
}

[PropertyObject]
public class TrainingProfile
{
    [CommandProperty(AccessLevel.GameMaster)]
    public TrainingMode TrainingMode { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool HasBegunTraining { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool HasIncreasedControlSlot { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int TrainedThisLevel { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool HasRecievedControlSlotWarning { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public double TrainingProgress { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public double TrainingProgressMax { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public BaseCreature Creature { get; }

    [CommandProperty(AccessLevel.GameMaster)]
    public double TrainingProgressPercentile => TrainingProgressMax <= 0 ? 0 : TrainingProgress / TrainingProgressMax;

    [CommandProperty(AccessLevel.GameMaster)]
    public int ControlSlots => Creature.ControlSlots;

    [CommandProperty(AccessLevel.GameMaster)]
    public int ControlSlotsMin => Creature.ControlSlotsMin;

    [CommandProperty(AccessLevel.GameMaster)]
    public int ControlSlotsMax => Creature.ControlSlotsMax;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool CanApplyOptions => HasBegunTraining && TrainingProgressPercentile >= 1.0;

    private int _trainingPoints;
    private int _startingTrainingPoints;
    private PlanningProfile _plan;

    [CommandProperty(AccessLevel.GameMaster)]
    public int TrainingPoints
    {
        get => _trainingPoints;
        set
        {
            if (value <= 0)
            {
                EndTraining();
            }
            else
            {
                _trainingPoints = value;
            }
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public int StartingTrainingPoints
    {
        get => _startingTrainingPoints;
        set => _startingTrainingPoints = value;
    }

    public PlanningProfile PlanningProfile => _plan ??= new PlanningProfile(Creature);

    public static TimeSpan PowerHourDuration = TimeSpan.FromHours(1);
    public static TimeSpan PowerHourDelay = TimeSpan.FromHours(24);

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime PowerHourBegin { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool InPowerHour { get; set; }

    public int PowerHourMultiplier => InPowerHour ? 2 : 1; // 2x gains from creatures during power hour

    private const int MaxTrainingProgress = 100;

    private readonly Dictionary<BaseCreature, int> _progressTable;

    public TrainingProfile(BaseCreature bc)
    {
        Creature = bc;

        _progressTable = new Dictionary<BaseCreature, int>();
    }

    private int AssignStartingTrainingPoints()
    {
        if (ControlSlots != ControlSlotsMin)
        {
            return 1501;
        }

        if (ControlSlotsMin == 1 && ControlSlotsMax == 2)
        {
            return 2556;
        }

        if (ControlSlotsMin == 1 && ControlSlotsMax == 3)
        {
            return 2381;
        }

        return 1501;
    }

    public void BeginTraining()
    {
        if (ControlSlots < ControlSlotsMax)
        {
            TrainingPoints = AssignStartingTrainingPoints();
            HasBegunTraining = true;

            TrainingProgress = 0;
            TrainingProgressMax = MaxTrainingProgress;
        }
    }

    public void OnTrain(PlayerMobile pm, int points)
    {
        int reqInc;

        if (!HasIncreasedControlSlot)
        {
            reqInc = GetRequirementIncrease(true);

            Creature.RemoveFollowers();
            Creature.ControlSlots++;
            Creature.AddFollowers();
            TrainedThisLevel = 0;

            pm.SendLocalizedMessage(1157537); // Your pet's control slot have been updated.
            HasIncreasedControlSlot = true;
        }
        else
        {
            TrainedThisLevel++;
            reqInc = GetRequirementIncrease(false);
        }

        Creature.CurrentTameSkill = Math.Min(BaseCreature.MaxTameRequirement, Creature.CurrentTameSkill + reqInc);
        TrainingPoints -= points;
    }

    public int GetRequirementIncrease(bool slotIncreased)
    {
        if (slotIncreased)
        {
            // First level
            if (ControlSlotsMin + 1 == ControlSlots)
            {
                return 21;
            }

            // subsequent trains of that level
            return 22;
        }

        // First train of that level (after level increase, so actually 2nd train)
        if (TrainedThisLevel == 1)
        {
            return 3;
        }

        // subsequent trains of that level
        return 2;
    }

    public void EndTraining()
    {
        HasBegunTraining = false;
        _trainingPoints = 0;

        HasRecievedControlSlotWarning = false;
        HasIncreasedControlSlot = false;
    }

    private bool CheckCanProgress(BaseCreature bc, double toGain)
    {
        if (_progressTable.TryGetValue(bc, out var count))
        {
            var gains = GetGainsPerCreature(toGain);

            if (count >= gains)
            {
                return false;
            }

            _progressTable[bc] = count + 1;
            return true;
        }

        _progressTable[bc] = 1;
        return true;
    }

    private int GetGainsPerCreature(double toGain)
    {
        var gains = ControlSlots switch
        {
            1 => int.MaxValue,
            2 => int.MaxValue,
            3 => (int)(MaxTrainingProgress / toGain / 2.0),
            _ => (int)(MaxTrainingProgress / toGain / 4.0)
        };

        if (gains < int.MaxValue)
        {
            gains *= PowerHourMultiplier;
        }

        return gains;
    }

    public void CheckProgress(BaseCreature bc)
    {
        if (ControlSlots >= ControlSlotsMax || !HasBegunTraining || TrainingProgress >= TrainingProgressMax ||
            Creature.ControlMaster == null)
        {
            return;
        }

        var ourDif = Creature.BardingDifficulty;
        var theirDif = bc.BardingDifficulty;
        var master = Creature.ControlMaster;

        if (Utility.Random(100) < 8 - (1 + (ControlSlots - ControlSlotsMin)))
        {
            var toGain = GetAdvance(theirDif);

            if (ourDif - theirDif <= 50 && CheckCanProgress(bc, toGain))
            {
                if (PowerHourBegin + PowerHourDelay < Core.Now)
                {
                    _progressTable.Clear();

                    PowerHourBegin = Core.Now;
                    InPowerHour = true;
                    // [Pet Training Power Hour]:  Your pet is under the effects of enhanced training progress for the next hour!
                    master.SendLocalizedMessage(1157569);
                }
                else if (InPowerHour && PowerHourBegin + PowerHourDuration < Core.Now)
                {
                    InPowerHour = false;
                    // [Pet Training Power Hour]:  Your pet is no longer under the effects of enhanced training progress.
                    master.SendLocalizedMessage(1157570);
                }

                TrainingProgress = Math.Min(TrainingProgressMax, TrainingProgress + toGain);

                if (!bc.Controlled && !bc.Summoned && master is PlayerMobile pm)
                {
                    var cliloc = 1157574; // *The pet's battle experience has greatly increased!*

                    if (toGain < 1.3)
                    {
                        cliloc = 1157565; // *The pet's battle experience has slightly increased!*
                    }
                    else if (toGain < 2.5)
                    {
                        cliloc = 1157573; // *The pet's battle experience has fairly increased!*
                    }

                    if (pm.HasGump<PetTrainingProgressGump>())
                    {
                        ResendProgressGump(pm);
                    }
                    else if (pm.InRange(Creature.Location, 12))
                    {
                        pm.SendGump(new PetTrainingProgressGump(pm, Creature));
                    }

                    Creature.PrivateOverheadMessage(MessageType.Regular, 0x59, cliloc, pm.NetState);

                    if (TrainingProgress >= TrainingProgressMax)
                    {
                        // *The creature surges with battle experience and is ready to train!*
                        Creature.PrivateOverheadMessage(MessageType.Regular, 0x59, 1157543, pm.NetState);

                        // ServUO: LeadingIntoBattleQuest.CheckComplete(pm); that ML quest is not on this shard.
                    }
                }
                else
                {
                    // *The pet does not appear to train from that*
                    Creature.PrivateOverheadMessage(MessageType.Regular, 0x21, 1157564, master.NetState);
                }
            }
            else
            {
                // *The pet does not appear to train from that*
                Creature.PrivateOverheadMessage(MessageType.Regular, 0x21, 1157564, master.NetState);
            }
        }
    }

    private static double GetAdvance(double difficulty)
    {
        var advance = difficulty / 64;

        if (advance >= 2.5)
        {
            advance = 2.5;
        }

        return advance;
    }

    public void ResendProgressGump(Mobile m)
    {
        if (m is not PlayerMobile pm || pm.NetState == null)
        {
            return;
        }

        var g = pm.FindGump<PetTrainingProgressGump>();

        if (g == null)
        {
            pm.SendGump(new PetTrainingProgressGump(pm, Creature));
        }
        else
        {
            g.Refresh();
        }
    }

    public override string ToString() => "...";

    // ServUO version 2. The plan rides along as ServUO writes it (a flag, then the plan).
    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(2);

        writer.WriteEncodedInt(TrainedThisLevel);

        writer.Write(PowerHourBegin);
        writer.Write(InPowerHour);

        if (_plan != null)
        {
            writer.WriteEncodedInt(1);
            _plan.Serialize(writer);
        }
        else
        {
            writer.WriteEncodedInt(0);
        }

        writer.WriteEncodedInt((int)TrainingMode);
        writer.Write(HasBegunTraining);
        writer.Write(HasIncreasedControlSlot);
        writer.Write(HasRecievedControlSlotWarning);
        writer.Write(TrainingProgress);
        writer.Write(TrainingProgressMax);

        writer.WriteEncodedInt(_startingTrainingPoints);
        writer.WriteEncodedInt(_trainingPoints);
    }

    public void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        switch (version)
        {
            case 2:
                TrainedThisLevel = reader.ReadEncodedInt();
                goto case 1;
            case 1:
                PowerHourBegin = reader.ReadDateTime();
                InPowerHour = reader.ReadBool();
                break;
        }

        if (reader.ReadEncodedInt() == 1)
        {
            _plan = new PlanningProfile(Creature);
            _plan.Deserialize(reader);
        }

        TrainingMode = (TrainingMode)reader.ReadEncodedInt();
        HasBegunTraining = reader.ReadBool();
        HasIncreasedControlSlot = reader.ReadBool();
        HasRecievedControlSlotWarning = reader.ReadBool();
        TrainingProgress = reader.ReadDouble();
        TrainingProgressMax = reader.ReadDouble();

        _startingTrainingPoints = reader.ReadEncodedInt();
        _trainingPoints = reader.ReadEncodedInt();
    }
}
