# PR38 — Where crowd control work comes from, and why it used to loop (apply ON TOP OF PR37)

## Symptom

Two symptoms, and they are the same bug seen from two sides:

- **Bots that can mezz, snare and stun barely ever use it** — and never against each other.
- **"CC spells were applied, but bards/minstrels were stuck in a loop."**

## Root cause 1: the CC work list was fed by a player's battlegroup-add-finder

`MimicGroup.CCTargets` is the list the whole CC path reads. Repo-wide, it had exactly
**one** producer in live code:

```csharp
// GameServer/ai/brain/StandardMob/StandardMobBrain.cs:702-716
foreach (StandardMobBrain brain in brainsInRadius)
{
    ...
    if (target is IGamePlayer gamePlayer)
        gamePlayer.Group?.MimicGroup.CCTargets.Add(brain.Body);   // <- the only writer
}
```

That fires only when a mob walks past a **player** in the mimic group. So for every
fight that is not "a player pulled and something BAF'd":

```
CCTargets empty
  -> TacticalSensor NUM_CONTROLLABLE_ADDS == 0        (the count was `?? 0`)
  -> ControlAvailable == false                        (adds > 0 was required)
  -> ControlAddsGoal returns 0                        (it scores off that key)
  -> ControlAdd never planned, never run
```

Bot against bot, and solo, the pipeline was dead end to end. Meanwhile
`MimicBrain.AggroList` already held *exactly* the enemies in question — it was never
consulted.

## Root cause 2: the queued-cast chain — why it looped

This is the one that produced the "stuck in a loop" report, and it is **not** obvious.

```csharp
// TacticalSensor.cs
SetBool(OffenseAvailable, canAct && targetValid && !_body.IsSilenced && !_brain.IsFleeing);
                                                            // no !IsCasting
SetBool(AttackAvailable,   canAct && targetValid && !_body.IsCasting && _brain.MayAttackInMelee);
                                                            // has it
SetBool(InterruptAvailable, canAct && enemyCasting && !_body.IsCasting);
                                                            // has it
```

`OffenseAvailable` was the only one of the three action gates that did **not** test
`!IsCasting`. That single missing term is the whole loop:

1. The CC goal fires → `CheckSpells(CrowdControl)` → `Body.CastSpell(mez)` → a
   **six-second** cast starts.
2. `OffenseAvailable` is still true, so `DealDamageGoal` (2.0) is planned →
   `CastOffensiveSpell` → `CheckSpells(Offensive)`.
3. `CheckSpells(Offensive)` has **no `IsCasting` guard**, and its opportunistic CC roll
   sat **before** the damage pool. At 50-95 % chance the roll filled `spellsToCast` with
   a **control spell**, so `if (spellsToCast.Count < 1)` was false and **the damage
   spells were never considered**. The "offensive" action was a control action half the
   time.
4. `CastingComponent.RequestStartCastSpell` returns **`true` as soon as the request is
   enqueued**, and for an NPC a second spell goes to `QueuedSpellHandler`
   (`CastingComponent.cs:145`). So the action "succeeded" — with a spell that had not
   started.
5. `OnSpellHandlerCleanUp` promotes the queued spell to the active cast. `SpellHandler`
   is never null, so `IsCasting` is **permanently true**.

The bot therefore **cast control forever**: it could not attack (`AttackAvailable` needs
`!IsCasting`), could not sing (`SongAvailable` needs `!IsCasting`), could not kite, could
not assist, could not heal. "CC spells were applied, stuck in a loop" — exactly.

## The fix

### One gate for both the count and the cast

```csharp
public bool TryPickControlSpell(GameLiving enemy, out Spell spell)
public bool IsCcPrimary            // the pool contains a CcKind.Mezzer
```

`GetCcCandidates()` now returns only enemies a cast would actually **change something
for**, and the cast path asks the same method. The number the sensor reports and the
casts the brain makes can never disagree.

That fixes a second bug for free. The old "already controlled" test read
`IsMezzed || IsStunned || IsRooted` — but a **snare sets none of them**:

```csharp
// GameServer/spells/SpeedDecreaseSpellHandler.cs:95
if (Spell.Value == 99)
    effect.Owner.IsRooted = true;
```

A snared enemy therefore stayed a candidate **forever**, the count never drained, and
the bot never went back to damage. Now the count is the *actionable* count, so it falls
to zero the moment control is on — "mezz, then return to your other goals" needs no
class policy, because whoever runs out of targets has nothing left to cast at.

### The gates that come off, and the ones that come on

| Gate | Change | Why |
|---|---|---|
| `_brain.IsMainCC` | **removed** | Only the elected controller could ever act. In a group of eight with three mezz-capable bots exactly one acted, and a solo bot — never any role — could not act at all. The role is still elected and still used by the pre-pull path in `CheckMainCC`; it is simply no longer a *licence to cast*. |
| `Group != null` | **removed** | A solo mimic against a pack is supposed to be able to mezz it. `?? 0` and an early `return false` made solo structurally impossible. |
| `MimicBody.Group.MimicGroup` unguarded | **fixed** | Threw `NullReferenceException` for an ungrouped bot. |
| `IsCcPrimary` | **added** | PR37 put roots and snares in the same pool as mezzes, so a Druid with one root took the group's control duty. Only a mezzer qualifies — capability-derived, not a class table. Stuns do not: a stun is a melee reward, not a battle plan. |
| pressure gate | **added** | A bot with a mob on it still started a multi-second mezz; the mezz got interrupted, the count was unchanged, and the next tick started it again. |
| `!IsCasting` on `OffenseAvailable` | **added** | The queued-cast chain. |

### The opportunistic CC roll is deleted

It was a `Util.Chance(50)` — 95 when the target was already on us, 0 when the target was
the group's shared one — and it ran *before* the damage pool. It is the engine of the
loop, and it duplicated a decision the GOAP now makes properly (`ControlAdd` →
`ControlAddsGoal`). A random roll inside the damage path is not a decision.

### The priority is flat

```csharp
// before
Math.Min(4f * adds, 40f)
// after
public const float Priority = 3.8f;
```

Two adds scored 8, ten scored 40 — so **the bigger the fight, the less the bot fought**,
the exact opposite of what should happen. Nothing else in the ladder scales with the
enemy count, because one cast is one cast. 3.8 sits above `DealDamageGoal` (2.0) and
`PositionalStyleGoal` (3.5), and below `KiteGoal` (6), `InterruptEnemyCasterGoal` (9)
and the 4.0 pair `AssistTrainGoal` / `TargetCallerGoal`.

### Smaller fixes on the way

- **The `Util.Random` picks are gone.** Both the target and the spell were chosen at
  random, so two mez-capable bots could pick the same add and neither could answer
  "which enemy does this class control".
- **A range gate that was missing entirely.** `CanCastOffensiveSpell` checks cast time
  and target but never range, so the caster took the highest-threat add and then *walked
  to it*. Candidates are now filtered by the spell's own `Spell.Range`.
- **The claim list had its meaning inverted.** The old code did `if (casted) Remove(...)`
  — so a mezz that **landed** vanished from the list while one that was **resisted** stayed
  in it being retried. PR38 claims on cast start, and the caster's own claim does not
  hide the target from the caster (`_myCcClaim`).
- **`ValidateCCList` would have thrown.** It hard-cast
  `((StandardMobBrain)npc.Brain).HasAggro`, but `MimicBrain` and `StandardMobBrain` are
  **siblings** — both implement `IOldAggressiveBrain`, neither derives from the other — so
  any `MimicNPC` in that list threw `InvalidCastException`. Unreachable while only BAF
  mobs went in; now the ordinary case, because two bot groups fight each other.
- **Instant control was dead on both paths.** `CanCastOffensiveSpell` requires
  `CastTime > 0`, and `CheckInstantOffensiveSpells` has a `Mesmerize` case but iterates
  `InstantHarmfulSpells`, which never receives a spell the CC branch already claimed.
  `CanUseControlSpell` accepts instant casts.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/CcClassifier.cs` | PR37 | `IsMezzer` |
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR37 | `TryPickControlSpell`, `CanUseControlSpell`, actionable `GetCcCandidates`, `IsCcPrimary`, `_myCcClaim`, `PruneCcClaims`, `IsEngagedInMelee`, `IsUnderPressure`, `ShouldWithholdControl`, `CanStartOffensiveCast`; the opportunistic roll **deleted**; the CC cast path rewritten |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR37 | `ControlAvailable` + `IsCcPrimary` + pressure; `OffenseAvailable` + `!IsCasting`; the melee scan shared with the kite gate |
| `GameServer/custom/MimicNPC/ReGoap/Goals/ControlAddsGoal.cs` | export | flat `3.8` |
| `GameServer/custom/MimicNPC/ReGoap/Core/MimicReGoapAgent.cs` | PR34 | `/mgoap status` prints `adds`, `ccPrimary`, `underPressure` |
| `Tests/Unit/GoapTests.cs` | PR37 | 7 new tests |

## Apply order

Full-file snapshots — order matters. PR38 stacks on PR37 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR37 in order …
Copy-Item "$base\Caliburn_PR38_CcCandidates\*"                $repo -Recurse -Force
```

Full server restart.

## Tests

7 new, **131 total**, all green.

- `CcCandidatesComeFromWhoeverIsActuallyAttackingUs` — every way a candidate is refused
- `EveryCcCapableBotMayControlNotOnlyTheElectedOne` — the goal scores off the candidate count
- `OnlyMezzersTakeTheControlDuty`
- `PressedSilencedOrCastingBotsWithholdControl`
- `ControlIsFlatPricedSoMoreAddsCannotOutrankKiting`
- `AQueuedControlCannotChainIntoAnEndlessCastLoop`
- `TheControlPoolHasExactlyOneDecisionPath` — a **guard**, because a deletion cannot be
  pinned by a decision test: the control pool may be read in exactly two places (the
  capability check and the single gate). A third means a second decision path.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 131/131
```

`/mgoap status` now ends with a control line:

```
Control: adds 2; ccPrimary True; underPressure False
```

1. **Bot group vs bot group** (your main case) — control should start landing within
   seconds of the pull, not only once something BAFs a player
2. **Solo mimic vs a pack** — it should use the control it carries. Against exactly one
   enemy it should use single-target spells (that split is PR40; today it picks the
   first usable spell)
3. **More than one control-capable bot should act**, not only the elected CC
4. **`adds` must drain** — mezz an add and watch the number fall. Two adds → 0 after
   both are controlled. If it sits, the bot is trying to control something it cannot
5. **Return to damage** — once `adds` is 0 the bot should be attacking or nuking, not
   idling in the control goal. **This is the decisive test for the queued-cast chain**
6. **A pressed bot** — put a mob on a bard; `underPressure True` and it must not start
   a mezz
7. **Watch for double-casts** — two bots on the same add means the claim list did not
   stop it; check `CCTargets`
8. **Watch for chasing** — a CC bot walking toward an add instead of holding its
   position means the `Spell.Range` filter is not matching your data's range values

## Known limits

- **The spell is not chosen by kind yet.** PR38 takes the first usable spell in the pool.
  "Root the tank, mezz the ranged" is **PR40**.
- **No AoE opener.** A mezz on the current target only; opening with an AoE control when
  three or more enemies are up is **PR41**.
- **A resisted control is retried.** The claim survives until the target dies, leaves
  the region or actually turns up controlled. Bounding that is **PR42**, and it is where
  `CC_IMMUNE_ENEMIES` (declared at `MimicWorldStateKeys.cs:285`, written by nothing)
  finally gets used.
- **Everyone still converges on the group's target** (`CheckGoapAssistTarget` plus the
  0.6 off-assist priorities in `DealDamageGoal`/`DebuffPriorityGoal`). Splitting onto
  adds is **PR43**.
- **The Bard/Minstrel weapon oscillation is a separate bug** — the song and the melee
  stance fought each other over the instrument. That is **PR39**.