// cc-P262 (spec section 6): the companions' foot lines, as data, ready for the companion type.
//
// The spec says companions already have a personality field (Stoic, Cheerful, Grumpy, Vain, Timid, Zealous) and to use
// it. They do not: no companion type exists in this build (grep of server/customizations for "class \w*Companion" and for
// "personality": none outside achievement text). So the personality is read through PersonalityOf, which the companion
// type sets once it exists; until then every creature is Stoic, as the spec says for a companion with none set.
// Equip, Smothered and Bare are called from ClusterFAccessories for any creature with accessories (none have any yet);
// Hot and Cold need a barefoot companion's movement over lava, fire or snow, which waits for the companion type (OnStep).

#nullable enable

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server;

public enum CompanionPersonality
{
    Stoic,
    Cheerful,
    Grumpy,
    Vain,
    Timid,
    Zealous
}

public enum FootEvent
{
    Equip,
    Smothered,
    Bare,
    Hot,
    Cold
}

public static class AccessoryFootLines
{
    // Personality -> (Equip male, Equip female, Smothered, Bare, Hot, Cold). The spec's lines, word for word.
    public static readonly Dictionary<CompanionPersonality, string[]> Lines = new()
    {
        [CompanionPersonality.Stoic] =
        [
            "My feet are prepared for battle.", "A ring for each foot. Balanced. Acceptable.", "My toes will endure.",
            "Feet are just hands that walk. Proceed.", "Pain is information. My feet are well informed.",
            "I no longer feel my toes. This is efficient."
        ],
        [CompanionPersonality.Cheerful] =
        [
            "Look at them sparkle! My toes have never been so happy!", "Ten little toes and two fancy rings!",
            "Night night, piggies!", "Toes out! Freedom!", "Hot hot hot hot HOT!", "My toes are making snow angels!"
        ],
        [CompanionPersonality.Grumpy] =
        [
            "If anyone touches my feet, I bite.", "I hope you know how ridiculous my toes look.",
            "At least nobody has to look at them now.", "Great. Now I get splinters too.",
            "I'm filing a complaint with the League. About you. Specifically.",
            "When my toes fall off, you're carrying them."
        ],
        [CompanionPersonality.Vain] =
        [
            "Finally, feet worthy of a portrait.", "Darling, my arches deserve this.", "You are hiding a masterpiece.",
            "Let them see. Let them weep.", "These feet were not made for lava. They were made for admiration.",
            "If my pedicure chips, we are done."
        ],
        [CompanionPersonality.Timid] =
        [
            "Can... can people see my toes?", "What if someone steps on them?", "Oh thank goodness, they're hidden.",
            "I feel... very exposed.", "Ow ow ow, sorry, ow.", "Is it normal for toes to turn blue?"
        ],
        [CompanionPersonality.Zealous] =
        [
            "My toes shall shine for the League!", "Each step a prayer! Each toe a testament!",
            "Even unseen, my toes remain devoted!", "Let the earth feel my conviction!",
            "The flames test my soles, and my soul!", "Frostbite is merely devotion made visible!"
        ],
    };

    /// <summary>The companion type sets this to read its own personality field. Null: Stoic (spec: none set is Stoic).</summary>
    public static Func<BaseCreature, CompanionPersonality?>? PersonalityOf { get; set; }

    private static readonly Dictionary<(BaseCreature, FootEvent), DateTime> _lastSaid = new();

    public static string Line(CompanionPersonality personality, FootEvent e, bool female)
    {
        var row = Lines[personality];
        return e switch
        {
            FootEvent.Equip     => female ? row[1] : row[0],
            FootEvent.Smothered => row[2],
            FootEvent.Bare      => row[3],
            FootEvent.Hot       => row[4],
            FootEvent.Cold      => row[5],
            _                   => ""
        };
    }

    /// <summary>Says the line overhead. Hot and Cold keep their cooldowns (60 s and 5 min, settings).</summary>
    public static bool Say(BaseCreature companion, FootEvent e)
    {
        if (e is FootEvent.Hot or FootEvent.Cold)
        {
            var wait = TimeSpan.FromSeconds(e == FootEvent.Hot ? AccessorySettings.CompanionHotSeconds : AccessorySettings.CompanionColdSeconds);
            if (_lastSaid.TryGetValue((companion, e), out var at) && Core.Now - at < wait)
            {
                return false;
            }

            _lastSaid[(companion, e)] = Core.Now;
        }

        var personality = PersonalityOf?.Invoke(companion) ?? CompanionPersonality.Stoic;
        companion.PublicOverheadMessage(MessageType.Regular, companion.SpeechHue, false, Line(personality, e, companion.Female));
        return true;
    }

    /// <summary>
    /// For the companion type's movement: a barefoot companion on a lava or fire tile says its Hot line, in the snow
    /// region its Cold line. Nothing calls this yet (no companion type).
    /// </summary>
    public static void OnStep(BaseCreature companion, bool onLavaOrFire, bool inSnow)
    {
        if (companion.FindItemOnLayer(Layer.Shoes) != null)
        {
            return;
        }

        if (onLavaOrFire)
        {
            Say(companion, FootEvent.Hot);
        }
        else if (inSnow)
        {
            Say(companion, FootEvent.Cold);
        }
    }
}
