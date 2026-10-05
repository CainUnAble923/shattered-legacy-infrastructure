// RaptorPackFriend.cs
//
// cc-P51 (F-31, ported). A raptor another raptor called into its fight (ClusterFRaptorPack). ServUO models it as
// new Raptor(true) with a serialized m_IsFriend (pub57 Raptor.cs :22-26, :54, :200, :215-216); here being this
// subclass is the flag, so upstream's Raptor keeps its version 0 save shape. Like ServUO's, a friend is not tamable,
// calls no friends of its own, and does not survive a world load: only its caller can call it again.
// Route argued in shard-migration/notes/cc-P51-staff-hub-and-raptor-pack.md, Part A.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class RaptorPackFriend : Raptor
{
    public RaptorPackFriend() => Tamable = false;

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        Delete();
    }
}
