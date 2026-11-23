# PR42 — The heal path: reach the target, and be allowed to cast on it (apply ON TOP OF PR41)

## Symptom

Healers in a group do not heal, or heal only sometimes. A healer that sits down never
casts again.

## The wrong plan I started with (and why it was wrong)

The first idea was **"a heal needs a still body"** — stop the healer before it casts,
because `SpellHandler.CheckDuringCast` cancels a cast when the caster is moving.

**That would have been a no-op, and it would have looked like a fix.** The engine
already does the stopping:

```csharp
// GameServer/spells/SpellHandler.cs, CheckBeginCast
if (Caster is GameNPC npcOwner)
{
    HasLos = true;
    if (!Spell.IsInstantCast)
    {
        if (npcOwner.IsMoving)
            npcOwner.StopMoving();      // ← the engine stops the mover
    }
    if (npcOwner != Target)
        npcOwner.TurnTo(Target);
}
```

The cast request is processed *after* the brain tick, so the body is stopped before the
first `CheckDuringCast`. The movement interrupt is therefore **largely defused for
cast-time heals already** — which is why healers mostly work, and why this had to be
found by reading the engine rather than by adding a "stop" and seeing the buff counter
move.

## What is actually broken — three things, all verified

### ① The walk to the heal target is overwritten by the follow

`NpcMovementComponent.Tick()`:

```csharp
if (IsFlagSet(MovementState.REQUEST)) { ...; _movementRequest.Execute(); }   // the WalkTo
if (IsFlagSet(MovementState.FOLLOW))
{
    if (ServiceUtils.ShouldTickAdjust(ref _nextFollowTick))
    {
        _followTickInterval = FollowTick();     // → PathToInternal → WalkToInternal
        ...
    }
}
```

`FollowTick()` ends with `PathToInternal(destination, …)` (`:562`), and `WalkToInternal`
(`:402`) **never clears `MovementState.FOLLOW`**. So in the same movement tick the walk
destination is written and then **overwritten by the follow destination**.

And the heal target is chosen purely by health percent, with **no range consideration**
(`MimicGroup.CheckGroupHealth:360-364` — the lowest `HealthPercent` wins), so being out
of range is the *normal* case.

**=> The healer walks toward the injured member and is instantly redirected to the
leader, every 500 ms. It can never get into range.**

The fix is one line — `MimicBody.StopFollowing()` before the walk — and it is enough
because `MimicState_FollowLeader` re-issues `Follow` at lines 202 and 219, and **both
are after** the `if (TryGoap(Healing) || CheckHeals()) return;` early return at line
188. While the healer is closing, the FSM never reaches them, and `StopFollowing()` has
already cleared the movement component's FOLLOW flag. **`MimicState.cs` does not need to
change.**

### ② A sitting healer is permanently dead

The engine refuses a cast from a sitting NPC:

```csharp
// SpellHandler.cs:791
// Songs can be played even if sitting.
else if (m_caster.IsSitting)
{
    if (!quiet)
        MessageToCaster("You can't cast while sitting!", eChatType.CT_ChatType, ...);
    return false;
}
```

`MimicState_Camp` sits the bot and **has no stand-up branch** — unlike
`MimicState_FollowLeader:224-225`, which stands up again. And `CheckHeals` reported
success for a request that had only been *queued* (see ③), so the FSM early-returned at
`MimicState.cs:482` and **never reached the sit/stand logic**.

**=> Sitting made the heal fail, and the failed heal kept it sitting.**

The fix: stand up before casting. `Sit(false)` is free — the `IsSitting` setter only
acts on the false → true transition, so standing up cannot cancel anything.

### ③ `CheckHeals` asked the engine a different question, and then claimed success

| | question asked |
|---|---|
| `CheckHeals:3492` | `IsBeingInterruptedIgnoreSelfInterrupt` — **damage only** |
| `SpellHandler:805` | `InterruptRemainingDuration` — damage **and** the self-interrupt |

`GameLiving.cs:1008-1011`: `InterruptRemainingDuration` = `max(InterruptTime, SelfInterruptTime)`,
and `IsBeingInterrupted` = `IgnoreSelfInterrupt || SelfInterruptTime > now`. A melee
swing sets a **three second self-interrupt** (`SelfInterruptDurationOnMeleeAttack`),
so a healer that had just swung asked for casts the engine would refuse.

And it reported success anyway: `MimicBody.CastSpell(…)` →
`CastingComponent.RequestStartCastSpellInternal` **returns `true` as soon as the
request is enqueued** — so every later refusal (the interrupt timer, sitting,
concentration, the target dying, the target stepping out of range at the end of the
cast) was invisible to the brain. The FSM early-returned, the follow was never
re-established, and the bot stood there "healing" forever.

The fix: ask the engine's question, and **do not claim success when the cast will not
happen**:

```csharp
public static bool CanEngineStartCast(bool isInterruptLocked, bool isInstantCast)
    => isInstantCast || !isInterruptLocked;
```

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR41 | the three fixes + `CanEngineStartCast` |
| `Tests/Unit/GoapTests.cs` | PR41 | 2 new tests |

## Apply order

Full-file snapshots — order matters. PR42 stacks on PR41 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR41 in order …
Copy-Item "$base\Caliburn_PR42_HealPath\*"                   $repo -Recurse -Force
```

Full server restart.

## Tests

2 new, **140 total**, all green.

- `TheEngineGatesAreMirroredBeforeTheHealIsClaimed` — the pure predicate.
- `AHealerStandsUpAndDropsTheFollowBeforeItCasts` — a **guard over the source**, because
  the *ordering* is the whole point and cannot be pinned by a decision test: inside
  `CheckHeals`'s `#region Cast Spell`, `Sit(false)` must come before `CastSpell(` and
  `StopFollowing()` must come before `WalkTo(`.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 140/140
```

1. **A healer in a moving group** — it must actually land its heals. Watch for the
   injured member's health going up while the group is walking.
2. **A healer that has sat down** — it must stand up and heal. If a healer sits in the
   camp and never casts again, this patch did not work.
3. **Whisper the bot `Debug`** — the server log prints every heal attempt.
4. **A healer out of range of the injured member** — it should walk to the member
   instead of walking to the leader.

## Known limits — deliberately left alone

| Left alone | Why |
|---|---|
| `SpellHandler.CheckDuringCast` / `CheckBeginCast` | engine code, shared with players. The mimic must adapt, not the engine |
| `Spell.MoveCast` | a per-spell DB flag. Faking it would change the heal for players |
| the instant-heal path | it is the one part of the heal path that **works while moving** — an instant spell completes in the `Precast` branch and never reaches `CheckDuringCast`. Do not gate it on stillness |
| `AlreadyCheckedHeals` | correct and load-bearing (`CheckSpells` also calls `CheckHeals`) |
| PR40's `ShouldFallbackToFsm` / `HoldTickForCast` | correct: every FSM fallback destroys a cast |
| `MimicGroup.HealThreshold` / `EmergencyThreshold` (75 / 37) | changing them ripples through every comparison. `BackupHealGoal`'s comments say 50 — fix the *comments*, not the constant |
| `EmergencyHealGoal` at 100 | emergency heal must stay on top |
| `MimicState_Roaming`'s missing `CheckHeals()` fallback | deliberate — a roaming healer that stops to heal stops roaming |
| `CheckGroupHealth`'s 449 ms cache | it is what keeps the group bookkeeping cheap |

## Still open (each needs its own decision)

| issue | why it matters |
|---|---|
| `SongGoal` (45) outranks `HealGroupGoal` (5–50, typically < 15) | a singer **plays a song instead of healing**, and the weapon flip kills the heal. `SongGoal.cs:23` claims the opposite — it is wrong for any deficit under 45 % |
| `CureGroupGoal` (20) outranks `HealGroupGoal` | any mezzed member means cures, not heals |
| `CanCastHealSpells` = cast-time heals only | a mimic whose pool is only *instant* heals is dead: `CheckHeals` bails at `:3441` and the GOAP never offers the heal action |
| the caster's own health is never considered | a healer at 20 % with a mate at 21 % heals the mate |
| a solo healer between 75 % and 100 % is never healed | `numNeedHealing` is only set below `HealThreshold` |
| the heal target ignores range | the lowest-health member wins, however far away |
| `CheckHeals` still reports success for a queued request | the *deeper* version of ③ — observing the real cast outcome needs a "did my last cast land" flag |
| `GroupHealthSensor` is dead code | 282 lines; two sensors write the heal keys with **different membership rules** (reachable-only vs all living) |
| `MimicState_Idle` has no `TryGoap(Healing)`; `MimicState_Roaming` has no `CheckHeals()` fallback | a healer in either state heals only by luck |
