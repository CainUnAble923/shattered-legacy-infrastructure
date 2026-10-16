// cc-P130: ServUO Scripts/Services/Pet Training/PlanningProfile.cs -> ours. The training plan: the points a tamer has
// queued (property, value, cost), with ServUO's rule that planning a magical ability zeroes the entries of the kinds
// its requirements cover. Saved by the training store (decision 2a); entries are written by name, as cc-P121 writes the
// ability profile, not by table index, so a table edit cannot shift a saved plan.

using System.Collections.Generic;
using System.Linq;
using Server.Items;

namespace Server.Mobiles;

public class PlanningProfile
{
    public BaseCreature Creature { get; }
    public List<PlanningEntry> Entries { get; }

    public PlanningProfile(BaseCreature bc)
    {
        Creature = bc;
        Entries = new List<PlanningEntry>();
    }

    public void Clear()
    {
        Entries.Clear();
    }

    public void AddToPlan(object tp, int value, int cost)
    {
        var entry = Entries.FirstOrDefault(e => Equals(e.TrainPoint, tp));

        if (entry != null)
        {
            Entries.Remove(entry);
        }

        Entries.Add(new PlanningEntry(tp, value, cost));

        if (tp is MagicalAbility ability && ability <= MagicalAbility.WrestlingMastery)
        {
            var trainingPoint = PetTrainingHelper.GetTrainingPoint(tp);

            foreach (var en in Entries)
            {
                if (trainingPoint?.Requirements is { Length: > 0 })
                {
                    foreach (var req in trainingPoint.Requirements.Where(r => r != null))
                    {
                        if (req.Requirement is WeaponAbility && en.TrainPoint is WeaponAbility ||
                            req.Requirement is SpecialAbility && en.TrainPoint is SpecialAbility ||
                            req.Requirement is AreaEffect && en.TrainPoint is AreaEffect)
                        {
                            en.Value = 0;
                            en.Cost = 0;
                        }
                    }
                }
            }
        }
    }

    public class PlanningEntry
    {
        public object TrainPoint { get; }
        public int Value { get; set; }
        public int Cost { get; set; }

        public PlanningEntry(object tp, int value, int cost)
        {
            TrainPoint = tp;
            Value = value;
            Cost = cost;
        }
    }

    // ServUO writes a kind (1-7) then an index or enum value; here the index is a name (cc-P121's rule for the ability
    // profile), so an entry whose ability no longer exists is dropped on load, not misread.
    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        writer.WriteEncodedInt(Entries.Count);

        foreach (var entry in Entries)
        {
            switch (entry.TrainPoint)
            {
                case MagicalAbility m:
                    writer.WriteEncodedInt(1);
                    writer.WriteEncodedInt((int)m);
                    break;
                case SpecialAbility s:
                    writer.WriteEncodedInt(2);
                    writer.Write(s.Name);
                    break;
                case AreaEffect a:
                    writer.WriteEncodedInt(3);
                    writer.Write(a.Name);
                    break;
                case WeaponAbility w:
                    writer.WriteEncodedInt(4);
                    writer.Write(w.GetType().Name);
                    break;
                case PetStat p:
                    writer.WriteEncodedInt(5);
                    writer.WriteEncodedInt((int)p);
                    break;
                case ResistanceType r:
                    writer.WriteEncodedInt(6);
                    writer.WriteEncodedInt((int)r);
                    break;
                case SkillName sk:
                    writer.WriteEncodedInt(7);
                    writer.WriteEncodedInt((int)sk);
                    break;
                default:
                    writer.WriteEncodedInt(0);
                    break;
            }

            writer.WriteEncodedInt(entry.Value);
            writer.WriteEncodedInt(entry.Cost);
        }
    }

    public void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        Entries.Clear();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            object tp = reader.ReadEncodedInt() switch
            {
                1 => (MagicalAbility)reader.ReadEncodedInt(),
                2 => SpecialAbility.GetByName(reader.ReadString()),
                3 => AreaEffect.GetByName(reader.ReadString()),
                4 => AbilityProfile.GetWeaponAbilityByName(reader.ReadString()),
                5 => (PetStat)reader.ReadEncodedInt(),
                6 => (ResistanceType)reader.ReadEncodedInt(),
                7 => (SkillName)reader.ReadEncodedInt(),
                _ => null
            };

            var value = reader.ReadEncodedInt();
            var cost = reader.ReadEncodedInt();

            if (tp != null)
            {
                Entries.Add(new PlanningEntry(tp, value, cost));
            }
        }
    }
}
