using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishBattleAxe.cs (CC9 batch 3). Based Off Battle Axe.
    // Derives from BaseSetWeapon, not BaseAxe: GargishPincer (Items/Artifacts/Equipment/Weapons/Pincer.cs) assigns SetSkillBonuses,
    // and a set carrier cannot be inserted above a type once an instance of it exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here.
    // See shard-migration/notes/cc9-artifacts.md section 13.
    [Flippable(0x48B0, 0x48B1)]
    [SerializationGenerator(0, false)]
    public partial class GargishBattleAxe : BaseSetWeapon
    {
        [Constructible]
        public GargishBattleAxe() : base(0x48B0) => Layer = Layer.TwoHanded;

        public override double DefaultWeight => 4.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.BleedAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ConcussionBlow;

        public override int AosStrengthReq => 35;
        public override int AosMinDamage => 16;
        public override int AosMaxDamage => 19;
        public override int AosSpeed => 31;
        public override float MlSpeed => 3.50f;

        public override int OldStrengthReq => 40;
        public override int OldMinDamage => 6;
        public override int OldMaxDamage => 38;
        public override int OldSpeed => 30;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 70;
    }
}
