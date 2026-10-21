// cc-P199: ServUO pub57 Scripts/Items/Functional/CoralTheOwl.cs. Coral the Owl (art 0x9A9B), a blessed house town crier.
// Locked down, it hears "news" (speech keyword 0x30) from a living player in line of sight within 12 tiles and answers
// from the global town crier list (pinned GlobalTownCrierEntryList, Mobiles/Townfolk/TownCrier.cs:20): "I have no news
// at this time." when there is none, otherwise "Some of the latest news!" and then one line of a random entry every 3
// seconds from a second later. A veteran-reward copy shows "1st Year Veteran Reward".
//
// ServUO offers it as a veteran reward (Services/VeteranRewards/RewardSystem.cs:399, miscellaneous, Expansion.ML). That
// entry is not added to pinned's reward list here (see the batch report). IsRewardItem is pinned's IRewardItem
// (Engines/Veteran Rewards/RewardSystem.cs:602), the same one member.
//
// Differences from ServUO, none visible in a single telling:
// - ServUO's news timer is unbounded and stops itself one tick after the last line (CoralTheOwl.cs:47 and :59-65); a
//   second "news" mid-telling overwrites the field, so the first timer never stops. Here each telling is a counted
//   timer of exactly its entry's lines (pinned TownCrier.cs:497 does the same), on the same 1-second delay and
//   3-second interval, so the same lines appear at the same times and nothing ticks forever. Two tellings can still
//   overlap, as in ServUO. An entry with no lines starts no timer (pinned's count 0 means forever).
// - ServUO's version-0 upgrade (marking every old owl a reward, :94-101) has no old saves to read here.

using System;
using ModernUO.Serialization;
using Server.Engines.VeteranRewards;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CoralTheOwl : Item, IRewardItem
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _isRewardItem;

    [Constructible]
    public CoralTheOwl() : base(0x9A9B) => LootType = LootType.Blessed;

    public override int LabelNumber => 1123603; // Coral the Owl

    public override bool HandlesOnSpeech => true;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (IsRewardItem)
        {
            list.Add(1076217); // 1st Year Veteran Reward
        }
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        // *news*
        if (!IsLockedDown || !e.HasKeyword(0x30) || !e.Mobile.Alive || !e.Mobile.InLOS(Location) || !e.Mobile.InRange(this, 12))
        {
            return;
        }

        var tce = GlobalTownCrierEntryList.Instance.GetRandomEntry();

        if (tce == null)
        {
            PublicOverheadMessage(MessageType.Regular, 0x3B2, 1005643); // I have no news at this time.
            return;
        }

        if (tce.Lines.Length > 0)
        {
            var index = 0;
            Timer.StartTimer(
                TimeSpan.FromSeconds(1.0),
                TimeSpan.FromSeconds(3.0),
                tce.Lines.Length,
                () => PublicOverheadMessage(MessageType.Regular, 0x3B2, false, tce.Lines[index++])
            );
        }

        PublicOverheadMessage(MessageType.Regular, 0x3B2, 502978); // Some of the latest news!
    }
}
