# PR31 — Songs (apply ON TOP of PR30)

## Symptom

Bards and Minstrels never play songs. No speed song, no mana, no endurance, no
heal song — ever. They just stand there looking like a worse version of a
caster.

## Root cause: songs were written, then switched off

The original author **implemented** song logic and then disabled it behind a
`return false`:

`MimicBrain.cs` (`CheckDefensiveSpells`)
```csharp
// TODO: Instrument classes need special logic.
if (spell.NeedInstrument)
{
    return false;              // ← unconditional
    switch (spell.SpellType)   // ← unreachable dead code below
    {
        case eSpellType.PowerRegenBuff: ...
```

and in the local `CanCastDefensiveSpell`:
```csharp
// TODO: Handle instrument spells
if (spell.NeedInstrument || ...) return false;
```

Three further facts made it worse than a missing feature:

1. **The engine enforces the instrument even for NPCs.** `SpellHandler.cs:263`
   cancels any spell with `InstrumentRequirement != 0` when `CheckInstrument()`
   fails, and that is not overridden for `GameNPC` — it tests
   `Caster.ActiveWeapon.Object_Type == eObjectType.Instrument`. A song without the
   instrument equipped is cancelled with *"You stop playing your song."*
2. **Switching weapons cancels the song.** `MimicNPC.SwitchWeapon` cancels every
   active pulse whose `InstrumentRequirement` is set
   (`MimicNPC.cs:6715-6722`). `AttackSelectedTarget` forced Minstrel and solo
   Bard to `Standard` on every melee decision, so even if songs had been enabled,
   combat would have cancelled them.
3. **Songs were entirely absent from GOAP** — no sensor flag, no goal, no action.

That third point is the real reason it could never work: nothing held the
instrument stance, so a song would start and cancel itself on the next decision.

## What the database actually has

Decoded from your `spell` table (`Spell.NeedInstrument` is exactly
`InstrumentRequirement != 0`, `Spell.cs:619`):

| PackageID | × | SpellType | Values |
|---|---|---|---|
| `Bard_Speed_Song` | 5 | `SpeedEnhancement` | Clear Path → Clear Horizon, 144%→204% |
| `Bard_Power_Regen_Song` | 5 | `PowerRegenBuff` | mana |
| `Bard_Health_Regen_Song` | 5 | `HealthRegenBuff` | health |
| `Bard_End_Regen_Song` | 5 | `EnduranceRegenBuff` | endurance |

All `Target='Group'`, `Pulse=1`, instrument-required. Plus Minstrel-style songs
(`Song of Energy`, `Song of Clarity`, …) with `InstrumentRequirement = 1`.
`eInstrumentType` is `Drum=1, Lute=2, Flute=3, Harp=4`.

**No song name is hard-coded anywhere.** Selection is by `SpellType`, so anything
your DB gives an instrument requirement to works without a code change.

## The fix

**1. A dedicated song path, not the defensive path.** Songs are not defensive
spells — they need an instrument and a held stance. So both `NeedInstrument`
rejections stay (with their misleading `TODO`s rewritten to point here), and
songs get their own `SongGoal` + `ExecuteGoapSong()`.

**2. Stance discipline — the part that makes it work.** `IsSongPulsing` and
`ShouldHoldSongStance`: while a song is running the bot will not leave the
instrument slot, and the forced `Standard` switch in `AttackSelectedTarget` now
asks first. Without this, songs cancel themselves every tick.

**3. `TacticalSensor.SongAvailable`** — songs + instrument + a concrete reason.
A bot already pulsing is never offered another song. Computed last, since it walks
the bot's usable spell list.

**4. `SongGoal`** — need-scaled priority:

| Singer | Priority | Rationale |
|---|---|---|
| Bard (support-leaning) | **45** | Singing is its main job. Above adds control (40), below group heal (50) |
| Minstrel (damage-leaning) | **11** | A damage class that happens to heal. Above damage (2), below interrupts (9) |

Decided from the **class**, not the elected role — role changes with the group,
and a bard needs the speed buff either way.

**5. Song choice**, by `SpellType`:

| SongType | Played when |
|---|---|
| `SpeedEnhancement` (+`SpeedOfTheRealm`, `CombatSpeedBuff`, `CelerityBuff`) | **not in combat, or in the regen window, or the effect lapsed** |
| `EnduranceRegenBuff` / `AllRegenBuff` | group endurance below 60% |
| `PowerRegenBuff` / `HeroismBuff` | a mana class in the group below 80% |
| `HealthRegenBuff` | someone below `NPC_HEAL_THRESHOLD` — **supplement only** |
| `Mesmerize` / demezze | handled by the existing `DebuffPriorityGoal`, not here |

Songs come in five tiers, so the **strongest tier the bot owns** is chosen, not
whichever the list yielded first.

**Health song is deliberately last.** Per your note: a heal song is a small
over-time heal, never a real heal, so it must not outrank `HealGroupGoal` (50) and
effectively never will at 45/11.

**Regen window** is *derived*, not a state — there is no regen FSM state (the
states are WakingUp/Idle/FollowLeader/Aggro/Roaming/Camp/ReturnToSpawn/
Patrolling/Duel/Dead). It means: not in combat, no aggro, and health ≥ 70%.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR27 | song helpers, `ExecuteGoapSong`, stance lock, forced-switch guard |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR26 | `SongAvailable` + `SONG_TYPE` |
| `GameServer/custom/MimicNPC/ReGoap/Goals/SongGoal.cs` | **new** | the goal |
| `GameServer/custom/MimicNPC/ReGoap/Core/MimicReGoapAgent.cs` | export | register the goal |
| `GameServer/custom/MimicNPC/ReGoap/Actions/MimicTacticalAction.cs` | export | register `CastSong` |
| `Tests/Unit/GoapTests.cs` | PR30 | 3 new tests |

`MimicNPC.cs` needed **no** change: `GetAllUsableListSpells()` is already
`public virtual`, so the brain can read the song pool.

## Apply order

Full-file snapshots — order matters. PR31 stacks on PR30 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR30 in order …
Copy-Item "$base\Caliburn_PR31_Songs\*"                    $repo -Recurse -Force
```

## Tests

3 new, **110 total**: the priority band, the sensor gate, and that the goal and
its action are both registered.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 110/110 green
```

Live — test a Bard first, it is the interesting case:

1. `/mgoap status` on a Bard → `SongGoal` / `CastSong`
2. **Out of combat → speed song up**, group visibly faster, and it **stays** up.
   *This is the one to watch*: if the speed song flickers on and off, the stance
   is still being lost
3. **No `You stop playing your song.` spam in the log** — that would mean the
   stance is thrashing
4. Drop the group's mana → mana song replaces the speed song
5. Drain endurance → endurance song
6. Someone wounded → the Bard **heals with its real spell**, not the song
7. A Minstrel songs occasionally but still melees
8. A non-singer (Warrior, Cleric) shows no `SongGoal` at all

## Known limits

- **Instrument types are not enforced.** `SpellHandler.CheckInstrument()` has the
  type check commented out (patch 1.97), so any instrument plays any song.
  Re-enabling it would change behaviour for **players** too, so it is left alone.
- **Bard/Minstrel melee policy is unchanged here** — that is PR32. A grouped Bard
  will still walk into melee in this patch.
- **`Melee kiting`, the debuff root cause, the `SkillBase` RA log spam and the
  traceability list all remain open.** PR32 and the melee-kiting item both touch
  `AttackAvailable`, so sequence them together.
