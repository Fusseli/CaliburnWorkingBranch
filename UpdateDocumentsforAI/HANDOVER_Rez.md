# Handover: mimic rez (PR16–PR18)

Companion to `HANDOVER.md` (base GOAP stack PR1–PR10 + sessions through PR15,
see `SESSION_SUMMARY.md`) and `HANDOVER_Archery.md` (PR11–PR13). Same
conventions: stacked drop-in folders, each only its own delta, verified in
temp overlays before delivery.

Server context: no new realm logic. Rezzing reuses the engine's existing
`CORPSE` + `IsSameRealm` checks, i.e. **PvE zones: anyone rezzes anyone;
RvR zones: same realm only; grouped cross-realm passes** (Coop rules).
Player→bot rez takes the NPC instant branch (auto-accept, no 15s dialog).

## The patches (copy in order, each over the previous, test between steps)

```powershell
Copy-Item "<Folders>\Caliburn_PR16_RezCorpse\*" "<YourCaliburn>\" -Recurse -Force
# ... then PR17_GroupRez, PR17b_OutsideRez, PR18_BattleRez in order
```

| # | Folder | Files | What it adds |
|---|---|---|---|
| PR16 | `Caliburn_PR16_RezCorpse` | 8 | Corpse foundation, **no casting yet**. Non-duel deaths linger `Active`+`!IsAlive` for 60s (`MIMIC_CORPSE_WINDOW_SECONDS`) instead of instant `Delete`; kill credit, loot and RP/XP unchanged; spawner slot + group membership freed only on expiry (`ReleaseCorpse`). Duels keep legacy instant delete. Healers stop counting corpses as heal jobs. |
| PR17 | `Caliburn_PR17_GroupRez` | 17 | Group rez, **out-of-combat only**. `ResurrectGoal` (15) / `CastRez` → nearest dead mate in 1500 range, same realm. `/mrez` toggles a bot's rez duty (needs a rez spell, else it tells you). Player→bot rez works with any realm rez spell. |
| PR17b | `Caliburn_PR17b_OutsideRez` | 9 | Stranger rez, **opt-in** via `/mrez outside` (default off), GOAP-only. Fires only when the own group is fully calm: no dead mates, no emergency/critical heals, no combat, no aggro. Never chases — corpse must already be in range. Prio 0.8 (below Damage 2). |
| PR18 | `Caliburn_PR18_BattleRez` | 6 | Battle-rez, **group members only**. `ResurrectGoal` goes 25 mid-fight, but only safe casts: caster clear (no enemy within 350, not interrupted) and corpse nearly alone (≤2 live enemies within 1000). Strangers stay calm-only. |

End state: 18 goals / 17 actions / 6 sensors. Tests: **65/65**
(`GoapTests|RoleElectionTests`: 48 pre-rez + 11 PR16 + 3 PR17 + 3 PR17b;
PR18 extends existing tests).

## Verification (build machine, temp overlays)

```powershell
if (!(Test-Path CoreServer/config/serverconfig.xml)) {
    Copy-Item CoreServer/config/serverconfig.example.xml CoreServer/config/serverconfig.xml
}
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 65/65 green
```

Unit-subset parity (`--filter 'FullyQualifiedName~Unit'`) TRX-compared vs clean
base: identical 44 pre-existing spell-math failures on both sides. Note: a full
unfiltered `dotnet test` is flaky in a DB-less environment
(`SchedulerTest.Scheduler_StartIntervalTimer_TaskRunMultipleTime`
intermittently aborts the host, also on pristine base) — use the Unit subset.

## Live-test protocol (one round per folder, do not stack-test)

1. **PR16:** kill a bot → corpse stays ~60s and stays targetable → vanishes →
   replacement spawns, no duplicates. No casting expected yet.
2. **PR17:** wipe a group out of combat → healer shows
   `ResurrectGoal / CastRez` in `/mgoap status` → mate stands up (with rez
   sickness) → group resumes buffing. Then: player targets a bot corpse and
   casts a realm rez spell → instant rez, no dialog. Realm matrix: RvR =
   same realm only, PvE = cross-realm OK, grouped cross-realm OK.
3. **PR17b:** `/mrez outside` on an idle healer → rezzes a nearby dead
   stranger (`ResurrectOutsiderGoal / CastRezOutside`); never mid-fight, never
   while a mate is dead, never chases. Default (toggle off): ignores strangers.
4. **PR18:** mate dies in a fight you are winning → healer rezzes mid-combat
   (prio 25, below living emergencies). Must NOT rez into 3+ enemies at the
   corpse, while interrupted, or with an enemy on top of the caster.

## Needs eyes (cannot verify without a client)

- Corpse lying pose: a `PlayerDeath` packet with the corpse OID is broadcast;
  harmless if the client ignores it for NPC objects, but confirm whether the
  body lies down or keeps standing.
- Revive stand-up visual (same mechanism, `PlayerRevive` packet).
- **Healer spell check:** the bot needs an actual rez spell in its misc pool
  (`/mrez` tells you if it has none). If your healer classes never carry one,
  report back — the fix is a small grant step (PR12 pattern), not a redesign.

## Known limits (accepted)

- Healer never walks to a far corpse (in-range 1500 only).
- `PerfectRecoveryAbility` targets `GamePlayer` only, so PR cannot lift mimics;
  spell-line rez works.
- Cross-group double casts can race (first completed cast wins, late casts
  fizzle safe on the living target).

## Field data protocol

Per incident paste back, numbered: PR folder applied, `/mgoap status` of the
acting bot (GOAP-vs-FSM line, last goal/action), solo/grouped, zone (RvR?),
realm setup, and ~20 log lines around it. One variable at a time.
