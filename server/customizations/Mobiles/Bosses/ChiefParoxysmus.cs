// ServUO: Mobiles/Bosses/ChiefParoxysmus.cs (cc-P129). The Palace of Paroxysmus peerless, on our BasePeerless (CC6
// batch 8, altar-less: D-79). Values verbatim; serialization by the generator.
//
// SetAreaEffect(PoisonBreath) goes to the profile (ported line for line in cc-P121). Pinned's OnDamage has ServUO's
// signature; Mobile.Heal(int) exists; GetSpawnPosition(int) is BasePeerless's. The three Paroxysmus items, the Lard,
// the Sweat and the Scepter are pinned's; the ParrotItem and the swamp dragon statuette are ported beside this file.
// BulbousPutrification is cc-P119's. ServUO's "eats pet or summons" heal reads the pet's hits before the kill, as here.
// It runs from a Damage override, not OnDamage: pinned's Mobile.Damage calls OnDamage before it assigns the new hit
// points (Mobile.cs:6009, :6029), so a heal inside OnDamage is overwritten by the hit; ServUO assigns first. Same
// outcome as ServUO's, in the order pinned allows (the fact ChiefParoxysmusEatsAPet... pins it).

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ChiefParoxysmus : BasePeerless
{
    [Constructible]
    public ChiefParoxysmus() : base(AIType.AI_Melee)
    {
        Body = 0x100;

        SetStr(1232, 1400);
        SetDex(76, 82);
        SetInt(76, 85);

        SetHits(50000);

        SetDamage(27, 31);

        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Poison, 20);

        SetResistance(ResistanceType.Physical, 75, 85);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.Wrestling, 120.0);
        SetSkill(SkillName.Tactics, 120.0);
        SetSkill(SkillName.MagicResist, 120.0);
        SetSkill(SkillName.Anatomy, 120.0);
        SetSkill(SkillName.Poisoning, 120.0);

        PackResources(8);
        PackTalismans(5);
        Timer.DelayCall(TimeSpan.FromSeconds(1), SpawnBulbous); // BulbousPutrification

        Fame = 25000;
        Karma = -25000;

        SetAreaEffect(AreaEffect.PoisonBreath);
    }

    public override string CorpseName => "a chief paroxysmus corpse";
    public override string DefaultName => "a chief paroxysmus";

    public override bool GivesMLMinorArtifact => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 8);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new LardOfParoxysmus());

        switch (Utility.Random(3))
        {
            case 0:
                c.DropItem(new ParoxysmusDinner());
                break;
            case 1:
                c.DropItem(new ParoxysmusCorrodedStein());
                break;
            case 2:
                c.DropItem(new StringOfPartsOfParoxysmusVictims());
                break;
        }

        if (Utility.RandomDouble() < 0.6)
        {
            c.DropItem(new ParrotItem());
        }

        if (Utility.RandomBool())
        {
            c.DropItem(new SweatOfParoxysmus());
        }

        if (Utility.RandomDouble() < 0.05)
        {
            c.DropItem(new ParoxysmusSwampDragonStatuette());
        }

        if (Utility.RandomDouble() < 0.05)
        {
            c.DropItem(new ScepterOfTheChief());
        }
    }

    public override int GetDeathSound() => 0x56F;
    public override int GetAttackSound() => 0x570;
    public override int GetIdleSound() => 0x571;
    public override int GetAngerSound() => 0x572;
    public override int GetHurtSound() => 0x573;

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        base.Damage(amount, from, informMount, ignoreEvilOmen);

        if (Deleted || !Alive)
        {
            return;
        }

        // eats pet or summons
        if (from is BaseCreature creature && (creature.Controlled || creature.Summoned))
        {
            Heal(creature.Hits);
            creature.Kill();

            Effects.PlaySound(Location, Map, 0x574);
        }

        // teleports player near
        if (from is PlayerMobile && !InRange(from.Location, 1))
        {
            Combatant = from;

            from.MoveToWorld(GetSpawnPosition(1), Map);
            from.FixedParticles(0x376A, 9, 32, 0x13AF, EffectLayer.Waist);
            from.PlaySound(0x1FE);
        }
    }

    public virtual void SpawnBulbous()
    {
        for (var i = 0; i < 3; i++)
        {
            SpawnHelper(new BulbousPutrification(), GetSpawnPosition(4));
        }
    }
}
