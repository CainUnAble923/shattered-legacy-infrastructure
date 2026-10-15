// ServUO: Mobiles/Normal/Turkey.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Turkey : BaseCreature
{
    // ServUO's pair is kept as two constructors, not collapsed into one optional parameter (the port recipe's item
    // shape): a creature built by reflection with no arguments (Activator.CreateInstance, as cc-P119's test first did)
    // needs a real parameterless constructor, and the optional-parameter form has none.
    [Constructible]
    public Turkey() : this(false)
    {
    }

    [Constructible]
    public Turkey(bool tamable) : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Body = 95;
        BaseSoundID = 0x66A;

        SetStr(5);
        SetDex(15);
        SetInt(5);

        SetHits(75);
        SetMana(0);

        SetDamage(5);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 1, 5);

        SetSkill(SkillName.MagicResist, 4.0);
        SetSkill(SkillName.Tactics, 5.0);
        SetSkill(SkillName.Wrestling, 5.0);

        Fame = 150;
        Karma = 0;

        VirtualArmor = 2;

        Tamable = tamable;
        ControlSlots = 1;
        MinTameSkill = -0.9;

        _nextGobble = Core.Now;
    }

    // Body 95 is on pinned's invalid-body list (Server/Mobiles/Mobile.cs:232-239), so SafeBody turns it into 0 and the
    // turkey has no body. ServUO comments 95 out of the same list, "Used for Turkey" (Server/Mobile.cs:9726). The
    // faithful port of that core change, scoped to this one type: SafeBody is virtual (Mobile.cs:7261).
    public override int SafeBody(int body) => body == 95 ? body : base.SafeBody(body);

    public override string CorpseName => "an turkey corpse";
    public override string DefaultName => "a turkey";

    public override int Meat => 1;
    public override MeatType MeatType => MeatType.Bird;
    public override FoodType FavoriteFood => FoodType.GrainsAndHay;
    public override int Feathers => 25;

    public override int GetIdleSound() => 0x66A;

    public override int GetAngerSound() => 0x66A;

    public override int GetHurtSound() => 0x66B;

    public override int GetDeathSound() => 0x66B;

    // Not saved, as in ServUO: a reloaded turkey may gobble at once.
    private DateTime _nextGobble;

    public override void OnThink()
    {
        base.OnThink();

        if (Tamable && !Controlled && _nextGobble < Core.Now)
        {
            Say(1153511); // *gobble* *gobble*
            PlaySound(GetIdleSound());

            _nextGobble = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 240));
        }
    }
}
