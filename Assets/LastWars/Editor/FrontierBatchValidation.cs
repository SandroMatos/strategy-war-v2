using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastWars.Client.Editor
{
    // Explicit integration probe; never runs when simply opening the project.
    [InitializeOnLoad]
    public static class FrontierBatchValidation
    {
        const string Active = "FrontierProbe.Active";
        static double started, next;
        static int phase;
        static bool runtimeError, collectionVerified;
        static string output;
        static FrontierBatchValidation()
        {
            if (SessionState.GetBool(Active, false))
            {
                EditorApplication.update += Tick;
                Application.logMessageReceived += ObserveLog;
                EditorApplication.playModeStateChanged += ObservePlayState;
            }
        }
        [MenuItem("LastWars/Validar fluxo em Play Mode (conta QA)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode antes de iniciar o teste.");
            FrontierEditor.ValidateContracts();
            string folder = "Library/FrontierProbe";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-frontierOutput") folder = args[i + 1];
            Directory.CreateDirectory(folder); SessionState.SetString("FrontierProbe.Output", folder);
            var config = Resources.Load<ClientConfiguration>("FrontierConfiguration");
            string key = "LastWars.Client.Session." + config.apiBaseUrl.TrimEnd('/');
            SessionState.SetString("FrontierProbe.Key", key);
            SessionState.SetBool("FrontierProbe.HadSession", PlayerPrefs.HasKey(key));
            SessionState.SetString("FrontierProbe.SavedSession", PlayerPrefs.GetString(key, ""));
            PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene("Assets/LastWars/Scenes/Frontier.unity");
            EditorApplication.EnterPlaymode();
        }
        static object Field(FrontierApp app, string name) => typeof(FrontierApp).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
        static void Invoke(FrontierApp app, string name, params object[] args) => typeof(FrontierApp).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, args);
        static void Tick()
        {
            if (!SessionState.GetBool(Active, false)) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) return;
            output = SessionState.GetString("FrontierProbe.Output", "Library/FrontierProbe");
            if (started == 0) { started = EditorApplication.timeSinceStartup; next = started + 3; }
            if (EditorApplication.timeSinceStartup - started > 180) { Finish(false, "Timeout waiting for frontend/API."); return; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                var app = UnityEngine.Object.FindObjectOfType<FrontierApp>();
                if (app == null) return;
                if (phase == 0)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "login.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase++; return;
                }
                if (phase == 1)
                {
                    Invoke(app, "Create", "QAUnity" + DateTime.UtcNow.ToString("MMddHHmmss"));
                    phase++; return;
                }
                var state = Field(app, "state") as BaseDto;
                if (state == null || (bool)Field(app, "busy")) return;
                if (phase == 2)
                {
                    File.WriteAllText(Path.Combine(output, "base-fixture.json"), JsonUtility.ToJson(state, true));
                    next = EditorApplication.timeSinceStartup + 3; phase++; return;
                }
                if (phase == 3)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "base.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 30; return;
                }
                if (phase == 30)
                {
                    var production = Field(app, "production") as BuildingProductionView;
                    var mine = state.buildings.First(b => b.type == "iron_mine");
                    if (!production.HasSnapshot) { Finish(false, "Missing production snapshot."); return; }
                    if (!production.Ready(mine.id))
                    {
                        File.WriteAllText(Path.Combine(output, "collection-note.txt"), "SKIP: new QA account has fewer than 100 units. The real 100-unit collection is tested with a seeded QA fixture; visual bursts have a separate offline fixture.");
                        phase = 33; return;
                    }
                    SessionState.SetString("FrontierProbe.BeforeIron", state.resources.iron.ToString());
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "production-ready.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 31; return;
                }
                if (phase == 31)
                {
                    var mine = state.buildings.First(b => b.type == "iron_mine");
                    Invoke(app, "Select", mine.id);
                    if ((Field(app, "ui") as FrontierView).HasDialog) { Finish(false, "Collection opened a dialog."); return; }
                    next = EditorApplication.timeSinceStartup + 2; phase = 32; return;
                }
                if (phase == 32)
                {
                    if (state.resources.iron <= long.Parse(SessionState.GetString("FrontierProbe.BeforeIron", "0")))
                    { Finish(false, "Collection did not credit the server wallet."); return; }
                    collectionVerified = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "production-countdown.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 33; return;
                }
                if (phase == 33)
                {
                    var building = state.buildings.FirstOrDefault(b => b.type == "iron_mine") ?? state.buildings.First();
                    SessionState.SetString("FrontierProbe.Building", building.id);
                    Invoke(app, "OpenDetails", building.id); next = EditorApplication.timeSinceStartup + 2; phase = 4; return;
                }
                if (phase == 4)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "building.png"));
                    var allText = string.Join("\n", UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>().Select(t => t.text));
                    File.WriteAllText(Path.Combine(output, "visible-ui.txt"), allText);
                    if (!allText.Contains("PRÓXIMO NÍVEL")) { Finish(false, "Upgrade contract not displayed."); return; }
                    var upgrade = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == "INICIAR EVOLUÇÃO");
                    if (upgrade == null || !upgrade.interactable) { Finish(false, "Affordable upgrade button disabled."); return; }
                    next = EditorApplication.timeSinceStartup + 2; phase++; return;
                }
                if (phase == 5)
                {
                    Invoke(app, "Act", SessionState.GetString("FrontierProbe.Building", ""), "upgrade");
                    next = EditorApplication.timeSinceStartup + 2; phase++; return;
                }
                if (phase == 6)
                {
                    var building = state.buildings.First(b => b.id == SessionState.GetString("FrontierProbe.Building", ""));
                    if (building.status != "building" && building.status != "ready_to_upgrade") { Finish(false, "Upgrade did not change server state: " + building.status); return; }
                    File.WriteAllText(Path.Combine(output, "base-after-upgrade.json"), JsonUtility.ToJson(state, true));
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "construction.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase++; return;
                }
                if (phase == 7)
                {
                    var building = state.buildings.First(b => b.id == SessionState.GetString("FrontierProbe.Building", ""));
                    app.BeginPlacement(building.id);
                    var placement = Field(app, "placement") as GridPlacementController;
                    var blockers = Field(app, "placementObstacles") as ObstacleDto[];
                    if (!placement.IsActive) { Finish(false, "Placement failed to open."); return; }
                    var other = state.buildings.First(b => b.id != building.id);
                    placement.SetCandidate(other.grid_x, other.grid_y);
                    if (placement.CanConfirm) { Finish(false, "Collision accepted by preview."); return; }
                    next = EditorApplication.timeSinceStartup + 2; phase = 70; return;
                }
                if (phase == 70)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "placement-invalid.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 8; return;
                }
                if (phase == 8)
                {
                    var placement = Field(app, "placement") as GridPlacementController;
                    placement.Cancel();
                    var building = state.buildings.First(b => b.id == SessionState.GetString("FrontierProbe.Building", ""));
                    app.BeginPlacement(building.id);
                    var blockers = Field(app, "placementObstacles") as ObstacleDto[];
                    bool found = false;
                    for (int x = state.grid_bounds.min_x; x < state.grid_bounds.max_x && !found; x++)
                        for (int y = state.grid_bounds.min_y; y < state.grid_bounds.max_y && !found; y++)
                            if ((x != building.grid_x || y != building.grid_y) && GridPlacementRules.Validate(state, blockers, building, x, y) == null)
                            {
                                placement.SetCandidate(x, y); found = true;
                                SessionState.SetInt("FrontierProbe.MoveX", x); SessionState.SetInt("FrontierProbe.MoveY", y);
                            }
                    if (!found || !placement.CanConfirm) { Finish(false, "No valid candidate."); return; }
                    next = EditorApplication.timeSinceStartup + 2; phase = 9; return;
                }
                if (phase == 9)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "placement-valid.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 10; return;
                }
                if (phase == 10)
                {
                    (Field(app, "placement") as GridPlacementController).Confirm();
                    next = EditorApplication.timeSinceStartup + 2; phase = 11; return;
                }
                if (phase == 11)
                {
                    var building = state.buildings.First(b => b.id == SessionState.GetString("FrontierProbe.Building", ""));
                    if (building.grid_x != SessionState.GetInt("FrontierProbe.MoveX", -1) || building.grid_y != SessionState.GetInt("FrontierProbe.MoveY", -1))
                    { Finish(false, "Server position not reconciled."); return; }
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "placement-saved.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 12; return;
                }
                if (phase == 12)
                {
                    Invoke(app, "OpenDetails", state.buildings.First(b => b.type == "food_farm" && b.status == "completed").id);
                    next = EditorApplication.timeSinceStartup + 2; phase = 13; return;
                }
                if (phase == 13)
                {
                    var button = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == "INICIAR EVOLUÇÃO");
                    if (button == null || button.interactable) { Finish(false, "Unavailable builder did not disable upgrade."); return; }
                    var icons = UnityEngine.Object.FindObjectsOfType<ResourceIcon>();
                    if (icons.Length < 4 || icons.Any(i => i.GetComponent<CanvasRenderer>() == null)) { Finish(false, "Resource icon renderer missing."); return; }
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "upgrade-disabled.png"));
                    next = EditorApplication.timeSinceStartup + 2; phase = 14; return;
                }
                if (phase == 14) Finish(!runtimeError, "Unity Play Mode: production UI, enabled/disabled upgrade buttons, icon renderers, upgrade and grid placement passed. " + (collectionVerified ? "Direct collection and wallet credit passed. " : "Collection skipped: fresh QA account has fewer than 100 units. ") + "QA account only; prior local session restored.");
            }
            catch (Exception error) { Finish(false, error.GetBaseException().Message); }
        }
        static void ObserveLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = true;
        }
        static void ObservePlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Active, false))
                Finish(false, "Teste interrompido; sessão original restaurada.");
        }
        static void Finish(bool success, string message)
        {
            var key = SessionState.GetString("FrontierProbe.Key", "");
            if (SessionState.GetBool("FrontierProbe.HadSession", false)) PlayerPrefs.SetString(key, SessionState.GetString("FrontierProbe.SavedSession", ""));
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); SessionState.EraseString("FrontierProbe.SavedSession");
            SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
            Application.logMessageReceived -= ObserveLog; EditorApplication.playModeStateChanged -= ObservePlayState;
            File.WriteAllText(Path.Combine(output, "result.txt"), (success ? "PASS\n" : "FAIL\n") + message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else { EditorApplication.isPlaying = false; Debug.Log((success ? "Frontier PASS: " : "Frontier FAIL: ") + message); }
        }
    }
}
