// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Fish/Hooks.cs -> ours, High Seas fishing, second half.
//
// The three magic fishhooks. Double-click one in the pack and target a fishing pole: it is tied on (the pole takes the
// hook's type, its uses and its hue; a hook already on the pole comes back to the pack with the uses it had left).
// Each catch wears the hook by one use (FishingPole.OnFishedHarvest, from the Fishing patch); at none the pole is a
// plain pole again.
//   - Lava hook: the pole fishes lava (HighSeasFishing: lava fish, lava rocks, stone footwear and pavers).
//   - Dredging hook: shipwreck items pulled up for an SOS are barnacle covered (BarnacleShipwreckedItem).
//   - Junk-proof hook: four times in five the cast skips the footwear row of the catch table.
// Players get hooks as profession fish quest rewards (FishQuestHelper.GiveRewards); staff can [add LavaHook.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - ServUO records the pole's hue as its own on every hook tied on, so a hook tied over another kept the first hook's
//   colour as the pole's for good. Here the pole's own hue is recorded only when it carries no hook (a bug with a
//   broken result is fixed, docs/bug-list.md D-4 rule).
// ServUO's InternalTarget has no message before the cursor (a TODO there); none here either.

using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

public enum HookType
{
    None,
    Lava,
    Dredging,
    JunkProof
}

[Flippable(19270, 19271)]
[SerializationGenerator(0, false)]
public partial class BaseFishingHook : Item
{
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private int _uses;

    public BaseFishingHook() : this(50)
    {
    }

    public BaseFishingHook(int uses) : base(19270) => _uses = uses;

    public virtual HookType HookType => HookType.None;
    public override int LabelNumber => 1098140;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1149847, $"{GetCondition(_uses):#}"); // Condition: ~1_val~
    }

    public static int GetCondition(int uses) =>
        uses switch
        {
            < 10 => 1149853, // worn
            < 20 => 1149852, // fair
            < 30 => 1149851, // very good
            < 40 => 1149850, // good
            < 50 => 1149849, // excellent
            _    => 1149848  // new
        };

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.Target = new InternalTarget(this);
        }
    }

    public static int GetHookType(HookType type) =>
        type switch
        {
            HookType.Lava      => 1150888,
            HookType.Dredging  => 1150890,
            HookType.JunkProof => 1150883,
            _                  => 0
        };

    private class InternalTarget : Target
    {
        private readonly BaseFishingHook _hook;

        public InternalTarget(BaseFishingHook hook) : base(-1, false, TargetFlags.None) => _hook = hook;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not FishingPole pole)
            {
                return;
            }

            if (pole.HookType == HookType.None)
            {
                pole.OriginalHue = pole.Hue;
            }
            else
            {
                Item hook = pole.HookType switch
                {
                    HookType.Lava      => new LavaHook(pole.HookUses),
                    HookType.Dredging  => new DredgingHook(pole.HookUses),
                    HookType.JunkProof => new JunkProofHook(pole.HookUses),
                    _                  => null
                };

                if (hook != null)
                {
                    from.AddToBackpack(hook);
                }
            }

            pole.HookType = _hook.HookType;
            pole.HookUses = _hook.Uses;
            pole.Hue = _hook.Hue;
            from.SendLocalizedMessage(1150884); // You tie the hook to the fishing line.
            _hook.Delete();
        }
    }
}

[Flippable(19268, 19269)]
[SerializationGenerator(0, false)]
public partial class LavaHook : BaseFishingHook
{
    [Constructible]
    public LavaHook(int uses) : base(uses) => Hue = 2075;

    [Constructible]
    public LavaHook() => Hue = 2075;

    public override HookType HookType => HookType.Lava;
    public override int LabelNumber => 1150888;
}

[Flippable(19268, 19269)]
[SerializationGenerator(0, false)]
public partial class DredgingHook : BaseFishingHook
{
    [Constructible]
    public DredgingHook(int uses) : base(uses) => Hue = 33;

    [Constructible]
    public DredgingHook() => Hue = 33;

    public override HookType HookType => HookType.Dredging;
    public override int LabelNumber => 1150890;
}

[Flippable(19268, 19269)]
[SerializationGenerator(0, false)]
public partial class JunkProofHook : BaseFishingHook
{
    [Constructible]
    public JunkProofHook(int uses) : base(uses) => Hue = 99;

    [Constructible]
    public JunkProofHook() => Hue = 99;

    public override HookType HookType => HookType.JunkProof;
    public override int LabelNumber => 1150883;
}
