# AGENTS.md — rules for every agent working in this repo

(Hermes, Claude Code, Codex, OpenCode: this file is loaded as project context.)

## Rule #1: one Unity writer at a time

This is a Unity tree. Two Unity processes — the editor or `-batchmode` writers —
on the **same project** corrupt `Library/`: duplicate junk files, broken imports,
black frames. Before launching Unity you must know that no other writer holds
this project, and **refuse (never race)** if one does.

- The rule is **per project tree**: one machine may run editors on *different*
  Unity projects side by side. Sharing or copying `Library/` between machines is
  never allowed — it is a derived cache (like `Temp/`, `Obj/`, `UserSettings/`)
  and must stay out of git, tarballs, and every other share.
- `scripts/build-mac.sh` and `scripts/build-ios.sh` enforce this with a
  pre-flight guard (exit 3 = lane busy). A lane that needs Unity while it is
  held must wait for the lane or refuse — never start a second writer.
- If you add a script that launches Unity, add the same pre-flight check
  (port `scripts/build-mac.sh`'s guard, or copy Spirus's
  `scripts/unity-lane-guard.sh`).
