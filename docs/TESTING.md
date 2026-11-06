# Testing — Corn Field Maze

## Automated (Unity Test Framework)
Window -> General -> Test Runner. Run EditMode first, then PlayMode.

Headless:
```
"/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -runTests -projectPath "$PWD" \
  -testPlatform EditMode -testResults /tmp/corn-editmode.xml -quit
```
The result XML is the evidence. No XML means the run did not complete — that is a
failure, not a pass.

## PlayMode hang rule (learned the hard way)
The PlayMode suite can hang on a `Temp/ProcessJobs` problem. Bounded procedure:
1. Wait ~10 minutes for a result XML.
2. If none: kill the run, clear stale `Temp`, retry once.
3. If it hangs again: record it and use EditMode as the gate. Do not loop forever
   and do not report a hung run as passing.

## Visual verification (the file is the spec)
For any referenced visual, put Todd's still and the game's frame side by side at
matched size. If they do not read as the same artwork, it is not done — a written
description of the intent is not evidence.

## Play checks
1. Play the maze to the pot of gold; the win jingle fires and the horror bed ducks.
2. After ~20 s the storm turns; the star arrow near zenith points along the next
   correct corridor.
3. The Husk appears ~5 s in and chases on gravel only; a dead-end turnaround can
   get you eaten (game over, R restarts).
4. Rain dissolves the cookie over minutes; you can still finish.
5. Path locking holds: you cannot walk through corn or off the trail.
