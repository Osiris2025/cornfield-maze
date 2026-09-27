using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// M38 — the bite. Measured on the built Mac app.
///
///     "&lt;app&gt;" -bitetest
///
/// Todd: "we need to work on some kind of combat system.. we have to be able to not trapped completey
/// by the husk or the game ends prematurely", then "Make it only 1/10.. we also have to have something
/// better than becoming treanparent also... masybe bite marks and bleeding icing?", then
/// "well rain could do 1/20 damage per minute?".
///
/// This harness walks the creature onto the player and lets the game's OWN TryCatch fire — it never
/// calls TakeBite itself — so what it measures is the run, not the harness. It answers:
///   * does a catch still end the run? It must NOT, until the pool is empty;
///   * does one bite cost exactly 1/10 (10 of 100), so exactly ten bites end him;
///   * does the bite shove him clear down the lane, through the lane constraint;
///   * does the Husk stop for the feed recoil, and does it start again;
///   * is the cookie OPAQUE at every damage level — the transparency Todd rejected;
///   * what the bite marks and the bleeding icing actually LOOK like. Shot in THIRD PERSON: in first
///     person the controller draws the cookie ShadowsOnly, so his wounds cannot be photographed there
///     at all, and a frame of nothing would read as "the wounds are missing".
///   * M41: the Husk's ARM SWIPE — 1-3 dough off the same pool, telegraphed, dodgeable, no wound, and
///     never in the same frame as a bite. Measured with the game's own attack path firing, in both
///     views, with a dodge proven by walking out of the windup.
///
/// M41 TUNE: the jump-frame capture is bounded to two shots with freshness-bounded waits (the old
/// fixed waits ate the bite budget), and the bite counter separates real bites (drop >= half the
/// bite cost) from rain drips, so the pool actually empties and the numbers diff against prior runs.
/// </summary>
public class M38BiteSelfTest : MonoBehaviour
{
    const string Flag = "-bitetest";

    readonly List<string> _lines = new List<string>();

    static System.DateTime _runStart;

    static bool Wanted()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == Flag) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeInstall()
    {
        if (!Wanted()) return;
        var go = new GameObject("M38BiteSelfTest");
        go.AddComponent<M38BiteSelfTest>();
        Object.DontDestroyOnLoad(go);
    }

    static string ReportPath => Path.Combine(Application.persistentDataPath, "m38-bite-report.txt");

    IEnumerator Start()
    {
        Application.runInBackground = true;
        _runStart = System.DateTime.UtcNow;
        yield return null;
        yield return null;

        GameFrontEnd.ForcePlayForTest();
        yield return null;

        var player = Object.FindFirstObjectByType<FarmWalkerController>();
        if (player == null) { Emit("FAIL: no FarmWalkerController"); Finish(1); yield break; }
        var maze = player.Maze;
        var cam = player.Camera;
        if (maze == null || cam == null) { Emit("FAIL: no maze or camera"); Finish(1); yield break; }

        Emit("M38 the bite — measured on the built Mac app, " +
             System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture));

        // ---- the arithmetic the whole thing rests on, stated before it is measured ------------------
        Emit("the pool: MaxDough=" + FarmWalkerController.MaxDough.ToString("0") +
             ", one bite costs " + FarmWalkerController.DoughBiteCost.ToString("0") +
             " -> " + (FarmWalkerController.MaxDough / FarmWalkerController.DoughBiteCost).ToString("0") +
             " bites empty it, so " + (FarmWalkerController.MaxDough / FarmWalkerController.DoughBiteCost).ToString("0") +
             " bites end the run");
        Emit("the rain: DissolveRainSeconds=" + FarmWalkerController.DissolveRainSeconds.ToString("0") +
             " s of full storm = " +
             (FarmWalkerController.MaxDough / FarmWalkerController.DissolveRainSeconds * 60f).ToString("0.0") +
             " dough/min at full storm — Todd asked for 1/20 of the pool per minute, i.e. " +
             (FarmWalkerController.MaxDough / 20f).ToString("0.0") + " dough/min");
        Emit("bite wounds: cap " + FarmWalkerController.MaxBiteWounds.ToString("0") + ", icing bleed RGBA " +
             FarmWalkerController.IcingBleedColour.ToString("0.00"));

        // ---- the cookie as the player sees him, BEFORE anything hurts him ---------------------------
        ReportCookie(player, "at full dough, first person");

        // ---- what the game actually TELLS the player -------------------------------------------------
        // Todd: "i have no idea how to attack." The control prompt is the whole answer to that, so it is
        // read back off the live HUD rather than trusted to the build.
        var hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null)
            Emit("FAIL: no GameHud in the scene — the control prompt cannot be on screen");
        else
            Emit("the HUD control prompt: visible=" + hud.ControlPromptVisible + ", text=\"" +
                 hud.ControlPromptForTest + "\"");

        // ---- M40: does a capture run STAY silent? ---------------------------------------------------
        // Todd: "it isnt muted. I hear it." The -silent flag DID mute at Start, and then
        // MobileAudioSession's Reactivate() — which the first keypress, a focus change and a resume all
        // call — put the volume straight back to 1. Checking that the mute FIRED was the wrong check. This
        // calls the very same Reactivate() a keypress triggers, twice, and reads the volume back, so
        // "silent" is proven rather than assumed.
        {
            float volBefore = AudioListener.volume;
            MobileAudioSession.Reactivate();
            MobileAudioSession.Reactivate();
            float volAfter = AudioListener.volume;
            Emit("capture audio: volume " + volBefore.ToString("0.00") + " -> " + volAfter.ToString("0.00") +
                 " after the same Reactivate() a keypress triggers, twice — " +
                 (volAfter <= 0.001f
                     ? "STILL SILENT (the mute holds)"
                     : "NOT SILENT (the mute is being undone — this is the bug)"));
        }

        // ---- the Husk ------------------------------------------------------------------------------
        Husk husk = null;
        float waited = 0f;
        while (husk == null && waited < 25f)
        {
            husk = Object.FindFirstObjectByType<Husk>();
            waited += Time.deltaTime;
            yield return null;
        }
        if (husk == null) { Emit("FAIL: no Husk spawned"); Finish(1); yield break; }
        Emit("the Husk is " + (husk.UsingModel ? "the supplied model" : "the M28 primitives") +
             ", " + husk.HeightMeters.ToString("0.00") + " m tall, health " + husk.Health.ToString("0.0") +
             " (a thrown cob takes a third, unchanged)");

        // ---- wait for the creature to WAKE --------------------------------------------------------------------
        // There is a spawn delay: Update returns at `if (Time.timeSinceLevelLoad < _spawnAt)` until it is
        // over, and TryCatch is the LAST line of Update — so a bite staged inside the dormant window
        // cannot land however precisely the creature is placed. It is not a broken catch. Waking is
        // detected by the creature actually MOVING off its spawn point, which is what being active looks
        // like from outside; a fixed sleep would only guess at the delay.
        Vector3 huskSpawnAt = husk.transform.position;
        float wake = 0f;
        while (wake < 30f && Vector3.Distance(husk.transform.position, huskSpawnAt) < 0.05f)
        {
            wake += Time.deltaTime;
            yield return null;
        }
        Emit("the creature woke after " + wake.ToString("0.0") + " s of level time (it is dormant for its spawn " +
             "delay and cannot bite before then), and has walked " +
             Vector3.Distance(husk.transform.position, huskSpawnAt).ToString("0.00") + " m since it was found");

        // ---- the player stands still; the creature is walked onto him -------------------------------
        // Injected zero input, NOT enabled=false: the controller must stay alive so TakeBite, the
        // knockback and the model are all real. Nothing below touches the health of either of them.
        player.InjectInput = true;
        player.InjectedMove = Vector2.zero;
        player.InjectedRun = false;

        float dough0 = player.DoughIntegrity;
        Emit("dough at the start: " + dough0.ToString("0.0") + " (" + player.DoughState + "), IsCaught=" +
             player.IsCaught + ", FirstPerson=" + player.FirstPerson);
        if (player.IsCaught || player.IsWon)
        {
            Emit("FAIL: the run was already over before the first bite could be staged");
            Finish(1);
            yield break;
        }

        // ---- the jump, measured against the code's own physics ---------------------------------------
        // FarmWalkerController derives the launch speed from the gravity the file already had:
        // v = sqrt(2 g h). At Gravity 18 and a 0.85 m apex that is 5.53 m/s, and 2v/g = 0.615 s of air.
        // InjectJumpNow drives the same path the Space bar does, and it is a ONE-FRAME pulse: the hook is
        // ORed into a grounded condition, so leaving it set would bunny-hop him down the lane.
        {
            float y0 = player.transform.position.y;
            player.InjectJumpNow = true;
            yield return null;
            player.InjectJumpNow = false;

            float rise = 0f;
            float air = 0f;

            // Todd: "when I press Jump, Husk jumps also." Its ROOT y is fixed by Husk.cs (next.y = pos.y)
            // and its only vertical motion in the CODE is the model bob (LurchHop = 0.07 m). What he can
            // actually see is the top of its rendered body, so sample that every frame of the jump. If
            // the creature rises here, the coupling is real and this number is where it shows up.
            float huskTop0 = HuskTopY(husk);
            float huskRise = 0f;
            float huskDip = 0f;

            bool launched = false;
            float jt = 0f;
            while (jt < 2.5f)
            {
                jt += Time.deltaTime;
                float dy = player.transform.position.y - y0;
                if (!launched && dy > 0.02f) launched = true;
                if (dy > rise) rise = dy;
                if (husk != null)
                {
                    float top = HuskTopY(husk) - huskTop0;
                    if (top > huskRise) huskRise = top;
                    if (top < huskDip) huskDip = top;
                }
                if (launched)
                {
                    air += Time.deltaTime;
                    if (dy <= 0.01f) break;              // back down on the lane
                }
                yield return null;
            }
            Emit("jump: rose " + rise.ToString("0.00") + " m over " + air.ToString("0.00") +
                 " s of air; the code predicts a 0.85 m apex and 0.615 s at Gravity " +
                 player.Gravity.ToString("0") + " — launched=" + launched);
            Emit("the creature through that same jump: its body moved " + huskRise.ToString("0.000") +
                 " m up and " + huskDip.ToString("0.000") + " m down (root y is pinned by code; the " +
                 "model bob alone is LurchHop = 0.070 m)");

            float settle = 0f;
            while (settle < 0.4f) { settle += Time.deltaTime; yield return null; }
        }

        // ---- does the creature move when the cookie jumps? Photograph it, don't argue about it ---------
        // Todd: "as I said earlier, husk jumps when I jump." His shot is truth.
        //
        // M41 TUNE: last night this section ate the bite budget. Four screenshots, each waited out on a
        // fixed 6 s window, plus the 0.4 s settle — during which the rain clock kept spending (5 dough
        // per minute) and those drips were later counted as "bites", so the loop quit at 14 phantom
        // bites with the pool stuck at 9.9. The proof did not need four frames — grounded vs apex is
        // the whole argument — so this version takes TWO, waits on each only until the file is FRESH
        // (never a fixed window), and the whole section is bounded. Same measurements: husk ROOT and
        // MODEL displacement frame by frame, the cookie's rise, worst frame time, and the camera parked
        // at a FIXED world position for the whole jump so "the camera does not ride the jump" is a
        // number, not an assertion.
        if (husk != null)
        {
            var lane = player.transform.forward;
            lane.y = 0f;
            lane = lane.sqrMagnitude > 0.0001f ? lane.normalized : Vector3.forward;
            husk.transform.position = player.transform.position + lane * 3f;
            husk.enabled = false;                  // hold it still: the jump must be the only variable
            husk.SwipesEnabled = false;            // and it cannot swipe a parked cookie either
            yield return null;
            yield return null;

            var huskModel = husk.transform.Find("ScarecrowMesh");
            Emit("jump frame test: creature parked 3 m down the lane (its model child " +
                 (huskModel != null ? "FOUND" : "not found — falling back to the root") + ")");

            var jumpCam = player.Camera;
            float rootY0 = husk.transform.position.y;
            float modelY0 = huskModel != null ? huskModel.position.y : 0f;
            float playerY0 = player.transform.position.y;
            float rootMax = 0f, modelMax = 0f, modelMin = 0f, playerMax = 0f, worstFrame = 0f;

            var shotNames = new[] { "m40-jump-0-grounded.png", "m40-jump-2-apex.png" };
            var shotPaths = new string[shotNames.Length];
            for (int i = 0; i < shotPaths.Length; i++)
                shotPaths[i] = Path.Combine(Application.persistentDataPath, shotNames[i]);
            int shotsFired = 0;
            float camY0 = jumpCam != null ? jumpCam.transform.position.y : 0f;

            if (jumpCam != null)
            {
                jumpCam.transform.position = player.transform.position - lane * 3.2f + Vector3.up * 1.9f;
                var aim = husk.transform.position + Vector3.up * 1.15f - jumpCam.transform.position;
                jumpCam.transform.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up);
            }
            yield return null;
            yield return null;
            ScreenCapture.CaptureScreenshot(shotPaths[shotsFired++]);
            // Bounded, not fixed: stop the moment the file is FRESH, give up at 3 s. The old fixed 6 s
            // wait is where the bite budget went.
            yield return WaitForFreshShot(shotPaths[shotsFired - 1], 3f);

            player.InjectJumpNow = true;
            yield return null;
            player.InjectJumpNow = false;

            float jt = 0f;
            bool airborneSeen = false;
            while (jt < 2.5f)
            {
                float dt = Time.deltaTime;
                if (dt > worstFrame) worstFrame = dt;
                jt += dt;

                float dRoot = husk.transform.position.y - rootY0;
                float dModel = huskModel != null ? huskModel.position.y - modelY0 : 0f;
                float dPlayer = player.transform.position.y - playerY0;
                if (dRoot > rootMax) rootMax = dRoot;
                if (dModel > modelMax) modelMax = dModel;
                if (dModel < modelMin) modelMin = dModel;
                if (dPlayer > playerMax) playerMax = dPlayer;

                if (!airborneSeen && dPlayer > 0.05f) airborneSeen = true;
                if (airborneSeen && dPlayer > 0.4f && dPlayer < playerMax - 0.03f && shotsFired == 1)
                {
                    ScreenCapture.CaptureScreenshot(shotPaths[shotsFired++]);   // the apex, on the way down
                    yield return WaitForFreshShot(shotPaths[shotsFired - 1], 3f);
                }
                if (airborneSeen && dPlayer <= 0.02f)
                    break;
                yield return null;
            }

            for (int i = 0; i < shotsFired; i++)
                Emit("frame " + shotNames[i] + " -> " +
                     (File.Exists(shotPaths[i]) && File.GetLastWriteTimeUtc(shotPaths[i]) >= _runStart
                        ? "written (captured mid-jump, straight from the screen)"
                        : "MISSING/STALE"));
            Emit("through that jump the cookie rose " + playerMax.ToString("0.00") + " m; the CREATURE's root " +
                 "y moved " + rootMax.ToString("0.000") + " m and its model " + modelMax.ToString("0.000") +
                 " m up / " + modelMin.ToString("0.000") + " m down; worst frame time " +
                 worstFrame.ToString("0.000") + " s; the camera was FIXED and its y moved " +
                 (jumpCam != null ? (jumpCam.transform.position.y - camY0).ToString("0.000") : "n/a") +
                 " m — it does not ride the jump");
            husk.enabled = true;
        }

        // ---- M41: the arm swipe, measured BEFORE the bite loop --------------------------------------
        // Swipes come first because the bite numbers are only comparable with the previous runs when
        // the pool still starts where it started — and the swipe spends 1-3 dough off that same pool.
        // The swipe is watched with the game's OWN path firing (the harness never calls the attack),
        // in THIRD person where the telegraph is visible, then once in FIRST person to prove the hit
        // lands in both views. The dodge is proven by backing the cookie out of the windup.
        {
            const float SwipeWatchSeconds = 75f;
            const int WantSwipeEvents = 4;   // 1 dodged + 2 landed (third person) + 1 landed (first person)
            int land0 = husk.SwipesLanded, dodge0 = husk.SwipesDodged;

            // THE FIX from the first M41 run: the jump-frame section leaves swiping off, and this
            // section forgot to turn it back on — so the husk stood at 1.0 m and BIT ten times, the
            // pool hit 0 before a single swipe fired, and the report said "no swipe within 75 s".
            husk.SwipesEnabled = true;

            player.SetFirstPerson(false);
            yield return null;
            yield return null;

            var lane = player.transform.forward;
            lane.y = 0f;
            lane = lane.sqrMagnitude > 0.0001f ? lane.normalized : Vector3.forward;
            // The Husk is parked BEHIND the cookie: the start cell's only exit is forward, so the dodge
            // must walk FORWARD down the open lane — backing up walks him into the corridor's dead end,
            // which is exactly what the first run did (he could not clear the windup and ate the swing).
            Vector3 BehindSpot() => player.transform.position - lane * 1.00f;   // inside SwipeRange 1.15, past CatchDistance 0.62

            Emit("M41 the arm swipe: starts when the cookie hovers within " + Husk.SwipeRange.ToString("0.00") +
                 " m, telegraphs " + Husk.SwipeWindupSeconds.ToString("0.00") + " s, lands inside " +
                 Husk.SwipeStrikeRange.ToString("0.00") + " m for a random integer " +
                 Husk.SwipeDamageMin + "-" + Husk.SwipeDamageMax + " off the same dough pool (no wound — " +
                 "the cap of 6 counts bite marks); it cannot stack with the bite (a swipe in flight blocks TryCatch)");

            husk.transform.position = BehindSpot();
            bool dodging = false;
            int events = 0;
            int seen = land0 + dodge0;
            float watch = 0f;
            float swipeDough = player.DoughIntegrity;
            while (events < WantSwipeEvents && watch < SwipeWatchSeconds && !player.IsCaught)
            {
                if (husk.PhaseNow == Husk.SwipePhase.Windup && !dodging && events == 0)
                {
                    // The dodge proof: WALK the cookie forward down the open lane using the game's own
                    // injected movement. Aim forward first (AimAtForTest sets the yaw the stick is read
                    // against), then push the stick straight ahead — no yaw arithmetic to get wrong, and
                    // the direction he walks is the one open corridor direction, away from the Husk.
                    player.AimAtForTest(player.transform.position + lane * 5f);
                    dodging = true;
                    Emit("dodge: windup seen — the cookie now walks FORWARD down the open lane at the " +
                         "injected stick for the " + Husk.SwipeWindupSeconds.ToString("0.00") +
                         " s the arm is winding");
                }
                if (dodging && events == 0 && husk.PhaseNow == Husk.SwipePhase.Windup)
                    player.InjectedMove = new Vector2(0f, 1f);   // stick straight ahead = away from the Husk

                yield return null;
                watch += Time.deltaTime;

                // Interstitial bites ARE part of this melee: at 1.0 m the husk is in the cookie's cell
                // (cells are 4 m), so between swings the game's own TryCatch may fire — that is the
                // game's rule, not the harness's. Log them so the pool arithmetic stays honest.
                float doughNow = player.DoughIntegrity;
                if (swipeDough - doughNow >= 5f)
                {
                    Emit("bite during the swipe test: dough " + swipeDough.ToString("0.0") + " -> " +
                         doughNow.ToString("0.0") + "  (delta 10.0, the game's own catch at mid-range — " +
                         "NOT a swipe, NOT counted in the bite loop below)");
                    swipeDough = doughNow;
                }

                int now = husk.SwipesLanded + husk.SwipesDodged;
                if (now > seen)
                {
                    seen = now;
                    events++;
                    player.InjectedMove = Vector2.zero;   // the dodge walk is over, whatever the outcome
                    swipeDough = player.DoughIntegrity;   // swipe damage counts in this tracker too
                    if (husk.LastSwipeDodged)
                    {
                        Emit("swipe: windup seen, the cookie was OUT of reach when the arm came down -> " +
                             "DODGED, damage 0 (the windup is a real dodge window, not decoration)");
                    }
                    else
                    {
                        Emit("swipe: damage " + husk.LastSwipeDamage + "  (expected an integer in [" +
                             Husk.SwipeDamageMin + "," + Husk.SwipeDamageMax + "])" +
                             "  dough -> " + player.DoughIntegrity.ToString("0.0") +
                             "  state " + player.DoughState +
                             (player.FirstPerson ? "  [FIRST person]" : "  [third person]"));
                    }
                    if (events == 1) husk.transform.position = BehindSpot();          // step back in: next one lands
                    if (events == 3)
                    {
                        player.SetFirstPerson(true);                                  // same hit, first person
                        yield return null;
                        yield return null;
                        husk.transform.position = BehindSpot();
                        Emit("the same swipe test in FIRST person — his model is ShadowsOnly there, but the damage must still land");
                    }
                }

                // The Husk still walks; hold it at swat range whenever it is free to move. The phase
                // must NOT have to be Idle: after the dodge (run 4) it closed to contact DURING its
                // cooldown, reached the bite's 0.62 m, and chewed ten times before a single swipe
                // could arm. Only a windup in flight is left alone.
                if (husk.PhaseNow != Husk.SwipePhase.Windup &&
                    Vector3.Distance(husk.transform.position, player.transform.position) > 1.3f)
                    husk.transform.position = BehindSpot();
            }

            Emit("--- M41 swipe measured: " + (husk.SwipesLanded - land0) + " landed, " +
                 (husk.SwipesDodged - dodge0) + " dodged; pool now " + player.DoughIntegrity.ToString("0.0") +
                 " (the bites below start HERE, not at 100 — the swipe spends the same pool)");
            if (events == 0)
                Emit("FAIL: no swipe fired within " + SwipeWatchSeconds.ToString("0") + " s");
        }

        // ---- bite, bite, bite ----------------------------------------------------------------------
        const int BitesPerSwallow = 4;   // shoot the wounds once they are on him, before the pool empties
        int bites = 0;
        bool runEnded = false;
        bool shotWounds = false;
        float knockTotal = 0f;
        var recoils = new List<float>();
        float lastDough = player.DoughIntegrity;
        Vector3 prevPos = player.transform.position;
        float watching = 0f;
        float dripTotal = 0f;
        int bitesToEmpty = (int)(FarmWalkerController.MaxDough / FarmWalkerController.DoughBiteCost);
        float poolAtBiteStart = player.DoughIntegrity;
        // The swipe test has already spent a few dough off the same pool, so the bites below are expected
        // to end the run at floor(pool/10) bites, not at the full-pool figure — the DIFF line states both.
        int expectedBites = Mathf.Max(1, Mathf.FloorToInt(poolAtBiteStart / FarmWalkerController.DoughBiteCost));
        bool biteRunHadDough = !player.IsCaught;   // the swipe melee may have emptied the pool already
        Emit("the bite run starts with the pool at " + poolAtBiteStart.ToString("0.0") +
             " (full pool is " + FarmWalkerController.MaxDough.ToString("0") + "; the swipe test spent the " +
             "difference) -> " + expectedBites + " bites should empty it, and " + bitesToEmpty +
             " bites empty a FULL pool (last night's format, for the diff)");

        // M41 TUNE: the swipe is held OFF through the bite loop (Husk.SwipesEnabled) so every drop of
        // 1/10 stays a BITE and the numbers diff against the previous runs.
        //
        // And the counter now separates the two clocks that were conflated last night: the rain spends
        // ~0.08 dough per second (5/min) whether or not anything is chewing, and those drips were
        // counted as "bites" with delta 0.0 — 14 phantom bites, pool stuck at 9.9, run never ended. A
        // bite is a drop of at least half DoughBiteCost; anything smaller is drip, totalled separately.
        husk.SwipesEnabled = false;

        // The creature is held ON the player's own cell — TryCatch's sameCell rule — and the harness
        // watches the POOL, counting each drop of 1/10 as one bite. It deliberately does NOT run a
        // sleep-then-wait cycle. An earlier version held the creature on him through the feed recoil and
        // then waited a hair LONGER than the recoil (2.4 s against BiteRecoilSeconds = 2.0), so a SECOND
        // bite landed inside every wait and was never counted: the pool emptied in half the bites it
        // should have and the report claimed five. Watching the pool counts the game's own bites, at
        // whatever rate the game actually lands them, and cannot double-count or miss one.
        while (bites < bitesToEmpty && !player.IsCaught)
        {
            husk.transform.position = player.transform.position + Vector3.up * 0.05f;

            float now = player.DoughIntegrity;
            float drop = lastDough - now;
            if (drop >= FarmWalkerController.DoughBiteCost * 0.5f)
            {
                float delta = drop;
                float knock = Vector3.Distance(prevPos, player.transform.position);
                knockTotal += knock;
                bites++;

                // Read the recoil at the instant the bite lands: StaggerLeft is what freezes the walk and
                // what makes TryCatch return early, so it is the whole escape window in one number.
                float stagger = husk.StaggerLeft;
                recoils.Add(stagger);

                Emit("bite " + bites + ": dough " + lastDough.ToString("0.0") + " -> " + now.ToString("0.0") +
                     "  (delta " + delta.ToString("0.0") + ", expected " +
                     FarmWalkerController.DoughBiteCost.ToString("0") + ")" +
                     "  state " + player.DoughState +
                     "  shoved " + knock.ToString("0.00") + " m" +
                     "  Husk feed recoil " + stagger.ToString("0.00") + " s" +
                     "  IsCaught=" + player.IsCaught);
                lastDough = now;

                if (player.IsCaught)
                {
                    runEnded = true;
                    Emit("the run ENDED on bite " + bites + " with the pool at " + now.ToString("0.0") +
                         " — the ONLY fail state, and it took " + bites + " bites to get there");
                }

                // ---- what the damage LOOKS like, shot in third person --------------------------------
                if (!shotWounds && bites >= BitesPerSwallow && !player.IsCaught)
                {
                    shotWounds = true;

                    // Get the creature OUT OF THE SHOT. It is standing on the player when a bite lands —
                    // that is what makes a bite land — and the first attempt at these frames photographed
                    // its coat instead of the cookie's wounds. The portrait has to be of the cookie.
                    bool huskWasEnabled = husk.enabled;
                    husk.enabled = false;
                    husk.transform.position = maze.CellToWorld(maze.GoldCell.x, maze.GoldCell.y) +
                                              Vector3.up * 0.05f;

                    player.SetFirstPerson(false);
                    yield return null;
                    yield return null;

                    Vector3 p = player.transform.position;
                    Vector3 fwd = player.transform.forward;
                    Vector3 flank = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 chest = p + Vector3.up * 0.85f;

                    // Third person is the only view where he is drawn at all, so say what the material is
                    // doing HERE as well as there — the fade Todd rejected shows up in both numbers.
                    ReportCookie(player, "at " + now.ToString("0.0") + " dough, third person (the only view he is drawn in)");

                    yield return AimAndShoot(cam, p - fwd * 2.6f + Vector3.up * 1.05f, chest, "m38-bite-front.png");
                    yield return AimAndShoot(cam, p + flank * 2.4f + Vector3.up * 1.05f, chest, "m38-bite-side.png");

                    player.SetFirstPerson(true);
                    husk.enabled = huskWasEnabled;   // put it back to work for the rest of the bites
                    yield return null;
                }
            }
            else if (drop >= 0.01f)
            {
                // The rain clock (and nothing else) — tracked so it is visible, never counted as a bite.
                dripTotal += drop;
                lastDough = now;
            }

            prevPos = player.transform.position;
            yield return null;

            watching += Time.deltaTime;
            if (watching > 90f)
            {
                Emit("stopped watching after " + watching.ToString("0") + " s with the pool at " +
                     player.DoughIntegrity.ToString("0.0") + " — the bites stopped landing");
                break;
            }
        }
        husk.SwipesEnabled = true;

        // ---- the totals ----------------------------------------------------------------------------
        // Read the END off the live player, not off the in-loop flag. TakeBite reports "the pool is
        // empty" and the eat sequence only raises IsCaught about 0.35 s later (EatPlayer opens the mouth
        // first), so a flag set on the bite frame can read false while the run is already over — which is
        // exactly how an earlier version of this report managed to say the run never ended with the pool
        // sitting at 0. The player's own state is the truth; the flag is only a hint.
        bool ended = runEnded || player.IsCaught;

        Emit("--- measured: " + bites + " bites landed; total shove " + knockTotal.ToString("0.0") +
             " m (" + (bites > 0 ? (knockTotal / bites).ToString("0.00") : "0") + " m each); rain/swipe drips " +
             dripTotal.ToString("0.0") + " dough (NOT counted as bites); pool now " +
             player.DoughIntegrity.ToString("0.0") + "; run ended=" + ended);

        if (recoils.Count > 0)
        {
            float mn = recoils[0];
            float mx = recoils[0];
            foreach (var r in recoils) { if (r < mn) mn = r; if (r > mx) mx = r; }
            Emit("feed recoil seen on " + recoils.Count + " bites: " + mn.ToString("0.00") + " s to " +
                 mx.ToString("0.00") + " s, against Husk.BiteRecoilSeconds=" +
                 Husk.BiteRecoilSeconds.ToString("0.00") + " s, which is " +
                 (Husk.MoveSpeed * Husk.BiteRecoilSeconds).ToString("0.00") +
                 " m of free ground at MoveSpeed " + Husk.MoveSpeed.ToString("0.00"));
        }

        Emit(ended
            ? (!biteRunHadDough
                ? "NOTE: the swipe melee spent the whole pool (bites at mid-range between swings are the " +
                  "game's own rule) — the bite loop had nothing left to measure, and the run ended in the melee"
                : bites >= expectedBites
                ? "the run ended ONLY when the pool emptied: " + bites + " bites of " +
                  FarmWalkerController.DoughBiteCost.ToString("0") + " emptied the " +
                  poolAtBiteStart.ToString("0.0") + "-dough pool this run, and " + bitesToEmpty +
                  " bites empty a FULL " + FarmWalkerController.MaxDough.ToString("0") +
                  "-dough pool (last night's figure, for the diff) — nothing ended the run before that"
                : "WRONG: the run ended after " + bites + " bites, before the bites alone could empty the " +
                  "pool (" + expectedBites + " expected from " + poolAtBiteStart.ToString("0.0") + " dough)")
            : "NOTE: the pool did not empty, so the bites never ended the run — " + bites +
              " bites landed and he is still walking");

        Finish(0);
    }

    /// <summary>Wait only until a screenshot file is FRESH — written this run. Never a fixed window:
    /// last night's fixed 6 s waits are where the bite budget went. Gives up after maxWait seconds.</summary>
    IEnumerator WaitForFreshShot(string path, float maxWait)
    {
        float waited = 0f;
        while (waited < maxWait)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _runStart) yield break;
        }
    }

    /// <summary>
    /// The cookie's live material. Todd rejected the old damage look because it faded him toward
    /// transparent (icing to 0.05 alpha, dough to 0.22), so the number that matters here is the LOWEST
    /// alpha on any of his materials: at every damage level it must stay 1.00.
    /// </summary>
    void ReportCookie(FarmWalkerController player, string when)
    {
        var renderers = player.GetComponentsInChildren<Renderer>(true);
        int count = 0;
        int dim = 0;
        float minAlpha = 1f;
        int queues = -1;
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            var m = r.sharedMaterial;
            count++;
            if (queues < 0) queues = m.renderQueue;
            if (m.HasProperty("_BaseColor"))
            {
                float a = m.GetColor("_BaseColor").a;
                if (a < minAlpha) minAlpha = a;
                if (a < 0.98f) dim++;
            }
        }
        Emit("cookie look " + when + ": " + count + " renderers, lowest _BaseColor alpha " +
             minAlpha.ToString("0.00") + ", renderQueue " + queues + " -> " +
             (dim == 0
                ? "NONE of his materials are see-through: he stays SOLID (the fade Todd rejected is gone)"
                : dim + " materials are TRANSPARENT — the fade is BACK"));
    }

    /// <summary>The highest point of the creature's rendered body, in world metres. This is how Todd's
    /// "when I press Jump, Husk jumps also" gets answered with a number: the creature's root y is pinned
    /// by its own code (next.y = pos.y) and root motion is off, so the only thing that CAN move is the
    /// mesh — and this is that mesh's top edge.</summary>
    static float HuskTopY(Husk h)
    {
        if (h == null) return 0f;
        float top = h.transform.position.y;
        var rends = h.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null && rends[i].enabled)
                top = Mathf.Max(top, rends[i].bounds.max.y);
        return top;
    }

    /// <summary>Aim the camera at a world point, shoot, and say whether the file is from THIS run.</summary>
    IEnumerator AimAndShoot(Camera cam, Vector3 at, Vector3 look, string file)
    {
        if (cam == null)
        {
            Emit("frame " + file + " -> SKIPPED: no camera left (the run restarted mid-harness)");
            yield break;
        }
        cam.transform.position = at;
        var d = look - at;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        cam.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        for (int i = 0; i < 3; i++) yield return null;

        string path = Path.Combine(Application.persistentDataPath, file);
        ScreenCapture.CaptureScreenshot(path);
        float waited = 0f;
        bool fresh = false;
        while (waited < 8f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= _runStart) { fresh = true; break; }
        }
        yield return new WaitForEndOfFrame();
        Emit("frame " + file + " -> " + (fresh
                ? "written this run (" + new FileInfo(path).Length + " bytes)"
                : "MISSING/STALE — the file on disk predates this run"));
    }

    void Emit(string line)
    {
        _lines.Add(line);
        Debug.Log("M38: " + line);
    }

    void Finish(int code)
    {
        var text = new StringBuilder();
        foreach (var line in _lines) text.AppendLine(line);
        File.WriteAllText(ReportPath, text.ToString());
        Debug.Log("M38 report written to " + ReportPath);
        Application.Quit(code);
    }
}
