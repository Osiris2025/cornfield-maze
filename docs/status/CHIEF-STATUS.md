# Corn Field Maze — Chief Status

**Updated:** 2026-09-17 (scaffold — crew not yet dispatched)
**Milestone:** M0 — the gingerbread cookie. **Nothing else is in flight.**

## Crew

| Name | Handle | Role | State |
|---|---|---|---|
| Harrow | @corn-chief | Chief | armed |
| Dough | @corn-art | the gingerbread cookie, materials | idle — M0 is theirs |
| Furrow | @corn-gameplay | maze, walker, path rules, beast, gold, HUD, touch | idle |
| Squall | @corn-world | corn, sky, storm, mud | idle |
| Rattle | @corn-audio | mood bed, cues, jingle, iOS audio | idle |
| Lantern | @corn-qa | verify.sh, Mac + iPhone builds, perf, evidence | idle |

## Why this crew exists

Todd: *"we are very unhappy with the gingerbread man."* The player is ~40 Unity primitives — a blocky robot wearing brown. Replacing it is **M0 and a HARD GATE**: no other milestone closes until Todd has seen a frame of the new cookie from the running Mac build.

## Last commits

- baseline commit — the whole project as found (no prior history).

## In flight

- nothing. Scaffold only.

## Blockers

- none yet. Known standing item: `appleDeveloperTeamID` is empty in `ProjectSettings.asset` and iOS automatic signing is off — a device build will need Todd's team id pinned where Unity re-emits it. That becomes real at T21, not before.

## CAPTURES FOR TODD

- none yet. First one is T7 (the cookie gate).
