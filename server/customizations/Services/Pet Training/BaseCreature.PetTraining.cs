// cc-P121: ServUO's BaseCreature members for the ability profile (ServUO BaseCreature.cs:577-581 and 635-678), on pinned's
// BaseCreature through a partial declaration (pinned's is `public partial class BaseCreature`, BaseCreature.cs:137),
// so a ported creature calls SetSpecialAbility(SpecialAbility.X) exactly as its ServUO source does. No field is added
// and nothing here is serialized: the profile lives in AbilityProfileStore (decision 2a).
//
// SetWeaponAbility is deliberately not here. Pinned's combat reads BaseCreature.GetWeaponAbility(), not a profile, so a
// SetWeaponAbility that only filled the profile would compile and do nothing. Ports keep translating it to a
// GetWeaponAbility override (notes/port-recipe.md). Trained weapon abilities need that read point, which is P130's.

namespace Server.Mobiles;

public partial class BaseCreature
{
    /// <summary>
    /// The profile, made if the creature has none, so [props] on a stock dragon shows its breath. A profile holding only
    /// natural abilities is never written to the save (AbilityProfile.IsEmpty), so looking costs nothing.
    /// </summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public AbilityProfile AbilityProfile => PetTrainingHelper.GetAbilityProfile(this, true);

    public void SetMagicalAbility(MagicalAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).AddAbility(ability, false);

    public void SetSpecialAbility(SpecialAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).AddAbility(ability, false);

    public void SetAreaEffect(AreaEffect ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).AddAbility(ability, false);

    public void RemoveMagicalAbility(MagicalAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).RemoveAbility(ability);

    public void RemoveSpecialAbility(SpecialAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).RemoveAbility(ability);

    public void RemoveAreaEffect(AreaEffect ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).RemoveAbility(ability);

    public bool HasAbility(object o) => PetTrainingHelper.GetAbilityProfile(this, true).HasAbility(o);
}
