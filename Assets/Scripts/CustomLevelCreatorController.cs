using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public sealed class CustomLevelCreatorController : MonoBehaviour
{
    public const string HostSceneName = "Level 4";
    static bool launchRequested;

    CustomLevelDocument document;
    readonly Stack<string> undoStack = new Stack<string>();
    readonly Stack<string> redoStack = new Stack<string>();

    bool isEditing = true;
    string activeTool = "ground";
    string statusMessage = "Ready";

    GameObject customRoot;
    GameObject editorCanvasObj;
    TextMeshProUGUI headerText;
    TextMeshProUGUI toolText;

    readonly List<GameObject> hiddenCampaignRoots = new List<GameObject>();
    PlayerMovement templatePlayer;
    LevelExit templateExit;
    Collecting templateCoin100;
    Collecting templateCoin150;
    BreakableOreCrate templateCrate;
    OreBankCheckpoint templateBank;
    PressurePulseHazard templateVent;
    PressureGate templateGate;
    EnemyPatrol2D templateEnemy;
    Sprite groundSprite;
    Sprite lavaSprite;
    Sprite spikeSprite;

    PlayerMovement activePlayer;
    GameSession session;

    public CustomLevelDocument Document => document;
    public bool IsEditing => isEditing;
    public string ActiveTool => activeTool;
    public string StatusMessage => statusMessage;

    public static void OpenCreatorFromMenu()
    {
        launchRequested = true;
        Time.timeScale = 1f;
        PauseMenu.GameIsPaused = false;
        SceneManager.LoadScene(HostSceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoaded()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (launchRequested && scene.name == HostSceneName)
            {
                launchRequested = false;
                var host = new GameObject("CustomLevelCreatorController");
                host.AddComponent<CustomLevelCreatorController>();
            }
        };
    }

    void Start()
    {
        session = FindObjectOfType<GameSession>();
        if (session)
        {
            session.PlaytestDeathOccurred += OnPlaytestDeath;
            session.PlaytestCompleted += OnPlaytestCompleted;
        }

        CaptureTemplatesAndHideCampaign();
        document = LoadOrDefaultDraft();
        BuildToolbarUI();
        EnterEditMode("Creator ready. Left-click to paint, Playtest to run.");
    }

    void OnDestroy()
    {
        if (session)
        {
            session.PlaytestDeathOccurred -= OnPlaytestDeath;
            session.PlaytestCompleted -= OnPlaytestCompleted;
            if (session.IsPlaytestMode) session.ExitPlaytestMode();
        }
    }

    void CaptureTemplatesAndHideCampaign()
    {
        ScenePersist sp = FindObjectOfType<ScenePersist>();
        if (sp) sp.ResetScenePersist();

        templatePlayer = FindObjectOfType<PlayerMovement>(true);
        templateExit = FindObjectOfType<LevelExit>(true);
        templateCrate = FindObjectOfType<BreakableOreCrate>(true);
        templateBank = FindObjectOfType<OreBankCheckpoint>(true);
        templateVent = FindObjectOfType<PressurePulseHazard>(true);
        templateGate = FindObjectOfType<PressureGate>(true);
        templateEnemy = FindObjectOfType<EnemyPatrol2D>(true);

        foreach (Collecting coin in FindObjectsOfType<Collecting>(true))
        {
            if (coin.BaseOreValue >= 150 && !templateCoin150) templateCoin150 = coin;
            else if (coin.BaseOreValue == 100 && !templateCoin100) templateCoin100 = coin;
        }
        if (!templateCoin150) templateCoin150 = templateCoin100;

        foreach (Tilemap map in FindObjectsOfType<Tilemap>(true))
        {
            foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
            {
                Sprite s = map.GetSprite(pos);
                if (!s) continue;
                if (map.gameObject.layer == LayerMask.NameToLayer("Ground") && !groundSprite)
                    groundSprite = s;
                else if (map.gameObject.layer == LayerMask.NameToLayer("Hazard") && !spikeSprite)
                    spikeSprite = s;
            }
        }
        if (templateVent && templateVent.GetComponent<SpriteRenderer>())
            lavaSprite = templateVent.GetComponent<SpriteRenderer>().sprite;
        if (!groundSprite && templateCrate && templateCrate.GetComponent<SpriteRenderer>())
            groundSprite = templateCrate.GetComponent<SpriteRenderer>().sprite;
        if (!spikeSprite) spikeSprite = lavaSprite;

        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            if (root == gameObject) continue;
            if (root.GetComponentInChildren<Camera>(true)) continue;
            if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true)) continue;
            if (root.activeSelf)
            {
                hiddenCampaignRoots.Add(root);
                root.SetActive(false);
            }
        }

        Camera cam = Camera.main;
        if (cam)
        {
            var brain = cam.GetComponent<Cinemachine.CinemachineBrain>();
            if (brain) brain.enabled = false;
            cam.orthographicSize = 9.5f;
            cam.transform.position = new Vector3(12f, 6.5f, -10f);
        }
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
        else if (CustomLevelDocument.IsAllowedObjectId(chosen)) changed = document.SetObject(x, y, chosen);

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
        string folder = Path.Combine(Application.persistentDataPath, "CustomLevels");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{document.levelId}.json");
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

    CustomLevelDocument LoadOrDefaultDraft()
    {
        return CustomLevelDocument.CreateStarterDraft();
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
            cam.orthographicSize = 9.5f;
            cam.transform.position = new Vector3(12f, 6.5f, -10f);
        }
        statusMessage = message;
        RefreshToolbarText();
    }

    public void ExitCreatorToLevelSelector()
    {
        if (session && session.IsPlaytestMode) session.ExitPlaytestMode();
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
                pos.x = Mathf.Clamp(pos.x + panX * 10f * Time.unscaledDeltaTime, 6f, document.width - 6f);
                pos.y = Mathf.Clamp(pos.y + panY * 10f * Time.unscaledDeltaTime, 4f, document.height - 4f);
                Camera.main.transform.position = pos;
            }
        }
        else if (!isEditing && activePlayer && Camera.main)
        {
            Vector3 p = activePlayer.transform.position;
            Camera.main.transform.position = new Vector3(
                Mathf.Clamp(p.x, 8f, Mathf.Max(8f, document.width - 8f)),
                Mathf.Clamp(p.y + 1.5f, 5f, Mathf.Max(5f, document.height - 5f)),
                -10f);
        }
    }

    void RebuildWorld(bool spawnLivePlaytest)
    {
        if (customRoot) DestroyImmediate(customRoot);
        customRoot = new GameObject("CustomLevelRuntimeRoot");

        for (int i = 0; i < document.tiles.Count; i++)
        {
            CustomLevelCell cell = document.tiles[i];
            BuildTileObject(cell.x, cell.y, cell.id);
        }

        for (int i = 0; i < document.objects.Count; i++)
        {
            CustomLevelCell obj = document.objects[i];
            BuildPlacedObject(obj.x, obj.y, obj.id, spawnLivePlaytest);
        }

        BuildEndpointMarker("SpawnMarker", document.spawnX, document.spawnY, "SPAWN", new Color(0.35f, 0.95f, 0.55f));
        if (spawnLivePlaytest && templateExit)
        {
            LevelExit exitInstance = Instantiate(templateExit, new Vector3(document.exitX, document.exitY, 0f),
                Quaternion.identity, customRoot.transform);
            exitInstance.gameObject.SetActive(true);
            exitInstance.name = "CustomLevelExit";
        }
        else
        {
            BuildEndpointMarker("ExitMarker", document.exitX, document.exitY, "EXIT", new Color(0.45f, 0.85f, 1f));
        }

        if (spawnLivePlaytest && templatePlayer)
        {
            activePlayer = Instantiate(templatePlayer, new Vector3(document.spawnX, document.spawnY + 0.35f, 0f),
                Quaternion.identity, customRoot.transform);
            activePlayer.gameObject.SetActive(true);
            activePlayer.name = "CustomPlaytestPlayer";
        }
    }

    void BuildTileObject(int x, int y, string id)
    {
        var go = new GameObject($"Tile_{id}_{x}_{y}");
        go.transform.SetParent(customRoot.transform, false);
        go.transform.position = new Vector3(x, y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = id == "lava" ? lavaSprite : id == "hazard_spike" ? spikeSprite : groundSprite;
        sr.sortingLayerName = "Platforme";
        sr.sortingOrder = 2;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        if (id == "ground")
        {
            go.layer = LayerMask.NameToLayer("Ground");
            sr.color = new Color(0.55f, 0.65f, 0.72f);
        }
        else
        {
            go.layer = LayerMask.NameToLayer("Hazard");
            sr.color = id == "lava" ? new Color(1f, 0.38f, 0.1f) : new Color(0.95f, 0.3f, 0.35f);
        }
    }

    void BuildPlacedObject(int x, int y, string id, bool live)
    {
        Vector3 pos = new Vector3(x, y, 0f);
        GameObject spawned = null;
        if (id == "coin_100" && templateCoin100) spawned = Instantiate(templateCoin100.gameObject, pos, Quaternion.identity, customRoot.transform);
        else if (id == "coin_150" && templateCoin150) spawned = Instantiate(templateCoin150.gameObject, pos, Quaternion.identity, customRoot.transform);
        else if (id == "crate_250" && templateCrate) spawned = Instantiate(templateCrate.gameObject, pos, Quaternion.identity, customRoot.transform);
        else if (id == "bank" && templateBank) spawned = Instantiate(templateBank.gameObject, pos, Quaternion.identity, customRoot.transform);
        else if (id == "vent" && templateVent) spawned = Instantiate(templateVent.gameObject, pos, Quaternion.identity, customRoot.transform);
        else if (id == "gate_t1" && templateGate) spawned = Instantiate(templateGate.gameObject, pos + Vector3.up * 0.5f, Quaternion.identity, customRoot.transform);
        else if (id == "enemy" && templateEnemy) spawned = Instantiate(templateEnemy.gameObject, pos, Quaternion.identity, customRoot.transform);

        if (spawned)
        {
            spawned.name = $"CustomObj_{id}_{x}_{y}";
            spawned.SetActive(true);
            if (!live)
            {
                EnemyPatrol2D patrol = spawned.GetComponent<EnemyPatrol2D>();
                if (patrol) patrol.enabled = false;
                Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
                if (rb) rb.simulated = false;
            }
        }
    }

    void BuildEndpointMarker(string markerName, int x, int y, string textLabel, Color color)
    {
        var go = new GameObject(markerName);
        go.transform.SetParent(customRoot.transform, false);
        go.transform.position = new Vector3(x, y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = groundSprite;
        sr.color = color;
        sr.sortingLayerName = "Exit";
        sr.sortingOrder = 20;

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(go.transform, false);
        labelObj.transform.localPosition = Vector3.up * 0.9f;
        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = textLabel;
        tmp.fontSize = 2.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.sortingLayerID = sr.sortingLayerID;
        tmp.sortingOrder = 100;
    }

    void BuildToolbarUI()
    {
        editorCanvasObj = new GameObject("CustomCreatorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = editorCanvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
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
        barRect.offsetMin = new Vector2(0f, -92f);
        barRect.offsetMax = Vector2.zero;
        topBar.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

        headerText = CreateText(topBar.transform, "HeaderText", new Vector2(16f, -6f), new Vector2(720f, 28f), 16f, TextAlignmentOptions.Left);
        toolText = CreateText(topBar.transform, "ToolText", new Vector2(16f, -34f), new Vector2(720f, 22f), 13f, TextAlignmentOptions.Left);
        toolText.color = new Color(0.65f, 0.9f, 1f);

        string[] palette = { "ground", "lava", "coin_100", "crate_250", "bank", "vent", "gate_t1", "erase" };
        float x = 16f;
        for (int i = 0; i < palette.Length; i++)
        {
            string tool = palette[i];
            CreateToolbarButton(topBar.transform, $"Btn_{tool}", tool.ToUpperInvariant(), new Vector2(x, -60f), new Vector2(88f, 26f),
                new Color(0.14f, 0.24f, 0.34f), () => SelectTool(tool));
            x += 94f;
        }

        CreateToolbarButton(topBar.transform, "Btn_Undo", "UNDO", new Vector2(780f, -60f), new Vector2(68f, 26f),
            new Color(0.25f, 0.25f, 0.32f), () => Undo());
        CreateToolbarButton(topBar.transform, "Btn_Save", "SAVE", new Vector2(854f, -60f), new Vector2(68f, 26f),
            new Color(0.16f, 0.42f, 0.28f), () => SaveDraft());
        CreateToolbarButton(topBar.transform, "Btn_Playtest", "PLAYTEST", new Vector2(928f, -60f), new Vector2(104f, 26f),
            new Color(0.85f, 0.48f, 0.12f), () =>
            {
                if (isEditing) StartPlaytest();
                else EnterEditMode();
            });
        CreateToolbarButton(topBar.transform, "Btn_ExitCreator", "BACK", new Vector2(1038f, -60f), new Vector2(76f, 26f),
            new Color(0.45f, 0.18f, 0.18f), ExitCreatorToLevelSelector);
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
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    void RefreshToolbarText()
    {
        if (headerText)
        {
            string modeLabel = isEditing ? "EDIT MODE" : "ISOLATED PLAYTEST";
            headerText.text = $"LEVEL CREATOR ({modeLabel})  |  {document.title} ({document.tiles.Count} tiles, {document.objects.Count} objects)";
        }
        if (toolText)
        {
            toolText.text = $"Active Tool: {activeTool.ToUpperInvariant()}  |  Undo: {undoStack.Count}  |  {statusMessage}";
        }
        Transform playBtn = editorCanvasObj ? editorCanvasObj.transform.Find("TopBar/Btn_Playtest/Label") : null;
        if (playBtn)
        {
            TextMeshProUGUI label = playBtn.GetComponent<TextMeshProUGUI>();
            if (label) label.text = isEditing ? "PLAYTEST" : "EDIT MODE";
        }
    }
}
