# Adaptive Combat Morale System — Implementation Plan

## Concept

Boids dynamically adjust their combat behavior based on **flock health** (squad-level HP ratio) and **flock strength** (remaining member count). This creates three distinct combat "morale" states that emerge naturally from the flock's condition.

## Morale States

| State | Condition | Behavior |
|-------|-----------|----------|
| **Confident** | High HP + high member count | Keep formation during combat, coordinated attacks, tight cohesion |
| **Cautious** | Moderate losses | Break formation, spread out, use hit-and-run, fight independently |
| **Broken** | Critical HP or heavy losses | Flee from combat, disengage targets, retreat to spawn/anchor point |

## Key Formula

```
flockHealthRatio  = sum(boid.HitPoints) / sum(boid.MaxHitPoints)         [0..1]
flockStrengthRatio = currentMemberCount / initialMemberCount              [0..1]

moraleScore = flockHealthRatio * healthWeight + flockStrengthRatio * strengthWeight   [0..1]
```

- `moraleScore > confidentThreshold` (e.g. 0.7) → **Confident**
- `moraleScore > brokenThreshold` (e.g. 0.3) → **Cautious**
- `moraleScore ≤ brokenThreshold` → **Broken**

Hysteresis: transitioning UP requires exceeding the threshold by a small margin (e.g. +0.05) to prevent rapid flickering.

## File Changes

### 1. `BoidSettings.cs` — Add morale configuration fields

New `[Header("Adaptive Combat Morale")]` section:

```
bool  useAdaptiveMorale         = false    // Toggle: opt-in per flock
float confidentThreshold        = 0.7      // moraleScore above this → Confident
float brokenThreshold           = 0.3      // moraleScore below this → Broken
float moraleHysteresis          = 0.05     // Prevents rapid state flickering
float healthWeight              = 0.6      // Weight of HP ratio in morale score
float strengthWeight            = 0.4      // Weight of member count in morale score
float fleeSpeedMultiplier       = 1.5      // Speed boost when fleeing
float confidentFormationWeight  = 0.6      // Formation tightness multiplier in Confident
```

### 2. `BoidsManager.cs` — Evaluate morale at the flock level

- Cache `_initialBoidCount` when boids finish first spawn
- Add a `_moraleEvalCounter` to evaluate morale every N frames (reuse `CombatSyncIntervalFrames`)
- New method `EvaluateFlockMorale()`:
  - Sums HitPoints/MaxHitPoints across all boids' VehicleBase components
  - Computes `moraleScore` with weighted formula
  - Applies hysteresis for upward transitions
  - Sets `CurrentMorale` on each boid
- Store `FlockMorale` as a public property for external readers
- Cache VehicleBase references per-boid to avoid GetComponent each frame

### 3. `Boid.cs` — React to morale state in combat

Add `[HideInInspector] public CombatMorale CurrentMorale` field.

Modify `CalculatePrimaryAcceleration()`:
- **Confident + InCombat**: Blend formation forces into combat (partial formation keeping)
  - Use `CalculateFormationAcceleration()` scaled by `confidentFormationWeight` PLUS `CalculateCombatAcceleration()` scaled by `(1 - confidentFormationWeight)`
- **Cautious + InCombat**: Current behavior (no formation, pure attack profile)
- **Broken + InCombat**: Override with flee acceleration — steer away from nearest threat at max speed

Modify `ApplyFlockingAcceleration()`:
- **Confident**: Use normal (non-combat) cohesion/alignment multipliers even during combat
- **Cautious**: Use current combat multipliers (reduced cohesion, high separation)
- **Broken**: Zero cohesion/alignment, maximum separation

### 4. `BoidAttackBehavior.cs` — Support flee behavior

New method `GetFleeDirection(Vector3 threatPosition)`:
- Returns direction directly away from threat
- Speed multiplier set to `fleeSpeedMultiplier`

New property `IsFleeing` checked by Boid to skip weapon engagement.

### 5. New enum `CombatMorale` — Add to BoidSettings.cs or standalone

```csharp
public enum CombatMorale { Confident, Cautious, Broken }
```

## State Transition Diagram

```
                  moraleScore rises
     ┌─────────────────────────────────────┐
     │                                     │
  BROKEN ──── moraleScore > broken+hyst ──► CAUTIOUS ──── moraleScore > confident+hyst ──► CONFIDENT
     ▲                                       │    ▲                                           │
     │            moraleScore ≤ broken        │    │        moraleScore ≤ confident            │
     └────────────────────────────────────────┘    └───────────────────────────────────────────┘
```

## Flow Per Frame (in BoidsManager.Update)

1. Every N frames: `EvaluateFlockMorale()` → compute score → set morale on all boids
2. Each boid's `UpdateBoid()` reads its `CurrentMorale`:
   - Confident: blend formation + combat
   - Cautious: pure combat (existing behavior)
   - Broken: flee override
3. `ApplyFlockingAcceleration()` adjusts weights based on morale
4. Weapons continue firing in Confident/Cautious, stop targeting in Broken (existing `IsIdle` or skip)

## Non-Goals (out of scope)

- Individual boid morale (too noisy; flock-level is more stable and strategic)
- Morale recovery over time (could be added later; currently only recovers by healing HP)
- UI indicators for morale state (could be added separately)
