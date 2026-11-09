# AGENTS.md — "Yatai Together" (The Festival Ramen Cart)
### Asymmetric Reframed Reality (ARR) for Paediatric Cerebral Palsy Home Rehab & Clinical Assessment

> **Source-of-truth docs (read before any work):**
> - `Docs/MVP_skeleton.md`: feature-by-feature implementation skeleton with atomic review gates
> - `Docs/game_concept_document.md`: what the game is, keyboard cluster, choreography, strict sequence law
> - `Docs/implementation_roadmap.md`: architecture, component breakdown, architectural decisions
> - `Docs/reference/HybridHotkeyActuator.reference.md`: user's original hotkey script reference
>
> If this file and the docs disagree, **stop and ask the user**.

---

## 0. Workflow Law: Build One Step at a Time, Review Gate After Each
- **Never build the whole game in one go.** Work strictly through the numbered steps in `Docs/MVP_skeleton.md` and `Docs/implementation_roadmap.md` §3.
- Each step: (1) state which step you're starting, (2) build only that step's deliverables, (3) run its Editor Play Mode acceptance test, (4) report the result and **wait for the user to approve** before starting the next step.
- Doc changes (concept, roadmap, skeleton, this file) need user review **before** any code that depends on them is written.
- Update the skeleton's step status whenever a milestone is verified.

---

## 1. Core ARR Concept & Design Law: Reframing != Distraction
- **Project concept ("Yatai Together")**: turns painful, high-friction daily stretching for children with **Unilateral Spastic Cerebral Palsy (Hemiplegia — GMFCS Level I–II, MACS Level II)** into a shared **Japanese festival ramen cart (`Yatai`)** run by the **Child (Head Ramen Chef, in VR)** and the **Parent/Clinician (Sous-Chef Partner, on the laptop)**, serving **Monkey / Po the Panda** and festival animal customers.
- **NEVER build passive distraction**: never disconnect the child from their body. The physical resistance of spastic muscles, the parent's guiding counter-force, and the real props (yarn noodles, silicone pork/egg, paper onions, plastic ladle and bowl, resistance band) are reframed 1:1 as cooking actions (**Passive Haptic Reframing**).
- **1:1 spatial congruency**:
  - The real table in front of the seated child is locked 1:1 to the virtual ramen counter (top at ~0.75 m, 1.2 × 0.6 m) whenever the child is at the stall (Prof. Mel Slater's *Place & Plausibility Illusion*).
  - **Two fixed seated viewpoints only**: `BikeSeatAnchor` (POV 1) and `FoodStallCounterAnchor` (POV 2). The **only** allowed camera change is a switch between them behind a ≥ 0.5 s comfort fade (auto at 100 m, or the parent's `V` failsafe).
  - **NEVER translate, accelerate, tilt, or rotate the `XROrigin` while the child can see.** During the bike ride the rig and cart stay still, and **the village road scrolls past** with eased, slow motion (optional comfort vignette).
- **Anti-uncanny valley & Mutator VR biofeedback**:
  - **No uncanny human avatars**: never render the parent as a human avatar. The parent's flattening hand appears as **Monkey / Po's warm paw** (Golden Nori press).
  - **Mutator VR procedural biofeedback**: broth and steam are a living shader canvas. They look rigid/crystalline when holds are paused or the wrist is jittery, and bloom into soft, flowing, bioluminescent forms as a hold progresses steadily.

---

## 2. Hybrid Hotkey Actuation & Strict Assembly Sequence Law
- **Quest tracks WHERE, the parent decides WHEN.**
  - **Continuous channel (Quest 3):** Optical hand tracking continuously drives the virtual hand so held items follow the real arm. XR Device Simulator is used in the Editor.
  - **Discrete channel (parent's physical keyboard on laptop):** every grab, release, hold, sync moment, serve, and POV switch is a parent hotkey.
- **Tracking loss must never block progress**: hold the last good pose, fade the hand in again, and keep accepting hotkeys.
- **Strict Assembly Sequence Law:**
  The ramen bowl must be assembled in this exact, glitch-proof sequence:
  $$\text{Noodles (4 } \rightarrow \text{ W 3s } \rightarrow \text{ B 3s } \rightarrow \text{ 4 } \rightarrow \text{ A)} \longrightarrow \text{Chashu Pork (1 } \rightarrow \text{ A)} \longrightarrow \text{Ajitama Egg (2 } \rightarrow \text{ A)} \longrightarrow \text{Green Onions (3 } \rightarrow \text{ A)} \longrightarrow \text{Broth via Ladle (5 } \rightarrow \text{ D)}$$
  - Out-of-order keys (e.g. pressing `1` before noodles are boiled, or pressing `4` after noodles are already in the bowl) are **rejected/locked out** with a gentle HUD notification.
- **All keys sit on the left side of the keyboard** (+ `Space`, `Enter`, `Backspace`) for one-handed parent use:

| Key | Action | Key | Action |
| --- | --- | --- | --- |
| `Space` | Bike +10 m (×10 → stall) | `1` | Pork to hand |
| `V` | Toggle POV (failsafe) | `2` | Magic egg drop into palm |
| `Q` | Curtain open / Simmer Hold rest | `3` | Onions to hand |
| hold `W` 3 s | 2-hand noodle stretch (ends pinned to both hands) | `4` | Noodles to hand / cooked noodles out of pot |
| hold `B` 3 s | Boil noodles in pot (then `4` → `A` to bowl) | `5` | Broth ladle to hand |
| hold `F` 5 s | Hand flatten → Golden Nori | `D` | Wrist pour (2.5× gain) |
| `A` / `Enter` | Release held item into bowl | `S` | Serve, Yippee + fireworks |
| `Backspace` | Zero-penalty step reset | `E` | Elbow reach alignment cue |

- **Key reads live in exactly one place** (`ParentHotkeyRouter`, via `UnityEngine.InputSystem`). `YataiSessionDirector` strictly gates actions by phase. `V`, `Q`, and `Backspace` are always valid.
- **Hold-key semantics:** releasing early **pauses** (never resets or fails). Completion latches once per step.

---

## 3. Triad Asymmetric Loop & Physical Table Mat
- **Three stakeholders, one shared reality:**
  1. **Child (Quest 3 HMD, Head Ramen Chef):** festival Yatai stall, bike ride, Monkey / Po customer, reactive Mutator broth, predictable control, rest via the curtain.
  2. **Parent (laptop, physical keyboard, Sous-Chef Partner):** the **Kitchen Deck HUD**, styled as Po's kitchen, **not** a medical chart. Displays current step, next valid hotkey, hold rings (W/B/F), bike distance, customer count, and sequence lock warnings.
  3. **Physiotherapist (session summary):** repetitions per movement, hold durations reached, pacing-gate triggers, rest breaks, and tracked wrist range.
- **Physical setup:**
  - **Chair:** seated active cycling leg stretch. Parent taps `Space` per good cycle.
  - **Overhead:** Noren curtain reach (`Q`).
  - **Zone A (top-left):** boiler pot (`B` elbow-extension boil target) + broth pot and light ladle (`5`).
  - **Zone B (top-right):** 2×2 ingredient trays: yarn noodles (`4`), silicone pork (`1`), silicone egg halves held by parent (`2`), paper/foam onions (`3`). Dedicated `egg_bowl` station with egg chute above.
  - **Zone C (bottom-right):** real plastic ramen bowl, targets for release (`A`/`Enter`), pour (`D`), and serve push (`S`).
  - **Zone D (bottom-left):** padded flat-hand pad for parent-assisted hand flatten (`F`) + resistance band (`W`).

---

## 4. CP Movement Mappings (Counteracting Flexor Synergy)
| # | Movement | Physical action | VR reframe | Key |
| --- | --- | --- | --- | --- |
| 1 | Lower-limb active cycling stretch | Seated cycling leg motion | Pedal the ramen bike 10 m per stretch to festival | `Space` |
| 2 | Overhead reach (shoulder flexion) | Arms high overhead | Roll up the Noren curtain / close it to rest | `Q` |
| 3 | Pinch & two-hand stretch (bimanual abduction) | Pinch yarn, pull hands apart | Stretch 3D noodle element dynamically between hands | `4`, hold `W` 3s |
| 4 | Elbow extension & hold | Reach forward over pot, parent supports elbow | Boil noodles, 3s clock flashes green | hold `B` 3s |
| 5 | Finger opening & pinch | Pinch silicone pork | Toppings snap to hand | `1` |
| 6 | Forearm supination | Palm-up above egg_bowl, parent drops real egg | Magic egg drops from chute onto child's palm | `2` |
| 7 | Finger opening & pinch | Pinch paper onions | Onions snap to hand | `3` |
| 8 | Wrist & finger flexor release (assisted) | Parent flattens palm on mat for 5 s | Monkey/Po's paw presses Golden Nori | hold `F` 5s |
| 9 | Active finger release | Open fingers over bowl | Ingredient drops into bowl | `A`/`Enter` |
| 10 | Gross grasp + forearm/wrist rotation | Grasp ladle, turn wrist | Pour broth with 2.5× visual-motor gain | `5`, `D` |
| 11 | Two-hand reach forward | Push bowl forward | Serve customer, Yippee + fireworks | `S` |

---

## 5. Anti-Guarding & Clinical Safety Engine
1. **Pacing gate:** while a hold (W/B/F) or carried item is active, if wrist speed exceeds threshold, pause hold, cool broth visual, and alert HUD.
2. **Predictability & child control:** soft 3-2-1 cue before actions. Child can trigger Simmer Hold (`Q`) at any second with zero penalty.
3. **Audio safety:** soft attack envelopes (≥ 0.5 s fade-in), muffled distant pops.

---

## 6. Scope, Unity Project Root & CLI Rules
- Unity project root: `/Users/phamngoctu/Documents/ARR HACKATHON/ARR HACKATHON-2026-11-04-21-48-25`
- Stack: Unity 6000.6.8f1, URP 17.6, Input System only (`activeInputHandler: 1`), OpenXR 1.19, XRI 3.7, Meta XR SDK 207.
- Put all custom scripts and assets in `Assets/_ARR_Hackathon/`.
- Preflight with `/Users/phamngoctu/.unity/bin/unity status` before touching assets.
- Build feature by feature following `Docs/MVP_skeleton.md`. Never start a step before user review and approval of the previous step.

---

## 7. Physics & Grabbable Standards (Meta Interaction SDK Law)
- **Standard Component Stack for Interactive Props:**
  Every grabbable ingredient or utensil (`noodle`, `spoon`, `meat`, `egg`, `onion`, `ladle`) must include:
  1. `Rigidbody`: Mass `~0.1-0.2`, `collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic`, `isKinematic = false` (toggled to `isKinematic = true` when snapped to a station or attach point).
  2. `Collider`: Convex primitive (Box/Sphere/Capsule) or convex MeshCollider with appropriate physics material.
  3. `Oculus.Interaction.Grabbable`: Core Meta Interaction SDK grab target.
  4. `Oculus.Interaction.HandGrab.HandGrabInteractable` / `GrabInteractable`: Injected with `Rigidbody` and `Grabbable`.
- **Station Drop Zones & Assembly Trigger Standards:**
  - Cooking and assembly stations (`boiler`, `lamian_bowl`, `egg_bowl`, `spoon_scoop_trigger`) must use Trigger Colliders (`isTrigger = true`).
  - Stations detect ingredients via `OnTriggerEnter(Collider other)` matching ingredient names/tags (`"noodle"`, `"meat"`, `"egg"`, `"onion"`, `"spoon"`, `"broth"`).
  - Snapping into a station disables physics (`rb.isKinematic = true`, zero velocity), parents to the station snap point, and triggers station logic.
- **Hybrid Hotkey Fallback Coexistence:**
  - Continuous optical tracking & physical collider detection work hand-in-hand with discrete parent hotkeys (`Keys 1–5`, `A`/`Enter`, `B`, `D`, `F`, `W`, `S`).
  - If hand tracking is occluded by the parent physically assisting the child, hotkeys act as a 100% deterministic backup, executing identical state transitions and visual activations.
- **Step 6 Stretchable Noodle Visual Fidelity Standard:**
  - The stretchable noodle element used during grabbing, stretching, boiling, and transfer must be the authentic `noodle` prefab/mesh from `Test Street03` (and `noodle_bowl`).
  - Never use a primitive cylinder or random yellow block for noodle stretch or boiler cooking visuals.


