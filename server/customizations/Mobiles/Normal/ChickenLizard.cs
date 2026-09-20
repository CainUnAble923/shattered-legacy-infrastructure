// ServUO: Mobiles/Normal/ChickenLizard.cs (CC6 batch 4). Values and logic verbatim; serialization by the generator.
// Spawned by eight Ter Mur spawners (post-uoml/termur/TerMur.json), the highest-entry name on the orphan list. Body 716
// is a row in pinned Data/bodyTable.cfg ("ChickenLizard").
//
// The persisted cooldown: NextEgg is a [SerializableField], as ServUO writes it (version 1, one DateTime), following
// batch 2's GatheredFur call. It gates a per-instance yield -- one egg roll a day after bonding, then one a week -- and
// ServUO persists it; left volatile, every restart would re-arm a bonded lizard's egg roll. Read-only to a GameMaster,
// as ServUO's NextEgg is. Absolute time, as ServUO writes it, not a delta.
//
// ServUO's own logic, copied (bug list section 3 shape): the lay drops the egg at the owner's feet rather than in the
// pack. `if (from.Backpack == null || from.Backpack.TryDropItem(from, egg, false)) egg.MoveToWorld(...)` moves the egg
// to the world exactly when the pack ACCEPTED it, and does nothing at all when the pack refused it (the egg is then
// created and never placed). Copied faithfully; notes/cc6-creatures-batch4.md section 5 argues it. 5% of wild lizards
// carry an egg; a wild egg never advances (D-69), so it is a curio until an Incubator exists.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ChickenLizard : BaseCreature
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _nextEgg;

    [Constructible]
    public ChickenLizard() : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Body = 716;

        SetStr(74, 95);
        SetDex(78, 95);
        SetInt(6, 10);

        SetHits(74, 95);
        SetMana(6, 10);
        SetStam(78, 95);

        SetDamage(2, 5);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Fire, 5, 15);

        SetSkill(SkillName.MagicResist, 25.1, 29.6);
        SetSkill(SkillName.Tactics, 30.1, 44.9);
        SetSkill(SkillName.Wrestling, 26.2, 38.2);

        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = 0.0;

        if (0.05 > Utility.RandomDouble())
        {
            PackItem(new ChickenLizardEgg());
        }
    }

    public override string CorpseName => "a chicken lizard corpse";
    public override string DefaultName => "a chicken lizard";

    public override int Meat => 3;
    public override MeatType MeatType => MeatType.Bird;
    public override FoodType FavoriteFood => FoodType.Meat;

    public override int GetIdleSound() => 1511;
    public override int GetAngerSound() => 1508;
    public override int GetHurtSound() => 1510;
    public override int GetDeathSound() => 1509;

    public override bool CheckFeed(Mobile from, Item dropped)
    {
        if (from.Map == null || from.Map == Map.Internal)
        {
            return false;
        }

        var isBonded = IsBonded;
        var fed = base.CheckFeed(from, dropped);

        if (!isBonded && IsBonded)
        {
            _nextEgg = Core.Now + TimeSpan.FromDays(1);
            this.MarkDirty();
        }

        if (IsBonded && fed && Core.Now >= _nextEgg)
        {
            if (Utility.RandomBool())
            {
                var egg = new ChickenLizardEgg();

                if (from.Backpack == null || from.Backpack.TryDropItem(from, egg, false))
                {
                    egg.MoveToWorld(from.Location, from.Map);
                }
            }

            _nextEgg = Core.Now + TimeSpan.FromDays(7);
            this.MarkDirty();
        }

        return fed;
    }
}
