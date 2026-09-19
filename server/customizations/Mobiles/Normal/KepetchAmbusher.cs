// ServUO: Mobiles/Normal/KepetchAmbusher.cs (CC6 batch 2). Values verbatim; serialization by the generator; the
// one persisted bool (GatheredFur, ServUO version 2) is a [SerializableField]. No ability to drop.
// Dropped: DragonBlood => 8 (D-50). ICarvable.Carve is void here (Server/Interfaces.cs:16-19), bool in ServUO.
// Fur and FurType are this type's own members (Mobiles/FurType.cs explains why).
//
// CanStealth: ServUO's BaseCreature.OnMove (Mobiles/Normal/BaseCreature.cs:4858-4880) lets a hidden creature
// with CanStealth spend AllowedStealthSteps and re-roll Stealth.OnUse when they run out. ModernUO has neither,
// and Mobile.OnMove reveals a hidden mobile whose steps are spent. The parent branch is inlined as OnMove below,
// exactly as batch 1 did for TrapdoorSpider (notes/cc6-creatures-batch1.md section 4).
// OnDamagedBySpell(Mobile) is OnDamagedBySpell(Mobile, int) here (BaseCreature.cs:1626).

using ModernUO.Serialization;
using Server.Items;
using Server.SkillHandlers;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class KepetchAmbusher : BaseCreature, ICarvable
{
    [SerializableField(0, setter: "private")]
    private bool _gatheredFur;

    [Constructible]
    public KepetchAmbusher() : base(AIType.AI_Melee)
    {
        Body = 726;
        Hidden = true;

        SetStr(440, 446);
        SetDex(229, 254);
        SetInt(46, 46);

        SetHits(533, 544);

        SetDamage(7, 17);

        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Poison, 20);

        SetResistance(ResistanceType.Physical, 73, 95);
        SetResistance(ResistanceType.Fire, 57, 70);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 70, 95);

        SetSkill(SkillName.Anatomy, 104.3, 114.1);
        SetSkill(SkillName.MagicResist, 94.6, 97.4);
        SetSkill(SkillName.Tactics, 110.4, 123.5);
        SetSkill(SkillName.Wrestling, 107.3, 113.9);
        SetSkill(SkillName.Stealth, 125.0);
        SetSkill(SkillName.Hiding, 125.0);

        Fame = 2500;
        Karma = -2500;

        PackItem(new RawRibs(5));
        // ServUO: VirtualArmor = 16 is commented out there too.
    }

    public override string CorpseName => "a kepetch corpse";
    public override string DefaultName => "a kepetch ambusher";

    // ServUO: public override bool CanStealth => true; "Stays Hidden until Combatant in range." Consumed by OnMove.
    public bool CanStealth => true;

    public override int Meat => 7;
    public override int Hides => 12;
    public override HideType HideType => HideType.Horned;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

    // ServUO: public override int Fur / FurType on BaseCreature. Here they are the creature's own.
    public int Fur => _gatheredFur ? 0 : 15;
    public FurType FurType => FurType.Brown;

    public void Carve(Mobile from, Item item)
    {
        if (!_gatheredFur)
        {
            var fur = new Fur(FurType, Fur);

            if (from.Backpack == null || !from.Backpack.TryDropItem(from, fur, false))
            {
                from.SendLocalizedMessage(1112359); // You would not be able to place the gathered kepetch fur in your backpack!
                fur.Delete();
            }
            else
            {
                from.SendLocalizedMessage(1112360); // You place the gathered kepetch fur into your backpack.
                GatheredFur = true;
            }
        }
        else
        {
            // ServUO sends this one to the player's journal, unlike Kepetch's overhead message.
            from.SendLocalizedMessage(1112358); // The Kepetch nimbly escapes your attempts to shear its mane.
        }
    }

    // Can flush them out of hiding.
    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        RevealingAction();
        base.OnDamage(amount, from, willKill);
    }

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        RevealingAction();
        base.OnDamagedBySpell(from, damage);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
    }

    public override int GetIdleSound() => 1545;
    public override int GetAngerSound() => 1542;
    public override int GetHurtSound() => 1544;
    public override int GetDeathSound() => 1543;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.1)
        {
            c.DropItem(new KepetchWax());
        }
    }

    public override void OnThink()
    {
        if (!Alive || Deleted)
        {
            return;
        }

        if (!Hidden)
        {
            var chance = 0.05;

            if (Hits < 20)
            {
                chance = 0.1;
            }

            if (Poisoned)
            {
                chance = 0.01;
            }

            if (Utility.RandomDouble() < chance)
            {
                HideSelf();
            }

            base.OnThink();
        }
    }

    private void HideSelf()
    {
        if (Core.TickCount >= NextSkillTime)
        {
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023
            );

            PlaySound(0x22F);
            Hidden = true;

            UseSkill(SkillName.Stealth);
        }
    }

    // ServUO BaseCreature.OnMove, the Hidden branch, verbatim apart from the CanStealth gate being this type's own.
    protected override bool OnMove(Direction d)
    {
        if (Hidden)
        {
            if (!Mounted && Skills.Stealth.Value >= 25.0 && CanStealth)
            {
                var running = (d & Direction.Running) != 0;

                if (running)
                {
                    if ((AllowedStealthSteps -= 2) <= 0)
                    {
                        RevealingAction();
                    }
                }
                else if (AllowedStealthSteps-- <= 0)
                {
                    Stealth.OnUse(this);
                }
            }
            else
            {
                RevealingAction();
            }
        }

        return true;
    }
}
