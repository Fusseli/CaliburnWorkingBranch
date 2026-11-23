# PR32 — Bard / Minstrel melee policy (apply ON TOP of PR31)

## Symptom

A Bard is its realm's heal, CC, cleanse **and** speed — and it was walking into
melee anyway, spending the tick that should have been a song on poor melee
attacks. A Minstrel, which is a legitimate melee class, should be left alone.

## The policy

Deliberately narrow and asymmetric, because the two classes are not
interchangeable:

| Case | Melee? | Why |
|---|---|---|
| **Bard, grouped** | **no** | stays in support; the group is missing its heal/CC/cleanse/speed, not its damage |
| Bard, solo | yes | no one to support — standing off to sing is how it dies |
| **Minstrel** | yes, always | the better melee class of the two; meant to trade blows while it songs |
| Anyone else | unchanged | |

Expressed once, as a pure decision so it is testable:

```csharp
public static bool MayMeleeAttack(bool isBard, bool isMinstrel, bool hasGroup)
    => !isBard || isMinstrel || !hasGroup;
```

## Both decision paths are covered

The important part is that this is enforced in **two** places, because the mimic
has two ways to decide to attack:

1. **GOAP** — `TacticalSensor.AttackAvailable` now also requires
   `_brain.MayAttackInMelee`, so `DealDamageGoal` never fires for a grouped Bard.
2. **FSM** — `AttackMostWanted()` returns early on the same check. Without this a
   grouped Bard would still charge in through the legacy path even though GOAP
   would never have picked a damage goal — two paths, one policy.

Both call the same `MayAttackInMelee`, so they cannot drift.

## What is deliberately NOT gated

**`InterruptAvailable` is untouched.** `ExecuteGoapInterrupt` already melee-pressures
an enemy caster first and falls back to an instant spell, and that quick rupt is
exactly the one melee a Bard is still allowed to make — you said bards should
rupt enemy casters, so that has to keep working. If `InterruptAvailable` ever starts
consulting `MayMeleeAttack`, that is a regression.

**Self-defence is exempt in the FSM.** If something is already aggroed on the
Bard, `IsUnderAttack()` lets it fight back rather than trying to retreat into a
song stance.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR31 | `IsBard`, `IsMinstrel`, `MayMeleeAttack`, `MayAttackInMelee`, `MeleeRestrictionReason`, `IsUnderAttack`, FSM guard |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR31 | `AttackAvailable` gate |
| `Tests/Unit/GoapTests.cs` | PR31 | 3 new tests |

## Apply order

Full-file snapshots — order matters. PR32 stacks on PR31 → PR30 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR31 in order …
Copy-Item "$base\Caliburn_PR32_BardStance\*"                $repo -Recurse -Force
```

## Tests

3 new, **113 total**. The main one pins the actual truth table rather than
asserting a method exists:

- `GroupedBardIsTheOnlyFrontlineRestriction` — all five cases above, including the
  Minstrel exemption and the solo-Bard exemption
- `OnlyTheGroupedBardCaseIsWithheld` — the reason helper is null exactly when
  attacking is permitted, and both paths share one definition
- `BardMeleeRestrictionDoesNotBlockInterrupts` — guards the rupt exception

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 113/113 green
```

Live, with a **grouped** Bard and a **grouped** Minstrel:

1. Grouped Bard in a fight → it **stays back** and `/mgoap status` shows
   `SongGoal`/healing rather than `DealDamageGoal`/`EngageTarget`
2. Grouped Bard near a **casting enemy caster** → it still walks in to rupt it. If
   it never does, `InterruptAvailable` got gated by mistake
3. Solo Bard with nothing else around → it fights, it does not stand and sing
4. Grouped Minstrel → still walks into melee exactly as before
5. A Warrior/Cleric in the same group → completely unchanged

## Test alongside PR31

PR31 and PR32 are independent in behaviour but touch the same two files, so
**PR32 must be applied after PR31** and both together. That is also the better
test: a Bard that stays back and keeps the group speed up is the combined
behaviour you wanted.

## Known limits

- The Bard is excluded from *deliberate damage attacks*. It still fires offensive
  spells through `ExecuteGoapSpell(Offensive)` — `OffenseAvailable` was not
  gated. If you want a Bard to hold offensive spells entirely while grouped, say
  so and it is a one-line addition.
- **`Melee kiting`, the debuff root cause, the `SkillBase` RA log spam and the
  traceability list all remain open.** PR32 and the melee-kiting item both touch
  `AttackAvailable`, so that is the natural next patch after this.
