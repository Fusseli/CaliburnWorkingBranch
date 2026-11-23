# PR22 — Kiting fix (apply ON TOP of the patch export)

## Apply order (important)

This folder is **not** a standalone drop-in. Apply the export first, then this:

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"          # your OpenDaoc checkout, base master @ 660394d
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR22_KiteFix\*"                    $repo -Recurse -Force
```

Full server restart afterwards (`SkillBase` and the mimic spec tables build once
at startup).

**`Caliburn_PR23_RezFix` stacks on top of this folder**, because both replace
`MimicBrain.cs`, `TacticalSensor.cs` and `GoapTests.cs` as full-file snapshots.
If you apply both, this one must go first.

## Symptom

Solo caster mimics did not kite — they "ran some distance, turned toward the
enemy, ran again", forever. The legacy FSM path kited correctly; the GOAP path
did not.

## Root cause

`MimicBrain.ExecuteGoapKite()` had no terminal state. Every tick that was not
already travelling it cleared the flee flags and called
`Flee(max(400, 2000 - distanceToTarget))`. Once the bot reached the 2000 ring,
`2000 - distance` evaluated to 0 and was clamped **up** to 400 by
`if (dist < 400) dist = 400;` — so a completed flight immediately started
another one. Combined with `SelectGoapAttackTarget()` re-assigning
`Body.TargetObject` at the top of every combat GOAP tick (which re-orients the
bot), the result was a 400-unit hop loop: run, turn, run, turn.

The legacy FSM (`MimicBrain.cs`, defensive-spell branch) *does* have the missing
branch, and it is what made the FSM feel right:

```csharp
if (Body.GetDistance(TargetFleePosition) < 5)     // explicit arrival test
{
    IsFleeing = false;
    TargetFleePosition = null;
    if (Body.IsWithinRadius(Body.TargetObject, 400)) { Flee(1800); return; }  // too close
    if (Body.TargetObject != Body) Body.TurnTo(Body.TargetObject);             // hold
}
```

`ExecuteGoapKite` dropped both the arrival test and the hold.

Regression origin: **PR14**, not PR6. PR6 shipped a fixed `Flee(600)` hop; PR14
("kite rework") introduced the `2000 - distance` arithmetic plus the 400 clamp
and removed the arrival/hold branch.

## What this patch changes

### 1. `GameServer/custom/MimicNPC/ai/MimicBrain.cs`

**`ExecuteGoapKite()` — restores the FSM's terminal state.** A finished flight
is now judged instead of blindly restarted:

- flight still in progress → leave it alone (unchanged)
- flight finished **and** still inside `KITE_HOLD_RANGE` → `Flee()` the
  remaining distance, a long decisive break
- flight finished **and** outside the hold range → clear the flags,
  `StopFollowing()`, `TurnTo(Body.TargetObject)`, return. With `IsFleeing` false
  and no enemy inside 200, `pressured` goes false, `KiteAvailable` goes false,
  `KiteGoal.IsGoalSatisfied` returns true — the bot holds and casts, which is
  the intended kite loop.

The `if (dist < 400) dist = 400;` clamp is gone as a distance-manufacturing
device. It survives as `KITE_MIN_HOP`, which is only reached when the bot is
*starting* a kite (never fled, or arrived still too close) — where a short
opening hop is correct.

**New pure, unit-testable surface** (same convention as the existing
`ShouldKeepKiting`):

| Member | Purpose |
|---|---|
| `ShouldKeepFleeing(distanceToTarget, holdRange)` | did the flight get us out of reach? |
| `KITE_FLIGHT_DISTANCE = 2000` | ring the caster aims for |
| `KITE_HOLD_RANGE = 400` | inside → keep flying; outside → hold (legacy FSM value) |
| `KITE_MIN_HOP = 400` | shortest flight we ever issue |
| `KITE_NO_TARGET_FLIGHT = 1200` | target lost mid-decision, fixed break-away |

**`GetFleePoint()` — fixes a dead null-check.** The "surrounded, no flee point"
bail-out tested `if (point == null)`, but `point` was assigned from
`GetPointFromHeading` before the test and can never be null. So the guard never
fired and the bot would path at a point lying in no zone (run at a wall, stall,
re-flee next tick). The guard now re-tests the zone. Pre-existing base code, not
a PR regression — but PR14 called it once per 400-hop, which made it constant.

### 2. `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs`

The hold-ring scan was gated on `_brain.IsFleeing`:

```csharp
if (solo && combat && isCaster && canAct && _brain.IsFleeing)   // dead gate
```

but `ExecuteGoapKite` clears `IsFleeing` the moment it holds — so the ring could
never fire, and the sensor and action disagreed about what "hold" means. The scan
now runs whenever the bot could kite, and reads
`DOL.AI.Brain.MimicBrain.KITE_HOLD_RANGE` (was a hardcoded 700) so both sides
use one number. PR14's ring logic is now reachable.

Note this cannot make a kite start on its own:
`ShouldKeepKiting(pressured, fleeing, ring) => pressured || (fleeing && ring)`,
so a ring with no active flight and no pressure still yields false.

### 3. `Tests/Unit/GoapTests.cs`

Two new tests, 88 total (was 86):

- `FinishedKiteFlightHoldsInsteadOfHopping` — pins the hold/run decision at, just
  inside, exactly on and well clear of the ring, and that holding satisfies the
  goal.
- `KiteRingIsSharedBySensorAndAction` — pins the constants so the sensor and the
  action can never drift apart again.

## Expected behaviour change

A solo pressured caster now: breaks away in one long flight to ~2000, **stops**,
faces the enemy, and casts until something comes back into 200. It no longer
creeps away indefinitely. The grouped path (kite to a mate) is unchanged.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 88/88 green
```

Live: solo caster under ranged pressure → it should break away once, turn, and
start casting. Repeat with a melee class (must never kite) and with a grouped
caster (should converge on the tank, unchanged).

## Known limits (unchanged, still open)

- **Grouped-kite stale flag.** `ExecuteGoapKite`'s grouped branch sets
  `IsFleeing = true; TargetFleePosition = null; Body.Follow(mate, …)`. The only
  clear is in `SelectGoapAttackTarget`: `IsFleeing && TargetFleePosition == null
  && !Body.IsMoving`. If the follow breaks while `IsMoving` is still true, the
  flag sticks, and because `OffenseAvailable` and `DebuffAvailable` both require
  `!IsFleeing` the bot can only heal and kite for the rest of the fight. This is
  the leading suspect for "bots freeze after fights"
  (`HANDOVER_Issues.md` Issue 2) and is **deliberately not touched here** — it is
  a separate symptom and this project keeps patches independently testable.
- Rezzing has two sensor↔action gate mismatches of the same family
  (`TacticalSensor` gates on `REZ_RANGE`, `ExecuteGoapRez` gates on
  `spell.Range`; and the sensor omits `IsBeingInterrupted` in the calm case while
  the action returns false). Not touched here.
