using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CustomLevelCreatorController : MonoBehaviour
{
    public const string DefaultHostSceneName = "Level 4";
    static bool launchRequested;
    static int requestedCampaignLevel = 4;

    CustomLevelDocument document;
    readonly Stack<string> undoStack = new Stack<string>();
    readonly Stack<string> redoStack = new Stack<string>();

    bool isEditing = true;
    int currentCampaignLevel = 4;
    string activeTool = "ground";
    string statusMessage = "Ready";

    GameObject editorCanvasObj;
    GameObject spawnMarkerObj;
    TextMeshProUGUI headerText;
    TextMeshProUGUI toolText;
    GameSession session;

    public CustomLevelDocument Document => document;
    public bool IsEditing => isEditing;
    public int CurrentCampaignLevel => currentCampaignLevel;
    public string ActiveTool => activeTool;
    public string StatusMessage => statusMessage;

    public static void OpenCreatorFromMenu()
    {
        int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(StartMenu.UnlockedLevelKey, 1), 1, 6);
        OpenCreatorForCampaignLevel(unlocked);
    }

    public static void OpenCreatorForCampaignLevel(int campaignLevelNumber)
    {
        int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(StartMenu.UnlockedLevelKey, 1), 1, 6);
        requestedCampaignLevel = Mathf.Clamp(campaignLevelNumber, 1, unlocked);
        launchRequested = true;
        PlayerPrefs.SetInt(CustomLevelSceneAdapter.ActiveCustomStageKey, 0);
        CustomLevelSceneAdapter.SetSuppressAutoOverride(true);
        Time.timeScale = 1f;
        PauseMenu.GameIsPaused = false;
        ScenePersist sp = FindObjectOfType<ScenePersist>();
        if (sp) sp.ResetScenePersist();
        SceneManager.LoadScene($"Level {requestedCampaignLevel}");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoaded()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (launchRequested && GameSession.IsGameplayScene(scene.name))
            {
                launchRequested = false;
                var host = new GameObject("CustomLevelCreatorController");
                host.AddComponent<CustomLevelCreatorController>();
            }
        };
    }

    void Start()
    {
        currentCampaignLevel = requestedCampaignLevel;
        session = FindObjectOfType<GameSession>();
        if (session)
        {
            session.PlaytestDeathOccurred += OnPlaytestDeath;
            session.PlaytestCompleted += OnPlaytestCompleted;
        }

        ScenePersist sp = FindObjectOfType<ScenePersist>();
        if (sp) sp.ResetScenePersist();

        if (CustomLevelSceneAdapter.HasCampaignOverride(currentCampaignLevel))
        {
            document = CustomLevelSceneAdapter.LoadCampaignOverride(currentCampaignLevel);
            statusMessage = $"Loaded saved remix override for Level {currentCampaignLevel}.";
        }
        else
        {
            document = CustomLevelSceneAdapter.CaptureCurrentScene(currentCampaignLevel);
            statusMessage = $"Captured real Level {currentCampaignLevel} layout ({document.tiles.Count} tiles, {document.objects.Count} objects).";
        }

        BuildToolbarUI();
        EnterEditMode(statusMessage);
    }

    void OnDestroy()
    {
        CustomLevelSceneAdapter.SetSuppressAutoOverride(false);
        if (session)
        {
            session.PlaytestDeathOccurred -= OnPlaytestDeath;
            session.PlaytestCompleted -= OnPlaytestCompleted;
            if (session.IsPlaytestMode) session.ExitPlaytestMode();
        }
    }

    public void LoadCampaignLevelForRemix(int levelNumber)
    {
        OpenCreatorForCampaignLevel(levelNumber);
    }

    public void SelectTool(string toolId)
    {
        activeTool = toolId;
        statusMessage = $"Selected tool: {toolId}";
        RefreshToolbarText();
    }

    public bool ApplyToolAtCell(int x, int y, string toolId = null)
    {
        if (!isEditing) return false;
        string chosen = toolId ?? activeTool;
        if (!document.InBounds(x, y)) return false;

        string snapshot = document.ToJson();
        bool changed = false;

        if (chosen == "erase") changed = document.ClearCell(x, y);
        else if (chosen == "spawn") changed = document.SetSpawn(x, y);
        else if (chosen == "exit") changed = document.SetExit(x, y);
        else if (CustomLevelDocument.IsAllowedTileId(chosen)) changed = document.SetTile(x, y, chosen);
        else if (CustomLevelDocument.IsAllowedObjectId(chosen))
        {
            if (CustomLevelDocument.RequiresGroundSupport(chosen))
            {
                if (!document.TryResolveGroundedCell(x, y, chosen, out int groundedY, out float supportTopY))
                {
                    statusMessage = $"Cannot place {FormatToolLabel(chosen)}: needs solid ground support below ({x},{y})!";
                    RefreshToolbarText();
                    return false;
                }
                y = groundedY;
                float exactY = (chosen == "gate_t1" || chosen == "gate_t2") ? supportTopY + 1f : supportTopY + 0.5f;
                changed = document.SetObject(x, y, chosen, x, exactY);
            }
            else
            {
                changed = document.SetObject(x, y, chosen, x, y, chosen == "platform" ? 6f : 0f, 0f);
            }
        }

        if (!changed) return false;
        undoStack.Push(snapshot);
        redoStack.Clear();
        RebuildWorld(false);
        statusMessage = $"Applied {chosen} at ({x},{y})";
        RefreshToolbarText();
        return true;
    }

    public bool Undo()
    {
        if (!isEditing || undoStack.Count == 0) return false;
        redoStack.Push(document.ToJson());
        document = CustomLevelDocument.FromJson(undoStack.Pop());
        RebuildWorld(false);
        statusMessage = "Undid last edit.";
        RefreshToolbarText();
        return true;
    }

    public bool Redo()
    {
        if (!isEditing || redoStack.Count == 0) return false;
        undoStack.Push(document.ToJson());
        document = CustomLevelDocument.FromJson(redoStack.Pop());
        RebuildWorld(false);
        statusMessage = "Redid edit.";
        RefreshToolbarText();
        return true;
    }

    public string GetDefaultDraftFilePath()
    {
        return Path.Combine(CustomLevelSceneAdapter.GetCustomLevelsFolder(), $"{document.levelId}.json");
    }

    public bool SaveDraft(string explicitPath = null)
    {
        List<string> errors = document.Validate();
        if (errors.Count > 0)
        {
            statusMessage = $"Cannot save: {errors[0]}";
            RefreshToolbarText();
            return false;
        }
        string path = explicitPath ?? GetDefaultDraftFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, document.ToJson());
        statusMessage = $"Saved {Path.GetFileName(path)}";
        RefreshToolbarText();
        return true;
    }

    public bool LoadDraft(string explicitPath = null)
    {
        string path = explicitPath ?? GetDefaultDraftFilePath();
        if (!File.Exists(path)) return false;
        CustomLevelDocument loaded = CustomLevelDocument.FromJson(File.ReadAllText(path));
        if (loaded.Validate().Count > 0) return false;
        document = loaded;
        undoStack.Clear();
        redoStack.Clear();
        RebuildWorld(!isEditing);
        statusMessage = $"Loaded {Path.GetFileName(path)}";
        RefreshToolbarText();
        return true;
    }

    public bool SaveCurrentLevelCampaignOverride()
    {
        bool ok = CustomLevelSceneAdapter.SaveCampaignOverride(currentCampaignLevel, document);
        statusMessage = ok
            ? $"Saved live Campaign Override for Level {currentCampaignLevel}!"
            : $"Cannot save override: {document.Validate()[0]}";
        RefreshToolbarText();
        return ok;
    }

    public bool ResetCurrentLevelCampaignOverride()
    {
        CustomLevelSceneAdapter.DeleteCampaignOverride(currentCampaignLevel);
        OpenCreatorForCampaignLevel(currentCampaignLevel);
        return true;
    }

    public int SaveAsPlayableCustomStage(int explicitStageNumber = 0)
    {
        int stageNumber = CustomLevelSceneAdapter.SaveAsNewCustomStage(document, explicitStageNumber);
        statusMessage = stageNumber >= 7
            ? $"Added playable Stage {stageNumber} to Level Selector!"
            : "Failed to save custom stage.";
        RefreshToolbarText();
        return stageNumber;
    }

    public void ClearToBlankTemplate()
    {
        if (!isEditing) return;
        undoStack.Push(document.ToJson());
        redoStack.Clear();
        document = CustomLevelDocument.CreateStarterDraft();
        document.sourceCampaignLevel = currentCampaignLevel;
        RebuildWorld(false);
        CenterCameraOnSpawn();
        statusMessage = "Created clean starter template with real tiles and prefabs.";
        RefreshToolbarText();
    }

    public void GenerateProceduralDraft(int seed = 0)
    {
        if (!isEditing) return;
        undoStack.Push(document.ToJson());
        redoStack.Clear();
        int chosenSeed = seed != 0 ? seed : System.Environment.TickCount;
        document = ProceduralQuarryGenerator.Generate(chosenSeed, 2);
        document.sourceCampaignLevel = currentCampaignLevel;
        RebuildWorld(false);
        CenterCameraOnSpawn();
        statusMessage = $"Generated procedural stage '{document.title}' (Seed {chosenSeed}).";
        RefreshToolbarText();
    }

    public string GetDefaultShareFilePath()
    {
        return Path.Combine(CustomLevelSceneAdapter.GetCustomLevelsFolder(), $"{document.levelId}{CustomLevelSharing.FileExtension}");
    }

    public bool ExportShareFile(string explicitPath = null)
    {
        string path = explicitPath ?? GetDefaultShareFilePath();
        if (!CustomLevelSharing.ExportToFile(document, path, out string error))
        {
            statusMessage = $"Export failed: {error}";
            RefreshToolbarText();
            return false;
        }
        statusMessage = $"Exported verified {Path.GetFileName(path)}";
        RefreshToolbarText();
        return true;
    }

    public bool ImportShareFile(string explicitPath = null)
    {
        string path = explicitPath ?? GetDefaultShareFilePath();
        if (!CustomLevelSharing.ImportFromFile(path, out CustomLevelDocument imported, out string error))
        {
            statusMessage = $"Import rejected: {error}";
            RefreshToolbarText();
            return false;
        }
        undoStack.Push(document.ToJson());
        redoStack.Clear();
        document = imported;
        RebuildWorld(!isEditing);
        CenterCameraOnSpawn();
        statusMessage = $"Imported verified {Path.GetFileName(path)}";
        RefreshToolbarText();
        return true;
    }

    public void StartPlaytest()
    {
        List<string> errors = document.Validate();
        if (errors.Count > 0)
        {
            statusMessage = $"Invalid level: {errors[0]}";
            RefreshToolbarText();
            return;
        }

        isEditing = false;
        if (!session) session = FindObjectOfType<GameSession>();
        if (session) session.EnterPlaytestMode(document.levelId);
        RebuildWorld(true);
        Camera cam = Camera.main;
        if (cam)
        {
            var brain = cam.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain) brain.enabled = true;
        }
        QuarryPressureHUD.EnsureForScene(session);
        statusMessage = "PLAYTESTING (isolated save) — click Edit Mode to return";
        RefreshToolbarText();
    }

    public void EnterEditMode(string message = "Returned to Edit Mode.")
    {
        isEditing = true;
        if (!session) session = FindObjectOfType<GameSession>();
        if (session && session.IsPlaytestMode) session.ExitPlaytestMode();
        QuarryPressureHUD hud = FindObjectOfType<QuarryPressureHUD>();
        if (hud) Destroy(hud.gameObject);
        GameObject banner = GameObject.Find("LevelResultsBanner");
        if (banner) Destroy(banner);
        RebuildWorld(false);
        Camera cam = Camera.main;
        if (cam)
        {
            var brain = cam.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain) brain.enabled = false;
            cam.orthographicSize = 9.5f;
        }
        CenterCameraOnSpawn();
        statusMessage = message;
        RefreshToolbarText();
    }

    void CenterCameraOnSpawn()
    {
        Camera cam = Camera.main;
        if (cam && document != null)
            cam.transform.position = new Vector3(document.spawnX + 6f, document.spawnY + 3f, -10f);
    }

    public void ExitCreatorToLevelSelector()
    {
        CustomLevelSceneAdapter.SetSuppressAutoOverride(false);
        if (session && session.IsPlaytestMode) session.ExitPlaytestMode();
        ScenePersist sp = FindObjectOfType<ScenePersist>();
        if (sp) sp.ResetScenePersist();
        SceneManager.LoadScene("Level selector");
    }

    void OnPlaytestDeath()
    {
        if (isEditing) return;
        EnterEditMode("Playtest ended (player died) — edits preserved.");
    }

    void OnPlaytestCompleted()
    {
        if (isEditing) return;
        LevelResult r = session ? session.LastCompletedLevelResult : default;
        statusMessage = $"PLAYTEST CLEAR! Time {LevelMedals.FormatDuration(r.ElapsedMs)} | Banked {r.BankedOre}";
        RefreshToolbarText();
    }

    void Update()
    {
        if (isEditing && Camera.main)
        {
            if (Input.GetMouseButtonDown(0))
            {
                UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
                if (!es || !es.IsPointerOverGameObject())
                {
                    Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    ApplyToolAtCell(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
                }
            }
            float panX = Input.GetAxisRaw("Horizontal");
            float panY = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(panX) > 0.01f || Mathf.Abs(panY) > 0.01f)
            {
                Vector3 pos = Camera.main.transform.position;
                pos.x = Mathf.Clamp(pos.x + panX * 14f * Time.unscaledDeltaTime, CustomLevelDocument.MinCoord, CustomLevelDocument.MaxCoord);
                pos.y = Mathf.Clamp(pos.y + panY * 14f * Time.unscaledDeltaTime, CustomLevelDocument.MinCoord, CustomLevelDocument.MaxCoord);
                Camera.main.transform.position = pos;
            }
        }
    }

    void RebuildWorld(bool spawnLivePlaytest)
    {
        if (spawnMarkerObj) DestroyImmediate(spawnMarkerObj);
        CustomLevelSceneAdapter.ApplyDocumentToCurrentScene(document, spawnLivePlaytest);

        if (!spawnLivePlaytest)
        {
            spawnMarkerObj = new GameObject("CreatorSpawnMarker");
            spawnMarkerObj.transform.position = new Vector3(document.spawnX, document.spawnY + 0.8f, 0f);
            TextMeshPro tmp = spawnMarkerObj.AddComponent<TextMeshPro>();
            tmp.text = "SPAWN";
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.35f, 1f, 0.55f);
            tmp.sortingOrder = 120;
        }
    }

    void BuildToolbarUI()
    {
        editorCanvasObj = new GameObject("CustomCreatorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = editorCanvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;
        CanvasScaler scaler = editorCanvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(editorCanvasObj.transform, false);
        RectTransform barRect = topBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.offsetMin = new Vector2(0f, -124f);
        barRect.offsetMax = Vector2.zero;
        topBar.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.11f, 0.94f);

        headerText = CreateText(topBar.transform, "HeaderText", new Vector2(12f, -4f), new Vector2(680f, 24f), 15f, TextAlignmentOptions.Left);
        toolText = CreateText(topBar.transform, "ToolText", new Vector2(12f, -26f), new Vector2(680f, 20f), 12f, TextAlignmentOptions.Left);
        toolText.color = new Color(0.65f, 0.92f, 1f);

        float lx = 636f;
        int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(StartMenu.UnlockedLevelKey, 1), 1, 6);
        for (int lvl = 1; lvl <= 6; lvl++)
        {
            int capturedLvl = lvl;
            bool isUnlocked = lvl <= unlocked;
            string btnLabel = isUnlocked ? $"REMIX L{lvl}" : $"L{lvl} LOCK";
            Color btnColor = isUnlocked ? new Color(0.18f, 0.28f, 0.42f) : new Color(0.16f, 0.18f, 0.22f, 0.6f);
            CreateToolbarButton(topBar.transform, $"Btn_LoadL{lvl}", btnLabel, new Vector2(lx, -6f), new Vector2(64f, 24f),
                btnColor, () =>
                {
                    if (!isUnlocked)
                    {
                        statusMessage = $"Level {capturedLvl} is locked! Beat earlier campaign levels first.";
                        RefreshToolbarText();
                        return;
                    }
                    LoadCampaignLevelForRemix(capturedLvl);
                });
            lx += 66f;
        }
        CreateToolbarButton(topBar.transform, "Btn_Blank", "BLANK", new Vector2(lx, -6f), new Vector2(56f, 24f),
            new Color(0.24f, 0.34f, 0.24f), ClearToBlankTemplate);
        lx += 58f;
        CreateToolbarButton(topBar.transform, "Btn_ProcGen", "GEN SEED", new Vector2(lx, -6f), new Vector2(68f, 24f),
            new Color(0.36f, 0.24f, 0.46f), () => GenerateProceduralDraft());
        lx += 70f;
        CreateToolbarButton(topBar.transform, "Btn_ExportShare", "EXPORT", new Vector2(lx, -6f), new Vector2(56f, 24f),
            new Color(0.16f, 0.38f, 0.44f), () => ExportShareFile());
        lx += 58f;
        CreateToolbarButton(topBar.transform, "Btn_ImportShare", "IMPORT", new Vector2(lx, -6f), new Vector2(56f, 24f),
            new Color(0.22f, 0.34f, 0.48f), () => ImportShareFile());

        string[] row1 =
        {
            "ground", "rock", "ladder", "bounce", "hazard_spike", "water_surface", "lava_surface", "bg", "spawn", "exit", "erase"
        };
        float x1 = 12f;
        for (int i = 0; i < row1.Length; i++)
        {
            string tool = row1[i];
            CreateToolbarButton(topBar.transform, $"Btn_{tool}", FormatToolLabel(tool), new Vector2(x1, -54f), new Vector2(96f, 26f),
                new Color(0.12f, 0.24f, 0.34f), () => SelectTool(tool));
            x1 += 100f;
        }

        string[] row2 =
        {
            "coin_100", "coin_150", "coin_200", "crate_250", "enemy", "red_enemy", "shielded_enemy",
            "sentry", "platform", "jump_pad", "bank", "vent", "gate_t1", "gate_t2", "stabilization"
        };
        float x2 = 10f;
        for (int i = 0; i < row2.Length; i++)
        {
            string tool = row2[i];
            CreateToolbarButton(topBar.transform, $"Btn_{tool}", FormatToolLabel(tool), new Vector2(x2, -86f), new Vector2(58f, 26f),
                new Color(0.18f, 0.22f, 0.36f), () => SelectTool(tool));
            x2 += 60f;
        }

        CreateToolbarButton(topBar.transform, "Btn_Undo", "UNDO", new Vector2(918f, -86f), new Vector2(54f, 26f),
            new Color(0.26f, 0.26f, 0.32f), () => Undo());
        CreateToolbarButton(topBar.transform, "Btn_SaveOverride", $"SAVE L{currentCampaignLevel}", new Vector2(976f, -86f), new Vector2(72f, 26f),
            new Color(0.14f, 0.44f, 0.26f), () => SaveCurrentLevelCampaignOverride());
        CreateToolbarButton(topBar.transform, "Btn_ResetOverride", "RESET L", new Vector2(1052f, -86f), new Vector2(64f, 26f),
            new Color(0.42f, 0.26f, 0.14f), () => ResetCurrentLevelCampaignOverride());
        CreateToolbarButton(topBar.transform, "Btn_SaveStage", "+STAGE", new Vector2(1120f, -86f), new Vector2(66f, 26f),
            new Color(0.22f, 0.42f, 0.52f), () => SaveAsPlayableCustomStage());
        CreateToolbarButton(topBar.transform, "Btn_Playtest", "PLAY", new Vector2(1190f, -86f), new Vector2(44f, 26f),
            new Color(0.85f, 0.48f, 0.12f), () =>
            {
                if (isEditing) StartPlaytest();
                else EnterEditMode();
            });
        CreateToolbarButton(topBar.transform, "Btn_ExitCreator", "EXIT", new Vector2(1236f, -86f), new Vector2(38f, 26f),
            new Color(0.48f, 0.16f, 0.16f), ExitCreatorToLevelSelector);
    }

    static string FormatToolLabel(string id)
    {
        switch (id)
        {
            case "ground": return "RULE GROUND";
            case "rock": return "ROCK TILE";
            case "ladder": return "LADDER";
            case "bounce": return "MUSHROOM";
            case "hazard_spike": return "SPIKES";
            case "water_surface": return "WATER";
            case "lava_surface": return "LAVA";
            case "bg": return "BG TILE";
            case "coin_100": return "COIN 100";
            case "coin_150": return "COIN 150";
            case "coin_200": return "COIN 200";
            case "crate_250": return "CRATE";
            case "red_enemy": return "RED ENM";
            case "shielded_enemy": return "SHIELD";
            case "sentry": return "SENTRY";
            case "platform": return "PLATFRM";
            case "jump_pad": return "JUMPPAD";
            case "stabilization": return "HEAL";
            default: return id.ToUpperInvariant();
        }
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchoredPos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void CreateToolbarButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.color = bgColor;
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(go.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 10f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 7f;
        tmp.fontSizeMax = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    void RefreshToolbarText()
    {
        if (headerText)
        {
            string modeLabel = isEditing ? $"REMIXING LEVEL {currentCampaignLevel}" : "ISOLATED PLAYTEST";
            string hasOverride = CustomLevelSceneAdapter.HasCampaignOverride(currentCampaignLevel) ? " [OVERRIDDEN]" : "";
            headerText.text = $"LEVEL CREATOR ({modeLabel}{hasOverride})  |  {document.tiles.Count} tiles, {document.objects.Count} objects";
        }
        if (toolText)
        {
            toolText.text = $"Tool: {FormatToolLabel(activeTool)}  |  Undo: {undoStack.Count}  |  {statusMessage}";
        }
        Transform playBtn = editorCanvasObj ? editorCanvasObj.transform.Find("TopBar/Btn_Playtest/Label") : null;
        if (playBtn)
        {
            TextMeshProUGUI label = playBtn.GetComponent<TextMeshProUGUI>();
            if (label) label.text = isEditing ? "PLAY" : "EDIT";
        }
    }
}
