// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Fish/Bait.cs -> ours, High Seas fishing, second half.
//
// A cup of bait for one row of FishInfo's table (Index): fish bait goes on a fishing pole, crab and lobster bait on a
// lobster trap. Double-click it in the pack, say how much to use (when the cup holds more than one), target the pole or
// trap: the pole or trap takes that bait type and that many uses (a different bait is swapped out), and enhanced bait
// makes it enhanced. Targeting another cup of the same bait pours this one into it. A baited pole or trap rolls its
// fish first and at twice the chance (three times enhanced), FishInfo.GetBaitStrength; each catch uses one.
// Players get bait as a profession fish quest reward (FishQuestHelper.GiveRewards); staff can [add Bait <index>.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - The amount prompt uses pinned's Prompt; a typed amount below 1 is taken as 1, above the cup's uses as all of them,
//   as ServUO's TryBeginTarget does.
// - ServUO's IsFishBait gave an unset cup (the no-argument constructor) a random index on first use; kept.

using System;
using ModernUO.Serialization;
using Server.Prompts;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Bait : Item
{
    public static readonly bool UsePrompt = true;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private int _usesRemaining;

    private int _index;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(2)]
    private bool _enhanced;

    [Constructible]
    public Bait(int index) : base(2454)
    {
        Index = index;
        _usesRemaining = 1;
    }

    [Constructible]
    public Bait() : base(2454)
    {
        _index = -1;
        _usesRemaining = 1;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public Type BaitType => _index < 0 ? null : FishInfo.GetTypeFromIndex(_index);

    // ServUO's Deserialize clamps a saved index into the table the same way.
    [SerializableProperty(1, useField: nameof(_index))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int Index
    {
        get => _index;
        set
        {
            var count = FishInfo.FishInfos.Count;
            _index = Math.Clamp(value, 0, count - 1);
            Hue = FishInfo.GetFishHue(_index);
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            return;
        }

        if (UsePrompt && _usesRemaining > 1)
        {
            from.SendMessage("How much bait would you like to use?");
            from.Prompt = new InternalPrompt(this);
        }
        else
        {
            from.Target = new InternalTarget(this, 1);
            from.SendMessage("Target the fishing pole or lobster trap that you would like to apply the bait to.");
        }
    }

    public void TryBeginTarget(Mobile from, int amount)
    {
        if (amount < 0)
        {
            amount = 1;
        }

        if (amount > _usesRemaining)
        {
            amount = _usesRemaining;
        }

        from.Target = new InternalTarget(this, amount);
        from.SendMessage("Target the fishing pole or lobster trap that you would like to apply the bait to.");
    }

    public override void AddNameProperty(IPropertyList list)
    {
        var label = FishInfo.GetFishLabel(_index);

        if (_enhanced)
        {
            if (label is int number)
            {
                list.Add(1116464, $"{1116470:#}\t{number:#}"); // ~1_token~ ~2_token~ bait
            }
            else if (label is string text)
            {
                list.Add(1116464, $"{1116470:#}\t{text}");
            }
        }
        else if (label is int number)
        {
            list.Add(1116465, $"{number:#}"); // ~1_token~ bait
        }
        else if (label is string text)
        {
            list.Add(1116465, text);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1116466, _usesRemaining); // amount: ~1_val~
    }

    public bool IsFishBait()
    {
        if (BaitType == null)
        {
            Index = Utility.RandomMinMax(0, 34);
        }

        return !BaitType.IsSubclassOf(typeof(BaseCrabAndLobster));
    }

    private class InternalPrompt : Prompt
    {
        private readonly Bait _bait;

        public InternalPrompt(Bait bait) => _bait = bait;

        public override void OnResponse(Mobile from, string text)
        {
            _bait.TryBeginTarget(from, Utility.ToInt32(text));
        }

        public override void OnCancel(Mobile from)
        {
            from.SendMessage("Not applying bait...");
        }
    }

    private class InternalTarget : Target
    {
        private readonly int _amount;
        private readonly Bait _bait;

        public InternalTarget(Bait bait, int amount) : base(0, false, TargetFlags.None)
        {
            _bait = bait;
            _amount = amount;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted == _bait)
            {
                return;
            }

            if (targeted is FishingPole pole)
            {
                if (!_bait.IsFishBait())
                {
                    from.SendMessage("Think again before applying lobster or crab bait to a fishing pole!");
                    return;
                }

                if (pole.BaitType != null && pole.BaitType != _bait.BaitType)
                {
                    from.SendMessage("You swap out the old bait for new.");
                }

                if (pole.BaitType != _bait.BaitType)
                {
                    pole.BaitType = _bait.BaitType;
                }

                pole.BaitUses += _amount;

                if (_bait.Enhanced)
                {
                    pole.EnhancedBait = true;
                }

                from.SendLocalizedMessage(1149759); // You bait the hook.
                _bait.UsesRemaining -= _amount;
            }
            else if (targeted is LobsterTrap trap)
            {
                if (_bait.IsFishBait())
                {
                    from.SendMessage("Think again before applying fish bait to a lobster trap!");
                    return;
                }

                // ServUO sets the trap's bait before it compares, so its swap message never shows; kept.
                trap.BaitType = _bait.BaitType;
                trap.BaitUses += _amount;

                if (_bait.Enhanced)
                {
                    trap.EnhancedBait = true;
                }

                from.SendLocalizedMessage(1149760); // You bait the trap.
                _bait.UsesRemaining -= _amount;
            }
            else if (targeted is Bait other && other.IsChildOf(from.Backpack) && other.BaitType == _bait.BaitType)
            {
                other.UsesRemaining += _amount;
                _bait.UsesRemaining -= _amount;

                if (_bait.UsesRemaining <= 0)
                {
                    _bait.Delete();
                    from.SendLocalizedMessage(1116469); // You combine these baits into one cup and destroy the other cup.
                }
                else
                {
                    from.SendMessage("You combine these baits into one cup.");
                }

                return;
            }

            if (_bait.UsesRemaining <= 0)
            {
                _bait.Delete();
                from.SendLocalizedMessage(1116467); // Your bait is used up so you destroy the container.
            }
        }
    }
}
