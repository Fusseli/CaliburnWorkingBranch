# PR41 — The buff state is measured, not asserted (apply ON TOP OF PR40)

> **This patch is Part 1 of two.** It makes the buff state real and visible. It does
> **not** change which buffs are cast or in what order — see § Known limits.

## Symptom

The bot stands still, walks through its self buffs one at a time, then moves and fights.
That is **correct and intended**. What was missing was any way to answer:

- *"did the buffs actually land?"*
- *"would it re-apply them after the fight?"*

And there was one real defect underneath.

## Root cause: `BUFFS_MAINTAINED` was a hard-coded `false`

```csharp
// TacticalSensor.cs, before this patch
SetBool(MimicWorldStateKeys.BUFFS_MAINTAINED, false);
```

`BuffMaintenanceGoal.IsGoalSatisfied` reads that key:

```csharp
bool buffsMaintained = GetBool(currentState, MimicWorldStateKeys.BUFFS_MAINTAINED, true);
if (inCombat || !canCastDefensiveSpells) return true;
if (buffsMaintained) return true;
return false;
```

So with the key pinned to `false`, **the goal was never satisfied**. It re-fired on
every single tick out of combat, which:

- wasted a planning cycle every 500 ms, and
- before PR40's tick hold, churned the defensive cast queue.

And because the key was a constant, **nothing could report the buff state**. The goal's
own `GetDebugInfo` prints `BuffsMaintained`, but a goal's `GetDebugInfo` is never
surfaced — only the agent's is (`GoapCommand.cs:31`).

## The fix

### 1. The sensor asks the same question the action asks

`CanCastDefensiveSpell` was a **local function** inside `CheckDefensiveSpells`, so the
sensor could not reuse it. It is now a method, and the sensor asks:

```csharp
SetBool(MimicWorldStateKeys.BUFFS_MAINTAINED, !_brain.HasMissingSelfBuff());
```

`CountMissingSelfBuffs()` walks the bot's own defensive spells and counts the ones that
target **itself** and are genuinely missing:

- a buff that is **already up** is not missing — `LivingHasEffect` finds the effect on
  the body, so the action already skipped it;
- a buff **on recast cooldown** is not missing, because casting it would fail anyway;
- **summons are excluded** — `FindTargetForDefensiveSpell` returns the caster for those
  too, and a summon is not a buff.

Because the sensor and the action ask the *same* question, they cannot disagree.

### 2. `/mgoap status` reports it

```
Buffs: missing 2
```

right under PR38's control line. That is the answer to "did the buff land?" — and it
is the number to watch while testing.

## What this does NOT change

- **Which buffs are cast.** Still every self buff the bot has that is missing.
- **The order.** Still `Util.Random` — see § Known limits.
- **How long it takes.** One buff per cast, at the buff's own cast time.
- **The priority ladder.** `BuffMaintenanceGoal` is still 0.5 (1.5 after 10 s out of
  combat) — the lowest thing in the ladder, below `DealDamageGoal` (2.0).

## The two questions this was written to answer

**"Is the behaviour right — stand still, buff up, then fight?"**
**Yes.** That is `BuffMaintenanceGoal`'s documented sequence:

> 1. Combat ends → IN_COMBAT = false → 2. Group recovers health/mana → 3. 10+ seconds
> out of combat → priority increases to 1.5 → 4. Planner evaluates missing buffs →
> 5. Buff actions execute → Apply defensive/speed/stat buffs → 6. All critical buffs
> active → Goal satisfied → 7. PullTargetGoal can now activate

**"Would they recast their self buffs after the fight, so they still have them?"**
**Yes — and precisely:**

- **Not** the ones it still has. `LivingHasEffect` ends with
  `EffectListService.GetEffectOnTarget(target, spellEffect) != null`, so an **active**
  buff is detected and **skipped**.
- **Yes** the ones that expired or were dispelled. The goal's priority comes back once
  the bot is out of combat, and the missing-buff check finds them.

So the bot keeps a full buff set across a fight and tops up whatever expired.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR40 | `CanCastDefensiveSpell` hoisted out of the local function; `CountMissingSelfBuffs`, `HasMissingSelfBuff` |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR40 | `BUFFS_MAINTAINED` measured instead of hard-coded |
| `GameServer/custom/MimicNPC/ReGoap/Core/MimicReGoapAgent.cs` | PR40 | `/mgoap status` prints `Buffs: missing <n>` |
| `Tests/Unit/GoapTests.cs` | PR40 | 3 new tests |

## Apply order

Full-file snapshots — order matters. PR41 stacks on PR40 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR41 in order …
Copy-Item "$base\Caliburn_PR41_BuffState\*"                   $repo -Recurse -Force
```

Full server restart.

## Tests

3 new, **138 total**, all green.

- `TheBuffStateIsMeasuredRatherThanHardCoded` — a **guard**, because a hard-coded value
  cannot be pinned by a decision test: the sensor must not set `BUFFS_MAINTAINED` to a
  literal, and must ask the brain.
- `BuffMaintenanceIsSatisfiedOnlyWhenTheBuffsAreUp` — the goal's contract.
- `TheDebugStatusReportsHowManyBuffsAreMissing` — the state is visible.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 138/138
```

1. **Spawn a mimic out of aggro range** and watch `/mgoap status`. `Buffs: missing N`
   should start high and **fall to 0** as the buffs land. That falling number is the
   proof the buffs are being applied.
2. **Whisper the bot `Spell Effects`** — the active buffs should be listed.
3. **Whisper the bot `Debug`** — the server log then prints every defensive cast:
   `<bot> tried to cast <spell> on <target> and cast == true|false`, and
   `<target> has the effect already.` for the ones it skipped.
4. **After a fight**, `Buffs: missing` should be 0 if the buffs are still up, and
   climb again only for the ones that expired.
5. **A bot that is fully buffed** should stop re-planning the buff goal every tick —
   `Last goal:` in `/mgoap status` should settle on something else.

## Known limits

- **The order is still random.** `CheckDefensiveSpells` picks
  `spellsToCast[Util.Random(spellsToCast.Count - 1)]`, while `BuffMaintenanceGoal.cs:72-78`
  documents a cost hierarchy — speed → armour → stat → damage shield → resist — that was
  **never implemented** (there is no `BuffCostCalculator` in the live tree). That is
  **Part 2**.
- **The bot still applies every self buff it has**, not only the important ones. The
  goal's own doc says the resist buffs are "apply last, if time permits", but "if time
  permits" has never been given a budget. That is also Part 2.
- **The buff goal is still the lowest priority in the ladder** (0.5 / 1.5). A bot in a
  hurry to re-engage will defer its buffs.
- **The healer path is untouched.** `CheckHeals` does
  `if (MimicBody.IsCasting) MimicBody.StopCurrentSpellcast();` and then casts, with no
  `IsMoving` check — so a healer in a *moving* group may be cancelling its own heal to
  cast the next one. Worth checking on your next round: **do healers in a moving group
  land their heals?** If not, that is a separate patch, not a PR41 extra.