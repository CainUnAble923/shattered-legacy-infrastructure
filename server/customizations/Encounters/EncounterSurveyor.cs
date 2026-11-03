// cc-P776: the Lost Surveyor encounter's League surveyor. Notes in shard-migration notes/cc-P776-random-encounters.md.
// A friendly fighter the encounter's creatures go for: she defends herself, sturdy enough to last until help arrives.
// No fields of her own (version 0, nothing to migrate); she carries nothing worth looting, and the encounter's sweep
// removes her when it ends, or at the next world load if a restart comes first.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class EncounterSurveyor : BaseCreature
{
    [Constructible]
    public EncounterSurveyor() : base(AIType.AI_Melee, FightMode.Aggressor, 10, 1)
    {
        Name = "a League surveyor";
        Title = "the League surveyor";
        Body = 0x191;
        Female = true;
        Hue = Race.Human.RandomSkinHue();
        Team = 1;

        SetStr(80);
        SetDex(70);
        SetInt(60);
        SetHits(220);
        SetDamage(4, 8);

        SetSkill(SkillName.Wrestling, 60.0);
        SetSkill(SkillName.Tactics, 50.0);
        SetSkill(SkillName.MagicResist, 40.0);

        Fame = 0;
        Karma = 0;

        AddItem(new Cloak { Movable = false, Hue = 0x59C });
        AddItem(new Shirt { Movable = false, Hue = 0x47E });
        AddItem(new LongPants { Movable = false, Hue = 0x455 });
        AddItem(new Boots { Movable = false });
        AddItem(new WideBrimHat { Movable = false, Hue = 0x59C });
        AddItem(new Item(0x203C) { Movable = false, Hue = 0x45E, Layer = Layer.Hair });
    }

    public override bool ClickTitle => true;
    public override bool ShowFameTitle => false;

    // Nothing to loot: no gold, no reagents, no stray drops.
    public override void GenerateLoot()
    {
    }
}
