# PR40 — The GOAP keeps the tick while a spell is running (apply ON TOP OF PR39)

## Symptom

Reported after PR38+PR39, on the live server:

> "CC spells are applied now onto enemies, but classes like sorcerers when I spawn them solo
> are trying to apply self buffs. They start the casting animation but never finish them. They
> go through all self buffs but only for a part of a second and then go over to the next one
> and the next and then start moving but no buff is applied. The same could be for archers.
> Looks like they try to shoot / cast their arrows but never finish it."

**This is a regression introduced by PR38.** The buff path itself is byte-identical to
PR36 — `BuffsAvailable`, `BUFFS_MAINTAINED`, `CheckSpells(Defensive)`,
`CheckDefensiveSpells`, `FindTargetForDefensiveSpell`, `BuffMaintenanceGoal` and the whole
of `MimicState.cs` were verified unchanged. So the buff was not *changed*; it was
*interrupted*.

## Root cause: the GOAP handed the tick to the FSM, and every FSM fallback destroys a cast

PR38 added `!IsCasting` to `OffenseAvailable`. That was **correct** — it is what broke the
queued-cast chain. But it had a second effect nobody expected:

**While a cast is in flight, every action gate is now false, so the planner finds no
plan, so the agent falls back to the FSM — and every FSM fallback cancels the spell.**

| FSM fallback | how it cancels the cast |
|---|---|
| `AttackMostWanted` → `PrepareMeleeStance` → `SwitchWeapon(Standard)` | `MimicNPC.SwitchWeapon` → `StopCurrentSpellcast()` (`MimicNPC.cs:6794-6798`) |
| `Sit(...)` | the `IsSitting` setter → `CurrentSpellHandler?.CasterMoves()` (`MimicNPC.cs:9737`) |
| moving | `SpellHandler.CheckDuringCast` → `CasterMoves()` (`SpellHandler.cs:1375-1379`) |

Before PR38 this could not happen, because `OffenseAvailable` was still **true** while
casting, and `CheckSpells` returns `casted || Body.IsCasting` — so the action
"succeeded" without doing anything, `TryGoap` returned true, and **the FSM never ran**.

### The archer, tick by tick

```
tick N     OffenseAvailable true  -> CastOffensiveSpell
           -> CheckOffensiveSpells -> SwitchWeapon(Distance) -> the arrow cast starts

tick N+1   OffenseAvailable false (the new !IsCasting), and AttackAvailable,
           ArrowTypeAvailable, ControlAvailable, InterruptAvailable ... are all false
           -> TryPlan finds no plan
           -> Fallback("No achievable active goal") -> TryGoap returns FALSE
           -> the FSM runs AttackMostWanted()
           -> PrepareMeleeStance() -> SwitchWeapon(Standard)
           -> StopCurrentSpellcast()          => THE ARROW IS DESTROYED
```

`AttackComponent.cs:917` adds a second flip path — a Hunter/Ranger/Scout with no aggro
switches to the Distance slot, which is also a `SwitchWeapon`, which is also a cast
killer.

The same shape applies to the sorcerer: while the self-buff is running the GOAP has
nothing to plan, the FSM takes over, and its fallback (sitting, or moving) interrupts
the buff. `SpellHandler.CheckDuringCast` cancels on movement, and the user saw exactly
that — "then start moving but no buff is applied".

## The fix

### 1. The GOAP owns the tick while a cast is in flight

```csharp
// MimicReGoapAgent.TryThink
if (!planned)
{
    if (!ShouldFallbackToFsm(planned, IsCasting))
    {
        HoldTickForCast();
        return true;
    }
    return Fallback("No achievable active goal");
}
```

**One change closes all three FSM fallbacks**, because the FSM only runs them when
`TryGoap` returns false — and `TryGoap` now returns true for the whole duration of a
cast.

`/mgoap status` shows `AI: GOAP: casting` while a spell is running, instead of an FSM
reason. That is how you tell "the bot is busy casting" from "the bot had nothing to do".

`ShouldFallbackToFsm` is pure, so the decision is unit-testable without a body.

### 2. A defensive cast in flight is not re-requested

```csharp
// TacticalSensor
SetBool(BuffsAvailable, canAct && context == MimicDecisionContext.Support
    && MimicBrain.CanStartDefensiveCast(_body.IsCasting, _body.IsSilenced, _body.IsMoving, _body.InCombat));
```

`CheckDefensiveSpells` picks a **random** missing buff and `CastSpell` **queues** it
while the previous one is still casting — and `CastingComponent.StartCastSpell` does:

```csharp
QueuedSpellHandler = newSpellHandler;   // the PREVIOUS queued spell is silently discarded
```

The queue is refreshed every think tick, so the bot was **replacing its own queued buff
every tick with a different random one**, and the buff it had actually chosen never
happened. That is what "goes through all its buffs and none of them stick" looked like.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ReGoap/Core/MimicReGoapAgent.cs` | PR39 | `ShouldFallbackToFsm`, `HoldTickForCast`, the two fallback exits |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR39 | `BuffsAvailable` tests `!IsCasting` |
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR39 | `CanStartDefensiveCast` |
| `Tests/Unit/GoapTests.cs` | PR39 | 2 new tests |

## Apply order

Full-file snapshots — order matters. PR40 stacks on PR39 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR39 in order …
Copy-Item "$base\Caliburn_PR40_CastOwnership\*"               $repo -Recurse -Force
```

Full server restart.

## Tests

2 new, **135 total**, all green.

- `TheGoapKeepsTheTickWhileASpellIsRunning`
- `ADefensiveCastIsNotRequestedWhileOneIsRunning`

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 135/135
```

1. **Archer** — the shot must complete. The arrow animation must finish and the arrow
   must land, over and over, not restart every second.
2. **Sorcerer** — a solo sorcerer's self buffs must **apply**. Watch for the buff icons
   appearing. It should take a few seconds (one buff at a time), not a fraction of a
   second.
3. **`/mgoap status`** while a cast is running — `AI:` should read **`GOAP: casting`**,
   not `FSM: No achievable active goal`. If it still reads `FSM:`, the FSM is still
   taking the tick and something else is cancelling the cast — report it.
4. **A bard** — the song must still play and keep playing (PR39).
5. **CC** — control must still land (PR38).

## Known limits

- **The offensive path still queues.** A mage queuing a nuke after a snare is
  unchanged, and is deliberate. Only the *defensive* path stopped re-requesting,
  because there the random pick plus the silent queue discard made it pointless.
- **A cast that is interrupted by something else is still interrupted.** This patch stops
  the FSM from interrupting it; it does not stop a mob hitting the bot.
- **The buffer of 5 s for `IsCasting` to clear.** `Body.IsCasting` is
  `SpellHandler != null`, so a *queued* spell does not count as casting. A spell that
  has been requested but not started yet is not protected by the tick hold. That window
  is one engine tick wide.
- The next CC work is still open: choosing the spell **by kind** (root the tank, mezz
  the ranged) is **PR42**, the AoE opener is **PR43**, immunity memory is **PR44**, and
  spreading onto adds is **PR45**.