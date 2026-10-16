// ServUO: Mobiles/Named/Drelgor.cs (cc-P129). Old Haven's Drelgor the Impaler, the "Cleansing Old Haven" Chivalry
// quest target (Chase, 2026-10-15: the shard's own boss of that name is renamed, VorlakTheImpaler.cs, and this one is
// ported faithfully). Values verbatim; serialization by the generator: init and the next-message time are ServUO's
// two persisted fields, [SerializableField]s here. ServUO's msgevery is a constant 1.0 (minutes); kept as one.
//
// The broadcast (cliloc 1077840, "Who dares to defile Haven? ...") goes to every player who shares his "Old Haven
// Training" region. DEVIATION, recorded: pinned's regions.json has no region of that name (ServUO's Data/Regions.xml
// does, an ApprenticeRegion over Old Haven), so on this shard the test never holds and the line is never spoken,
// though he is placed where ServUO places him (ClusterFOldHavenSeeder). Reverses when an "Old Haven Training"
// region exists here. The check is kept as written so that it does.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Drelgor : BaseCreature
{
    private const double MsgEvery = 1.0; // Recurring message, minutes. ServUO: msgevery; 0 would disable it.

    [SerializableField(0)]
    private bool _init;

    [SerializableField(1)]
    private DateTime _nextMsgTime;

    [Constructible]
    public Drelgor() : base(AIType.AI_Melee)
    {
        Body = 147;
        BaseSoundID = 451;

        SetStr(127, 137);
        SetDex(82, 94);
        SetInt(48, 55);

        SetHits(131, 136);

        SetDamage(6, 8);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Cold, 60);

        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.Wrestling, 60);
        SetSkill(SkillName.Tactics, 60);
        SetSkill(SkillName.MagicResist, 60);

        Fame = 3600;
        Karma = -3600;

        VirtualArmor = 40;

        PackItem(new Scimitar());
        PackItem(new WoodenShield());

        switch (Utility.Random(5))
        {
            case 0:
                PackItem(new BoneArms());
                break;
            case 1:
                PackItem(new BoneChest());
                break;
            case 2:
                PackItem(new BoneGloves());
                break;
            case 3:
                PackItem(new BoneLegs());
                break;
            case 4:
                PackItem(new BoneHelm());
                break;
        }
    }

    public override string CorpseName => "A Drelgor The Impaler Corpse";
    public override string DefaultName => "Drelgor the Impaler";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Meager);
    }

    public override bool BleedImmune => true;

    private void Start()
    {
        _nextMsgTime = Core.Now + TimeSpan.FromMinutes(MsgEvery);

        BroadcastMessage();
        _init = true;
    }

    public void BroadcastMessage()
    {
        foreach (var state in NetState.Instances)
        {
            var m = state.Mobile;

            if (m != null && Region.Name == "Old Haven Training" && m.Region.Name == "Old Haven Training")
            {
                // Who dares to defile Haven? I am Drelgor the Impaler! I shall claim your souls as payment for this intrusion!
                m.SendLocalizedMessage(1077840, "", 34);
                m.PlaySound(0x14);
            }
        }
    }

    public override void OnThink()
    {
        if (!_init)
        {
            Start();
        }

        if (MsgEvery != 0 && Core.Now >= _nextMsgTime)
        {
            BroadcastMessage();
            _nextMsgTime = Core.Now + TimeSpan.FromMinutes(MsgEvery);
        }

        base.OnThink();
    }

    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
}
