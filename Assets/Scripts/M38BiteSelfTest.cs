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

        // The creature is held ON the player's own cell — TryCatch's sameCell rule — and the harness
        // watches the POOL, counting each drop of 1/10 as one bite. It deliberately does NOT run a
        // sleep-then-wait cycle. An earlier version held the creature on him through the feed recoil and
        // then waited a hair LONGER than the recoil (2.4 s against BiteRecoilSeconds = 2.0), so a SECOND
        // bite landed inside every wait and was never counted: the pool emptied in half the bites it
        // should have and the report claimed five. Watching the pool counts the game's own bites, at
        // whatever rate the game actually lands them, and cannot double-count or miss one.
        while (bites < 14 && !player.IsCaught)
        {
            husk.transform.position = player.transform.position + Vector3.up * 0.05f;

            float now = player.DoughIntegrity;
            if (lastDough - now >= 0.01f)
            {
                float delta = lastDough - now;
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
                    ReportCookie(player, "at " + now.ToString("0.0") +
                                 " dough, third person (the only view he is drawn in)");

                    yield return AimAndShoot(cam, p - fwd * 2.6f + Vector3.up * 1.05f, chest, "m38-bite-front.png");
                    yield return AimAndShoot(cam, p + flank * 2.4f + Vector3.up * 1.05f, chest, "m38-bite-side.png");

                    player.SetFirstPerson(true);
                    husk.enabled = huskWasEnabled;   // put it back to work for the rest of the bites
                    yield return null;
                }
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

        // ---- the totals ----------------------------------------------------------------------------
        // Read the END off the live player, not off the in-loop flag. TakeBite reports "the pool is
        // empty" and the eat sequence only raises IsCaught about 0.35 s later (EatPlayer opens the mouth
        // first), so a flag set on the bite frame can read false while the run is already over — which is
        // exactly how an earlier version of this report managed to say the run never ended with the pool
        // sitting at 0. The player's own state is the truth; the flag is only a hint.
        bool ended = runEnded || player.IsCaught;
        int bitesToEmpty = (int)(FarmWalkerController.MaxDough / FarmWalkerController.DoughBiteCost);

        Emit("--- measured: " + bites + " bites landed; total shove " + knockTotal.ToString("0.0") +
             " m (" + (bites > 0 ? (knockTotal / bites).ToString("0.00") : "0") + " m each); pool now " +
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
            ? (bites >= bitesToEmpty
                ? "the run ended ONLY when the pool emptied: " + bites + " bites of " +
                  FarmWalkerController.DoughBiteCost.ToString("0") + " off " +
                  FarmWalkerController.MaxDough.ToString("0") + " is " + bitesToEmpty +
                  " bites to die, and nothing ended the run before that"
                : "WRONG: the run ended after " + bites + " bites, before the bites alone could empty the " +
                  "pool (" + bitesToEmpty + " expected)")
            : "NOTE: the pool did not empty, so the bites never ended the run — " + bites +
              " bites landed and he is still walking");

        Finish(0);
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
