# CornFieldMaze War Room

## Queue
- [x] (a) Procedural level generation — done (MazeGenerator.cs, M43 shipped 57ce613)
- [x] (b) Coin/reward system — done (M44 shipped c3b9134, verify green)
- [x] (c) Upgrade shop — done (M45 shipped d81254d, verify green)
- [x] (d) Enemy types with AI — done (M46 shipped 88ee0e1, verify green)
- [ ] (e) Boss fight encounter
- [ ] (f) Game Center leaderboard

## Log
- 2026-09-28 00:00 — Crew chief first watch. HEAD 57ce613 (M43 level progression). Clean tree. Procedural generation verified complete. Dispatching (b) coin/reward system.
- 2026-09-28 10:09 — M44 coin/reward system landed. CoinPickup.cs (floating gold discs, auto-collect 0.9m, off-route planting 6+L per level), CoinWallet.cs (PlayerPrefs-persisted singleton), HUD coin counter. Build verified green (c3b9134).
- 2026-09-28 10:15 — M45 upgrade shop landed. UpgradeState.cs (5 upgrades × 3 tiers, PlayerPrefs persistence), ShopPanel.cs (full-screen overlay UI, 5 upgrade rows with BUY buttons), ShopFlow.cs (show/hide gate). Integrated into ShowWin for non-last levels. Build verified green (d81254d).
- 2026-09-28 10:22 — M46 enemy types landed. StaleLoaf.cs (stationary blocker, HP 102, 4-coin drop), SugarAnt.cs (5-ant clusters, 1 HP each, wall pathfinding), IcingWisp.cs (ranged, glob fire every 2.2s, HP 68), EnemySpawner.cs (level-gated spawning). Build verified green (88ee0e1).