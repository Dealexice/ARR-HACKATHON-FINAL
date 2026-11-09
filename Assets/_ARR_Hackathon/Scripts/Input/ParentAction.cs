using System;

namespace YataiTogether
{
    /// <summary>
    /// Canonical parent Sous-Chef keyboard actions defined in AGENTS.md §2 and Docs/MVP_skeleton.md.
    /// All discrete hotkeys and continuous hold keys read by ParentHotkeyRouter map to this enum.
    /// </summary>
    public enum ParentAction
    {
        None = 0,

        // Phase 1 & Global Camera / Safety Controls
        BikePedalStretch = 1,   // Spacebar (Tap x10 -> 100m)
        TogglePov = 2,          // Key V (Tap failsafe: Bike <-> Stall POV)
        ToggleCurtainRest = 3,  // Key Q (Tap: Open Noren Curtain / Simmer Hold rest pause)
        ResetCurrentStep = 4,   // Backspace (Tap failsafe: Zero-penalty step reset)

        // Stretch & Hold Actions (Paused on release, never reset on early release)
        BandPullStretch = 5,    // Hold Key W (3s: 2-Hand Noodle Stretch)
        BoilHold = 6,           // Hold Key B (3s: Elbow Extension over Boiler Pot)
        HandFlattenHold = 7,    // Hold Key F (5s: Parent Flattens Hand -> Golden Nori Press)
        ElbowReachCue = 8,      // Key E (Tap/Hold: Elbow Reach alignment cue)

        // Ingredient & Tool Attachment (Keys 1 - 5)
        GrabPork = 9,           // Key 1 (Tap: Pinch Chashu Pork)
        DropEggIntoPalm = 10,   // Key 2 (Tap: Forearm Palm-Up -> Egg Drop above egg_bowl)
        GrabOnions = 11,        // Key 3 (Tap: Pinch Green Onions)
        GrabNoodles = 12,       // Key 4 (Tap: Pinch Raw Noodle element)
        GrabLadle = 13,         // Key 5 (Tap: Gross Grasp Broth Ladle)

        // Release, Pour & Serve Actions
        ReleaseIntoBowl = 14,   // Key A or Enter (Tap: Reach & Let Go into Ramen Bowl)
        PourBroth = 15,         // Key D (Tap: Forearm / Wrist Rotation Broth Pour)
        ServeCustomer = 16      // Key S (Tap: Two-Hand Push Forward -> Serve Customer)
    }
}
