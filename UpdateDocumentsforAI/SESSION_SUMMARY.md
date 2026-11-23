# Session summary: Mimic tactical GOAP stack (copy into new session)

## Base facts
- Working repo: `C:\VScodeAI\OpenDC\Caliburn` = `Fusseli/CaliburnWorkingBranch` @ pre-PR state (verified vs PR base: pre-PR `MimicSensor` Console.WriteLine version, `ReGoapPlanner maxIterations=1000`). **Never modified — all work shipped as stacked drop-in folders.**
- Starting point: GitHub PR #1 "Wire mimic tactical GOAP with FSM fallback" (`joshlit:codex/wire-mimic-goap @ 5c9a96e`, 18 files, +861/−1416).
- `DaocConvtr\` is READ-ONLY reference (never touch). `PROJECT_LOG.md` (OpenDC Unity workstream) deliberately untouched by all Caliburn work.
- Server context: `allow_old_archery = false` (new archery live). Test PC is separate (another OpenCode instance); handovers carry context there.

## Deliverables (copy in order, each over previous; folders mirror repo structure, deltas only)
| # | Folder | Files | Content |
|---|---|---|---|
| PR1 | `Caliburn_PR1_GOAP` | 18 | The GitHub PR head verbatim. Hybrid GOAP (GOAP-first inside FSM states, FSM fallback same tick), 6 goals/5 actions/6 sensors, `/mgoap status\|on\|off`. |
| PR2 | `Caliburn_PR2_Interrupt` | 6 | Interrupt (prio 9): enemy-casting sensor, melee-pressure-first action. No README (accepted gap, covered from PR3). |
| PR3 | `Caliburn_PR3_Assist` | 7 | Assist train (4): non-assist open on MainAssist target; restores dead `CheckAssist()` intent. Casters included, healers/assist excluded. |
| PR4 | `Caliburn_PR4_Peel` | 7 | Tank peel (`min(6+3×loose,30)`, ×2 healer attacked); CC'd enemies never peeled. Main tank only. |
| PR5 | `Caliburn_PR5_Guard` | 9 | Auto-guard, neediest scoring (healer 6/caster 4/other 2, +3 attacked, +2 HP<50, hysteresis); prio 7 combat / 3 support. Manual `/mguard`+whisper lock wins. Protect/intercept stay manual. Fixed a real build bug found by verification (`ECSGameAbilityEffect` has no `Source` → cast to `GuardECSGameEffect`). |
| PR6 | `Caliburn_PR6_Survival` | 8 | Kite (6, casters only, `Flee(600)`) + Quickcast (8, mirrors FSM). Arrival hygiene for stale flee flags. |
| PR6b | `Caliburn_PR6b_Roles` | 6 | Class-aware role election (NOT GOAP): tank/CC/assist/puller to capable bots, auto-flag healers 1-per-4, lazy on membership change. `/mrole` locks, `/mheal` touched-flag, players never assigned/demoted, ties keep incumbents. Snapshot-pure scoring (real NPCs need server singletons — lesson). |
| PR7 | `Caliburn_PR7_Caller` | 7 | Assist calls focus (casting +50, weakest-first, +5 stickiness, never CC'd), prio 4. |
| PR8 | `Caliburn_PR8_Debuff` | 7 | Debuff-before-damage (3 / 0.9 off-train) via `Spell.IsDebuff` + existing cast path. |
| PR9 | `Caliburn_PR9_Positional` | 7 | Flank walk (3.5), mirrors FSM flank loop; target-switch `ResetFlanking()`. |
| PR10 | `Caliburn_PR10_Docs` | 1 | Closing README ("Not registered by design"). |
| PR11 | `Caliburn_PR11_ArcheryFix` | 1 | `GetModifiedSpecLevel`: `Archery`→best bow spec (`ba51dbb` pattern). Fixes ~0 archer damage under new flag. |
| PR12 | `Caliburn_PR12_OldArchery` | 2 | Old-flag bow abilities: grant Crit/Rapid/SureShot/Penetrating by bow spec in `ArcherBrain` + per-tick shot-type trigger (Critical opener, RapidFire vs casters); flag watcher; `SelectGoapAttackTarget` now `virtual`. Volley/Trueshot excluded (RA purchases). |
| PR13 | `Caliburn_PR13_ArrowType` | 8 | New-flag damage-type choice: Blunt/Thrusting/Slashing buffs (7397-9, Archery line L1) applied by direct cast; pure `BestArrowType()` scoring (+5 stickiness); prio 2.5; mimic-only engine touch in `AttackDamageType` NPC branch. |
| PR14 | `Caliburn_PR14_KiteFix` | 4 | Kite rework (live issue): solo = FSM-parity long flight (2000−dist) + 700 hold ring; grouped = fall back to tank (peel/guard take over), tanks never kite; pure `ShouldKeepKiting()` + `FindKiteMate()`. |
| PR15 | `Caliburn_PR15_Diagnostics` | 2 | `Fallback()` clears LastGoal/Action; `LastSenseMs/LastPlanMs` in `/mgoap`. Zero behavior change. |
| Docs | `HANDOVER.md`, `HANDOVER_Archery.md`, `HANDOVER_Issues.md` (workspace root) | — | Stack reference, archery arc, live-issue triage. `HANDOVER.md` already copied to test PC. |

End state: **16 goals / 15 actions / 6 sensors**; priorities: Emergency 100 > Cure 20 > Control ≤40 > Interrupt 9 > Quickcast 8 > Guard 7/3 > Peel ≤60 > Assist/Caller 4 > Debuff 3 > Kite 6 > Flank 3.5 > ArrowType 2.5 > Damage 2 > Buffs.

## Verification method (repeat for every future patch)
Temp overlay (base + stack) in `%TEMP%\opencode\caliburn_*check`, `serverconfig.example.xml`→`serverconfig.xml`, `dotnet build` (0 errors) + `GoapTests|RoleElectionTests` filter (**48/48**: 42 GOAP incl. new + 6 election). Full suite: 364 tests, 292 pass; 44 spell-math failures + DB-integration host crash are **pre-existing, TRX-proven identical on clean base** — never chase them. Two self-inflicted process notes: always set `workdir` to the temp dir (twice ran tests in the real repo by mistake — harmless, failed pre-compile), and stage each patch from the *latest* copy of every file (PR15 nearly regressed ArrowTypeGoal by staging the agent from PR9).

## Doctrines (keep them)
One tick of work → re-sense; fail closed to FSM; every behavior = sensor flag + goal + action + test reusing existing brain mechanics; manual commands always win via locks; never register a goal without all three + test; small stacked folders, verify each in overlay before delivery.

## Open threads (live, in priority order)
1. **Freeze** (bots stand post-fight, status roaming): buff-loop theory DISPROVEN by observation (bots buff once, move on). Suspects: ROAMING delay path / CAMP stale destination / stuck flee-flank flags. Needs frozen-state `/mgoap` (trustworthy post-PR15) + logs + `/mgoap off` unfreeze test. Do not fix blind.
2. **Long ticks** (26–45 ms/think all brain types; 93 ms effect ticks incl. regular mobs): needs PR15 sense/plan numbers from a lagging bot before optimizing.
3. **Archery live-measure pending**: PR11 numbers → PR12 old-flag specials → PR13 type choice vs armor (plate→Crush etc.). No Heat/Cold chooser buffs exist in DB; NPCs ignore ammo; `ClassSpecs` bow lines are correct, do not touch.
4. Retired by design (in PR10 README): pulls (FSM-owned), Survive/BackupHeal/MaintainSpeed/BreakEnemyMezz. Renegades inherit everything (plain `MimicNPC`, `Realm=Renegade`).

## User's stated next work (undescribed yet — ask first)
- More GitHub repos to pull in (built on theirs, newer state).
- 2–3 "challenging things" to implement (no details yet — get descriptions + acceptance criteria before scoping).

## Rez stack (PR16–PR18, built 2026-09-10, user-accepted plan)

| # | Folder | Files | Content |
|---|---|---|---|
| PR16 | `Caliburn_PR16_RezCorpse` | 8 | Corpse foundation, no casting. Non-duel deaths linger `Active`+`!IsAlive` 60s (`MIMIC_CORPSE_WINDOW_SECONDS`) instead of instant `Delete`: rewards (DropLoot/faction/OnNPCKilled) preserved via `GameLivingProcessDeath`, spawner `Remove` + `Group.RemoveMember` deferred to `ReleaseCorpse()` expiry, `m_deathtype` classified (`ResolveMimicDeathType`, pure), `SendPlayerDied(GameLiving)` overload so clients render the corpse (needs eyes). Duels keep legacy instant delete. `CheckGroupHealth` skips dead (corpses are rez jobs, not heal jobs). 11 pure tests. |
| PR17 | `Caliburn_PR17_GroupRez` | 17 | Group rez, out-of-combat. `FindRezTarget` (nearest dead mate, same region, `IsSameRealm` pre-check, engine re-validates) shared by sensor+GOAP+FSM. `ResurrectGoal` prio 15 (0 in combat) / `CastRez` → `ExecuteGoapRez` / legacy defensive branches fixed (range+realm+`AlreadyCastingRez`+`/mrez`). `OnMimicRevived` completes engine rez (timer, `m_isDead`, regen, sickness, brain→WAKING_UP, revive broadcast); hooked into `ResurrectSpellHandler` NPC branch (mimic-only). `SendPlayerRevive(GameLiving)` overloads. `/mrez` toggle (needs rez spell). Player→bot rez works via existing CORPSE path (NPC instant branch = auto-accept). 3 tests (counts 17/16). |
| PR17b | `Caliburn_PR17b_OutsideRez` | 9 | Stranger rez, opt-in `/mrez outside` (default off), GOAP-only. `FindOutsiderRezTarget` (dead players via `GetPlayersInRadius` + foreign mimic corpses via `GetNPCsInRadius`, never chases) fires only when fully calm (no dead mates, no emergency/critical, no combat/aggro). `ResurrectOutsiderGoal` prio 0.8 (below Damage 2, above off-train 0.6) / `CastRezOutside` (own `outsiderRezzed` effect — shared key confused the planner, fixed). 3 tests (counts 18/17). |
| PR18 | `Caliburn_PR18_BattleRez` | 6 | Battle-rez, group-only. `REZ_COMBAT_SAFE` (caster clear: no enemy ≤350, not interrupted; corpse: ≤2 live enemies ≤1000; never chases) lifts the combat gate: `ResurrectGoal` 25 in safe fights, 15 calm, 0 otherwise. `ExecuteGoapRez` yields while interrupted. Outsider stays calm-only. Tests extended (no new count). |

End state: **18 goals / 17 actions / 6 sensors** (test counts in `EveryRegisteredGoalHasAnActionWithMatchingEffects`: 18/17).
Verification per patch: overlay build 0 errors + `GoapTests|RoleElectionTests` green (59 → 62 → 65 → 65), Unit-subset (`FullyQualifiedName~Unit`) TRX-diff vs base15 overlay **empty** (44 pre-existing spell-math failures both sides). Full-suite `dotnet test` without filter is FLAKY in this env: `SchedulerTest.Scheduler_StartIntervalTimer_TaskRunMultipleTime` intermittently aborts the host (blame-proven, happens on pristine base too) — use the Unit subset for parity, not the full run. Final clean-room re-apply PR1–PR18: 0 errors, 65/65.
Process notes added: (1) `Select-String "error"` false-positives on German "vererbt" warnings — grep `error CS|Fehler` instead; (2) new `IPacketLib` members must be added to ALL implementers: `PacketLib168`, `DummyPacketLib`, `ConsolePacketLib` (CoreServer), `TestPacketLib` (Tests) — compiler finds them one by one; (3) two GOAP actions need distinct effect keys or the planner picks the first registered.
Known limits: healer never walks to a far corpse (in-range only); PerfectRecoveryAbility targets `GamePlayer` only so PR rez can't lift mimics (spell-line rez works); corpse lying pose + revive stand-up need eyes (packets are OID-based, harmless if ignored); healer bots need a rez spell in MiscSpells (verify live, grant only if missing).

## RA stack (PR19a–PR19b, built 2026-09-11; artifacts skipped by user decision)

| # | Folder | Files | Content |
|---|---|---|---|
| PR19a | `Caliburn_PR19a_RAbuyer` | 6 | RA auto-buyer. `MimicRABuyer`: curated priority table (universal → Healer/Caster/Archer/Melee, stealth-gated MoStealth) spends the rank budget (`Level>19 ? max(1,RL) : RL`, player formula) on class-list RAs; RR5 excluded (free via spec at RL40). One mimic-only engine branch in `LiveRealmAbilitiesSpecialization` surfaces granted RAs as live abilities → all passives apply via Activate/AbilityBonus, zero brain code. Throttled brain check (spawn-immediate, then on rank change / 10s). `/mra` shows rank, spent/budget, RA list. 9 pure tests. Atlas: only Live KeyNames + `Avoid Pain`←AugCon-3 encoded; unmatched Atlas KeyNames skip safely. |
| PR19b | `Caliburn_PR19b_Purge` | 8 | Self-purge. `PurgeAbility.RemoveNegativeEffects` generalized GamePlayer→GameLiving (was hard `false` for NPCs; chat/stealth feedback player-guarded, player behavior identical). `PurgeGoal` (30) / `UsePurge` when mezzed/stunned/rooted with Purge ready; yields while interrupted. 2 tests (counts 19/18). |

End state: **19 goals / 18 actions / 6 sensors**. Verification: PR19a isolated stage (base+PR1–PR18+PR19a) 0 errors, 74/74; final clean-room re-apply PR1–PR19b 0 errors, 76/76; Unit-subset TRX-diff vs base empty (44 pre-existing both sides).
Process notes added: (4) `MimicBrain.Body` is typed `GameNPC` — use `MimicBody` for mimic members; (5) when reconstructing an intermediate stage file, verify brace balance + test-method counts vs the overlay version (a fuzzy edit silently misplaced the class close once).
Open threads for later: ML auto-grant (Phase 2), CL (Phase 3, after player-side work), artifacts (Phase 4, needs curated per-class list).

## Savebots stack (PR20a–PR20c, built 2026-09-11)

| # | Folder | Files | Content |
|---|---|---|---|
| PR20a | `Caliburn_PR20a_SaveStore` | 5 | Storage. New `MimicSave` table (account/realm/slot 0–9, name, class, level, XP, serialized specs+RAs, rank, ML/Champion columns reserved, appearance, spec variant). `MimicSaveManager`: snapshot/restore, pair-CSV codec, slot/name/region-gate helpers, gear move via shared `Inventory` table under stable key. `/msave [newname]` (tame: snapshot + gear moves + wild bot despawns), `/mbots`, `/mdelete`. 7 pure tests. |
| PR20b | `Caliburn_PR20b_Summon` | 4 | Summoning. `/mcall` (RvR own-realm-only gate, max 7 active/account, already-out hurries over). `Instantiate` rebuilds (specs+RAs restored, stored gear or fresh spawn gear, group join when allowed, single-instance registry). `/mdismiss` writes back + despawns. Logout hook (`GamePlayer.Quit` 1 line) + shutdown via `[GameServerStoppedEvent]` (no engine edit). 1 test. |
| PR20c | `Caliburn_PR20c_DeathGuards` | 4 | Death + guards. `ReleaseCorpse` returns saved bots to storage (no replacement); owned bots drop no loot (RP credit kept — no farm). Self-healing active registry; `/mdelete` refuses live bots; `/msave` on own active bot refreshes. 1 test. |

No GOAP changes (19 goals / 18 actions stay). Verification: PR20a isolated stage 0 errors, 83/83; PR20b isolated stage 0 errors, 84/84; final clean-room re-apply PR1–PR20c 0 errors, 85/85; Unit-subset TRX-diff vs base empty (44 pre-existing both sides).
Process notes added: (6) never include file-closing braces in edit anchors — the tool can anchor them at the match while the real file continues (bit twice, caught by brace-count + test-name-count checks); (7) `MimicBrain.Body` is `GameNPC` — already noted in (4), same class of mistake as `GamePlayer` vs `GameLiving` in spell code.
Open threads for later: ML auto-grant, CL, artifacts, Atlas RA table extension, RA spec-gate live confirmation.

## PR21 help window + priv split (built 2026-09-11)

`/mcreate`, `/mspawner`, `/mgroup`, `/mbattle` moved `Player` → `GM` priv
(world-shaping; server enforces). New `/mimic` (Player) opens a standard text
window: player section always, GM section only for GMs. Content from pure
`MimicHelpBuilder.BuildText(isGM)` (3 files, 1 test). Chain re-apply
PR1–PR21: 0 errors, 86/86; Unit-subset parity empty.
