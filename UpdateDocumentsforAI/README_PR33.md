# PR33 — Song source list + instrument in hand (apply ON TOP of PR32)

## Symptom

Songs never fired, and singers looked empty-handed:

- Instruments **were** in the inventory (flute two-hand, drum distance, lute in a
  bag) but a singer was **never holding one**
- `/mgoap status` showed **no `SongGoal` and no `CastSong`**, ever
- Yet the **song animation played** — and only the endurance song, never the speed
  song, and with no instrument in hand

## Root cause 1: I read the wrong spell list — this is why songs never fired

`MimicNPC` keeps **two** usable-spell lists:

| Line | Field | Shape | Contents |
|---|---|---|---|
| 4860 | `m_usableSkills` | `List<Tuple<Skill, Skill>>` | **the songs live here** |
| 4866 | `m_usableListSpells` | `List<Tuple<SpellLine, List<Skill>>>` | the *list-caster* spell set — **no songs** |

The `// Add Songs` block (`MimicNPC.cs:5042`) is inside **`GetAllUsableSkills()`**,
which does `m_usableSkills.FreezeWhile(...)` at `:4962` and writes
`new Tuple<Skill, Skill>(sp, sl.Key)` — `Item1` is the spell, `Item2` the line.

PR31's `GetSongs()` called `GetAllUsableListSpells()` and walked `Item2` as a
`List<Skill>`. That is the other list, so it returned an **empty** result every
time. Consequently:

- `CanPlaySongs` → always false
- `SongAvailable` → never set by the sensor
- `SongGoal` → never won the planner → no `SongGoal` / `CastSong` in the status

The fix reads the right list:

```csharp
foreach (var entry in MimicBody.GetAllUsableSkills())
{
    if (entry?.Item1 is Spell spell && spell.NeedInstrument)
        songs.Add(spell);
}
```

**That also explains the animation.** A pre-existing generic path already tries to
cast from `m_usableSkills`, so the song *started*, played its animation, and was
then cancelled — see root cause 2.

## Root cause 2: singers spawned holding a sword

`MimicNPC.SetWeapons()`, `case eSpecType.Instrument`, created three instruments
but **never called `SwitchWeapon`**. And `eActiveWeaponSlot.Standard = 0x00` is the
default — only the `default:` branch ever switched. So a singer spawned gripping
its melee weapon while the instruments sat invisible in the pack.

`SpellHandler.CheckInstrument()` reads the caster's **active weapon**, so the song
was started and then rejected — animation with no effect, and no log line saying
why. `SetInstrumentROG` has no logging at all, which is why this needed code
reading rather than a log grep.

## What this patch changes

1. **`GetSongs()` reads `GetAllUsableSkills()`** — the actual fix.
2. **`SwitchWeapon(Distance)` in the `eSpecType.Instrument` case**, so a singer
   spawns holding the drum. The melee weapon stays equipped, exactly as you
   asked: bards and minstrels need it for **rupts** and for **playing solo**.
3. **Switch and cast are separate ticks.** Casting in the same breath that changed
   the active weapon risks `CheckInstrument()` judging the cast against the
   *outgoing* weapon. Tick one steps onto the instrument, tick two plays. This is
   the codebase's own "one tick of work per decision" rule.
4. **`PrepareMeleeStance()`** — before swinging, a singer puts the instrument
   away and picks the sword up next tick. Hooked into `AttackSelectedTarget`, so
   it covers both the FSM attack and the `ExecuteGoapInterrupt` rupt.
5. **Logging on the song path** — the chosen song, its `SpellType`, whether the
   cast succeeded, and what the active weapon was if it did not. Songs had been
   failing silently for a long time.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR32 | `GetSongs()` fix, split switch/cast, `PrepareMeleeStance`, logging |
| `GameServer/custom/MimicNPC/MimicNPC.cs` | PR26 | `SwitchWeapon(Distance)` for singers |
| `Tests/Unit/GoapTests.cs` | PR32 | 2 new tests |

`MimicManager.cs` is deliberately **not** touched — it is a base-repo file and now
that the bug is root-caused the diagnostics are worth more inside the code this
patch already owns.

## Apply order

Full-file snapshots — order matters. PR33 stacks on PR32 → PR31 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR32 in order …
Copy-Item "$base\Caliburn_PR33_SongSource\*"                $repo -Recurse -Force
```

Full server restart — the instrument stance is set at spawn.

## Tests

2 new, **115 total**. Both pin the two things that broke: that songs come from the
skills list, and that both stances are reachable.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 115/115 green
```

Live — a **Bard** first, since that is where you saw it:

1. Spawn a Bard → it should be **holding the drum**, not a sword
2. Out of combat → `/mgoap status` should now show **`SongGoal` / `CastSong`**,
   and the **speed** song specifically (not endurance)
3. The speed buff should **stay up** — no flicker, and no
   `You stop playing your song.` in the log
4. A **Minstrel** should also hold the instrument and song when out of combat
5. **Rupt still works**: grouped Bard near a casting enemy caster → it switches to
   the sword and rupt
6. **Solo Bard** → it can still fight with the sword
7. A Warrior/Cleric → unchanged, no `SongGoal`

Turn up debug logging if you want the per-cast detail: the bot now logs
`Song: <name> cast <spell> (<type>) = True/False`, and on a failure also the
active weapon, which is what would finally explain a non-playing song.

## Known limits

- **`OffenseAvailable` is still ungated**, so a grouped Bard can still fire
  offensive spells. PR32 only restricts deliberate melee attacks.
- **Melee kiting, the debuff root cause, the `SkillBase` RA log spam and the
  traceability list all remain open.** PR32/PR33 and the melee-kiting item all
  touch the same stance and `AttackAvailable` logic, so sequence melee kiting next.
- `GetBestSong`'s tier pick uses `Spell.Value`, which is a `double`; the speed
  songs' values (144–204) rank correctly, but if a song family ever stores its
  tier elsewhere the strongest tier may not be the highest `Value`.
