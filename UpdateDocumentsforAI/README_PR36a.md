# PR36a — Test repair (apply ON TOP OF PR36)

## Symptom

The unit test suite does not pass. Not one test — **28 of them**:

```
Fehler: 28, erfolgreich: 93, gesamt: 121
```

And the failing ones are not random. They are exactly the tests that were supposed to be
the safety net for the chain.

## Why this patch exists

`HANDOVER_PATCHCHAIN.md` and `README_PR36.md` both say **121/121 green**. That was never
true. The suite has been red since PR31, and the failures were invisible because the
numbers were never checked.

The three groups, and what each one was hiding:

### 1. A stale count that killed the planner check (19 tests)

`EveryRegisteredGoalHasAnActionWithMatchingEffects` starts with:

```csharp
153:  Assert.That(agent.GetGoals(), Has.Count.EqualTo(19));   // <-- FAILS HERE
154:  Assert.That(agent.GetActions(), Has.Count.EqualTo(18));
155:  Assert.That(agent.GetSensors(), Has.Count.EqualTo(6));
...
164:  var plan = new ReGoapPlanner<string, object>().Plan(agent, state, goal.GetGoalState());
165:  Assert.That(plan, Is.Not.Null);
166:  Assert.That(plan.Peek().GetName(), Is.EqualTo(actionName));   // <-- NEVER RUNS
```

PR31 added `SongGoal`, so the real count is **20 goals and 19 actions**. NUnit stops at
the first failed assertion, so lines 164–166 — *the only thing in the whole suite that
checks the planner can actually route a goal to its action* — have not executed since
PR31. Five patches of goal work landed with that check switched off.

**Fixed:** the counts are now read from the registration list, not guessed:
20 goals, 19 actions, 6 sensors.

### 2. `GetMethod` on a property (6 tests)

`typeof(MimicBrain).GetMethod("X")` returns `null` for a **property**. Several of these
members are properties, so the lookup failed on members that exist:

| Member | Kind | Test |
|---|---|---|
| `CanPlaySongs` | property | `SongsComeFromTheUsableSkillsListNotTheSpellList` |
| `ShouldHoldSongStance` | property | `SingerSwapsBetweenSwordAndInstrument` |
| `MayAttackInMelee` | property | `OnlyTheGroupedBardCaseIsWithheld` |
| `HasRangedOffensive` | property | `KitingIsGatedOnReachingPastMeleeNotOnHavingAnyHarm` |
| `IsMeleeArmed` | property | `KitingIsGatedOn…`, `MeleeArmedIsSharedBetweenFlankAndDebuff` |
| `IsBowUser` | property | `MeleeArmedIsShared…`, `RangedAttackerTestReadsTheInventoryNotTheActiveSlot` |

`PrepareMeleeStance` is the other shape: it is **private**, so the plain overload does
not find it either. It now asks with
`BindingFlags.Instance | BindingFlags.NonPublic`.

**Fixed:** `GetProperty` for properties, explicit binding flags for the private method.

This matters most for PR36: `KitingIsGatedOnReachingPastMeleeNotOnHavingAnyHarm` died on
line 740, so the two lines that actually pin the tunable threshold —
`KITE_MIN_OFFENSIVE_RANGE` — had never run. The handover tells you to tune that constant
live; nothing was checking it.

### 3. An inverted test helper (1 test)

```csharp
private static bool IsArcherClass(int classId)
    => classId != (int)eCharacterClass.Hunter
    && classId != (int)eCharacterClass.Ranger
    && classId != (int)eCharacterClass.Scout;
```

That is "is **not** an archer", written as "is an archer". The test asserted
`IsArcherClass(Scout) == true` and failed. The truth table PR27 wrote to guard the
archer exclusion was measuring the wrong classes entirely.

**Fixed:** the helper now says what its name says.

### 4. Two environment-dependent tests (2 tests)

`CorpseModelIsOffWhenNoModelIsConfigured` read the **loaded** server properties and
asserted the corpse model was non-zero:

```csharp
var model = DOL.GS.ServerProperties.Properties.MIMIC_CORPSE_MODEL;
Assert.That(model, Is.Not.EqualTo(0), "A corpse model is configured on this server");
```

`ServerProperties` never loads in a unit test — the attribute defaults are only applied
when the XML is read — so the value is always 0 and the assertion always fails. The test
was really about the *authored* default (2262, the skeleton) and the guard against a
copy/paste of the model value into the size field.

**Fixed:** the default is now read from the `ServerProperty` attribute, so the test pins
what the handover documents and does not care what the server's XML says.

`StrangerRezStaysTheLowestPriorityThingABotCanDo` builds a fake world state for
`ResurrectOutsiderGoal`. PR23 changed that goal to read the corpse the sensor already
picked, so it now needs `OUTSIDER_TO_REZ` — the fixture was never updated and the goal
scored 0.

**Fixed:** the fixture sets the key the goal actually reads.

## Files

| File | Baseline | Change |
|---|---|---|
| `Tests/Unit/GoapTests.cs` | PR36 | 13 fixes: counts, `GetProperty`, binding flags, `IsArcherClass`, corpse-model default, rez fixture |

**No production code is touched.** This patch changes nothing that runs on the server.

## Apply order

Full-file snapshots — order matters. PR36a stacks on PR36.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR36 in order …
Copy-Item "$base\Caliburn_PR36a_TestRepair\*"                $repo -Recurse -Force
```

## Tests

No new tests. **121 total, 121 green** — the 28 that were failing now pass, and the
other 93 are untouched.

```powershell
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 121/121
```

## Verify

1. Run the suite. It should be green for the first time since PR31.
2. **Read the message of the goal-routing test** — it now reaches the planner and
   asserts the action name, so a green run is real evidence for the first time.
3. Nothing to test in-game. This patch changes no behaviour.

## Known limits

- **These are still mostly existence-pokes.** The handover's own rule is "do not write
  tests that assert a method exists — assert the decision". Several of the repaired
  tests do exactly that, because rewriting them into decision tests is a different job
  than repairing them. The `GetProperty` fix makes them find the member; it does not make
  them strong.
- `CorpseModelIsOffWhenNoModelIsConfigured` now pins the *authored* default rather than
  the running server's value. If you change `mimic_corpse_model` in `serverconfig.xml`,
  this test will not notice — deliberately, because a unit test must not depend on the
  machine it runs on.
- The repair patches the **tests**, not the behaviour. Every one of the 28 failures was
  a broken assertion, not a broken feature. If you believe a specific behaviour is
  wrong, that is a separate finding and this patch does not address it.