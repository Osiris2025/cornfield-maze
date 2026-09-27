# Corn Field Maze

A third-person Unity 6 game: you play as a gingerbread cookie running through a corn maze. Several long dead ends force you to backtrack a few passages. Reach the pot of gold to win — an original celebratory jingle plays when you get there. Stay too long in the rain and the cookie slowly softens and dissolves; wrong turns risk **the Husk**, what is left of a corn plant once it is stripped (an original threat — not Sesame Street IP).

## Open the project

1. Install **Unity 6000.3.23f1** (Unity Hub is fine).
2. In Unity Hub, choose **Add** → **Add project from disk**.
3. Select this folder:

   `/Volumes/files1/projects/cornmaze/CornFieldMaze`

4. Open **Assets/Scenes/CornMaze** (created on first editor setup) and press **Play**.

If you open the project before that scene exists, press Play on any scene — the maze still builds at runtime.

## How to play

You are a stylized gingerbread cookie (brown dough, icing, gumdrop buttons). Find the pot of gold at the end of the maze.

A looping anxious underscore, chase-string stabs, hollow wind, and dry corn rustle start when you press Play (original procedural audio, not licensed tracks). Reach the pot of gold and the horror bed ducks for an original upbeat win jingle (bright brass and walking bass). After about **20 seconds** the weather turns: sky and fog go almost night-dark, wind and corn-sway harden, mostly vertical rain starts, paths grow slightly muddier over time, and lightning flashes before distant then closer thunder. When the sky darkens, look **straight up** — a faint star arrow near zenith points along the **next correct corridor** toward the gold.

About **5 seconds** after play starts, **the Husk** appears and chases along the gravel paths only. It is slow: if you stay on the solution route it cannot catch you. Dead ends that force a turnaround into it can get you eaten (game over — press **R** or Restart).

Stay on the **gravel-and-grass lanes**. You cannot walk through the corn or off the trail. **WASD / arrows follow the corridor** you are in (not free-strafe off the path). Longer rain slowly dissolves the gingerbread (icing washes off first) over minutes of play — you can still finish if you are reasonably quick.

| Key | Action |
| --- | --- |
| **W A S D** or **arrow keys** | Walk the lanes (in line with the path) |
| **Left stick** (iPhone) | Walk the lanes (same path-locked move) |
| **Mouse** | Look / orbit camera |
| **Drag right side** (iPhone) | Look / orbit camera |
| **Shift** or **RUN** (iPhone) | Run |
| **Esc** | Free or lock the cursor |
| **F11** or **Cmd+F** (Ctrl+F on Windows) | Toggle fullscreen in a **built** app |
| **R** | Restart after you win or get caught |

Wrong turns run several passages deep. When a corridor dead-ends, turn around and try another junction.

## Full screen and playing outside Unity

Yes — build a standalone Mac app. That is the real fullscreen game: it runs in its own window, without the Unity editor around it.

There are two different “full screen” ideas. **Maximize On Play** only enlarges the Game view *inside* Unity while you develop. A **standalone `.app`** is a real game you can launch from Finder.

### In the Unity Editor (while developing)

This fills the editor with the game. It is not a separate app.

1. Open **Assets/Scenes/CornMaze**.
2. Click the **Game** tab (next to Scene).
3. Enable **Maximize On Play**. Look at the top of the Game view for a toggle labeled **Maximize On Play**, or the extra-large rectangle icon.
4. Press the editor **Play** button. The Game view fills the Unity window.

Optional:

- Drag the **Game** tab out into its own window, then play.
- Use a larger **Window** layout (**Window → Layouts**).
- Click the Game view’s **Maximize** button while already in Play mode.

**Esc** only unlocks the mouse — it does not leave Play mode. **F11** / **Cmd+F** call `Screen.fullScreen`; that mainly affects a **built** app, not the editor Game view. To leave Play mode, press the editor Play button again.

### Play outside the Unity window (standalone app) — this is the real fullscreen game

Yes, you can play without Unity’s editor chrome. Build a standalone Mac player, then launch the `.app`.

**Shortcut in Unity:** menu **Corn Maze → Build Standalone macOS App** (writes `Builds/Corn Field Maze.app` and reveals it in Finder). Or **Corn Maze → Open Build Settings…** if you want the usual dialog. **Corn Maze → Apply Fullscreen Player Settings** sets Fullscreen Mode to Fullscreen Window.

**Manual steps (macOS, Unity 6):**

1. **File → Build Settings…** (or **File → Build Profiles** in Unity 6).
2. If **CornMaze** is not in the scene list, click **Add Open Scenes**. Make **CornMaze** the first scene (index 0).
3. Platform: **macOS** (switch platforms if needed).
4. **Player Settings**: set **Fullscreen Mode** to **Fullscreen Window** (this project already defaults to that).
5. Click **Build** (or **Build And Run**). Choose a folder, for example `Builds/`.
6. Launch the **`.app`** from Finder (or from the folder Unity just opened).
7. Fullscreen: **F11** or **Cmd+F**. The macOS green traffic-light button also works.
8. If a resolution dialog appears, choose **Fullscreen** vs **Windowed** there.

After launch:

- **Esc** frees the cursor; it does **not** exit fullscreen.
- Use **F11** / **Cmd+F** again, or the green button, to leave fullscreen.
- You can quit the app like any other Mac program (**Cmd+Q**).

## iPhone

iPhone audio plays with the mute switch on (Playback session); rebuild the Xcode project after this fix.

**Yes — it can run on an iPhone** by sideloading from Xcode. This is not an App Store install unless you later join the paid Apple Developer Program and ship through TestFlight or the store.

A Unity iOS build produces an **Xcode project**, not an `.ipa` by itself. That is expected.

The generated project is:

`/Volumes/files1/projects/cornmaze/CornFieldMaze/Builds/iOS/Unity-iPhone.xcodeproj`

**Xcode is required** to install the game on a phone. This Mac currently has only Command Line Tools (`xcode-select` points at `/Library/Developer/CommandLineTools`). There is no `/Applications/Xcode.app`, so `xcodebuild` cannot sign or install a device app from the command line. Install Xcode from the Mac App Store, then open that `.xcodeproj`.

### Touch controls (iPhone / iPad)

Hold the phone in **landscape**. Portrait rotation is turned off so the maze has width.

| Control | Action |
| --- | --- |
| **Left virtual stick** | Walk the gravel lanes (same path-locked movement as WASD) |
| **Drag on the right side** | Look / orbit the camera |
| **RUN** (lower right) | Sprint |
| **RESTART** (after you win or get caught) | Play the maze again |

Keyboard and mouse still work in the Mac build. On a phone the mouse-lock / fullscreen hint text is hidden.

### Install on your iPhone (free Apple ID)

A free Apple ID can sideload the app for about **7 days**. After that, open Xcode and Run again to re-sign.

1. Connect the iPhone with USB.
2. On the iPhone (iOS 16+): **Settings → Privacy & Security → Developer Mode** — turn it on, then restart if asked.
3. Install **Xcode** if it is missing. Open the generated project: `Builds/iOS/Unity-iPhone.xcodeproj`.
4. In the Xcode toolbar, select your **iPhone** as the run destination (not a simulator if you want it on the real phone). The iOS Simulator is optional for a quick look on the Mac.
5. Select the **Unity-iPhone** target → **Signing & Capabilities** → check **Automatically manage signing** → **Team** → your Apple ID.
6. If Xcode complains that the bundle ID is taken, change **Bundle Identifier** to something unique (the project default is `com.arl480.cornfieldmaze`).
7. Press the **Play / Run** button in Xcode.
8. On the phone, if iOS says the developer is not trusted: **Settings → General → VPN & Device Management** (wording varies) → trust the Apple ID, then open **Corn Field Maze**.

### App Store / TestFlight

Shipping through TestFlight or the App Store is a separate path: paid **Apple Developer Program**, App Store Connect, and a Release build. Do not expect this Xcode sideload to appear on the App Store by itself.

### Rebuild the Xcode project from Unity

In the Unity editor: **Corn Maze → Build iOS Xcode Project** (writes `Builds/iOS`). Or from a terminal:

```bash
"/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "/Volumes/files1/projects/cornmaze/CornFieldMaze" \
  -executeMethod CornMaze.EditorTools.CornMazeSetup.BuildIosPlayer \
  -logFile "/Volumes/files1/projects/cornmaze/CornFieldMaze/Builds/ios-build.log"
```

You also need **iOS Build Support** installed for Unity 6000.3.23f1 (Unity Hub → Installs → Add modules). The standalone macOS app (`Corn Maze → Build Standalone macOS App`) is unchanged.
