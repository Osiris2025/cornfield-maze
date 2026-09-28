# CornFieldMaze War Room

## Queue
- [x] (a) Procedural level generation — done (MazeGenerator.cs, M43 shipped 57ce613)
- [x] (b) Coin/reward system — done (M44 shipped 6c91e08, verify green)
- [ ] (c) Upgrade shop
- [ ] (d) Enemy types with AI
- [ ] (e) Boss fight encounter
- [ ] (f) Game Center leaderboard

## Log
- 2026-09-28 00:00 — Crew chief first watch. HEAD 57ce613 (M43 level progression). Clean tree. Procedural generation verified complete. Dispatching (b) coin/reward system.
- 2026-09-28 10:09 — M44 coin/reward system landed. CoinPickup.cs (floating gold discs, auto-collect 0.9m, off-route planting 6+L per level), CoinWallet.cs (PlayerPrefs-persisted singleton), HUD coin counter. Build verified green (6c91e08).