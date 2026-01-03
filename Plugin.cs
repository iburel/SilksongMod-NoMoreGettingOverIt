using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.AddressableAssets;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NoMoreGettingOverIt;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("Hollow Knight Silksong.exe")]
public class Plugin : BaseUnityPlugin
{
    private const float SAVE_INTERVAL = 10f;
    private const int MAX_SAVESTATES = 12;

    private ConfigEntry<KeyCode> _loadKey = null!;

    private readonly List<SaveState> _savestates = new();
    private float _saveTimer = 0f;
    private bool _isLoading = false;

    private void Awake()
    {
        _loadKey = Config.Bind("Keybinds", "LoadSavestate", KeyCode.F2,
            "Key to load the most recent savestate and go back in time");

        Logger.LogInfo("NoMoreGettingOverIt loaded! Press F2 to go back in time.");
    }

    private void Update()
    {
        if (_isLoading) return;

        // Handle savestate loading on key press
        if (Input.GetKeyDown(_loadKey.Value))
        {
            LoadMostRecentSavestate();
            return;
        }

        // Auto-save timer
        if (!IsGameplayActive()) return;

        _saveTimer += Time.deltaTime;

        if (_saveTimer >= SAVE_INTERVAL)
        {
            _saveTimer = 0f;
            CreateSavestate();
        }
    }

    private bool IsGameplayActive()
    {
        if (HeroController.instance == null) return false;
        if (GameManager.instance == null) return false;
        if (GameManager.instance.isPaused) return false;
        if (HeroController.instance.cState.dead) return false;
        if (HeroController.instance.cState.transitioning) return false;

        return true;
    }

    private void CreateSavestate()
    {
        var savestate = SaveState.Capture();
        if (savestate == null)
        {
            Logger.LogWarning("Failed to create savestate - hero not available");
            return;
        }

        _savestates.Add(savestate);

        // Keep only the last MAX_SAVESTATES
        while (_savestates.Count > MAX_SAVESTATES)
        {
            _savestates.RemoveAt(0);
        }

        Logger.LogInfo($"Savestate created ({_savestates.Count}/{MAX_SAVESTATES})");
    }

    private void LoadMostRecentSavestate()
    {
        if (_savestates.Count == 0)
        {
            Logger.LogWarning("No savestates available!");
            return;
        }

        // Get and remove the most recent savestate
        int lastIndex = _savestates.Count - 1;
        var savestate = _savestates[lastIndex];
        _savestates.RemoveAt(lastIndex);

        // Reset timer to avoid immediate savestate after load
        _saveTimer = 0f;

        Logger.LogInfo($"Loading savestate... ({_savestates.Count} remaining)");
        StartCoroutine(LoadSavestateCoroutine(savestate));
    }

    private IEnumerator LoadSavestateCoroutine(SaveState savestate)
    {
        _isLoading = true;

        string currentScene = GameManager.instance?.sceneName ?? "";
        bool needsSceneChange = currentScene != savestate.SceneName;

        if (needsSceneChange)
        {
            // Load the scene directly using Addressables
            Addressables.LoadSceneAsync($"Scenes/{savestate.SceneName}");
            yield return new WaitUntil(() => USceneManager.GetActiveScene().name == savestate.SceneName);
        }

        // Wait for hero to be available
        while (HeroController.instance == null)
            yield return null;
        yield return null;

        // Restore the savestate
        savestate.Restore();

        // Snap camera to position
        GameCameras.instance?.cameraController?.SnapTo(savestate.Position.x, savestate.Position.y);

        // Force position for 0.6s to counter spawn entry animation
        var hero = HeroController.instance;
        var heroGO = (hero as MonoBehaviour)?.gameObject;
        var rb = heroGO?.GetComponent<Rigidbody2D>();

        float endTime = Time.time + 0.6f;
        while (Time.time < endTime)
        {
            if (heroGO != null)
                heroGO.transform.position = savestate.Position;
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        // Restore velocity after position lock
        if (rb != null)
            rb.linearVelocity = savestate.Velocity;

        _isLoading = false;
        Logger.LogInfo("Savestate loaded successfully!");
    }
}

[System.Serializable]
public class SaveState
{
    public string SceneName = "";
    public Vector3 Position;
    public Vector2 Velocity;

    // Player stats
    public int Health;
    public int MaxHealth;
    public int Geo;
    public int Silk;

    // Player state flags
    public bool FacingRight;

    public static SaveState? Capture()
    {
        var hero = HeroController.instance;
        if (hero == null) return null;

        var heroGO = (hero as MonoBehaviour)?.gameObject;
        if (heroGO == null) return null;

        var rb = heroGO.GetComponent<Rigidbody2D>();
        var pd = PlayerData.instance;
        var gm = GameManager.instance;

        if (pd == null || gm == null) return null;

        return new SaveState
        {
            SceneName = gm.sceneName,
            Position = heroGO.transform.position,
            Velocity = rb != null ? rb.linearVelocity : Vector2.zero,
            Health = pd.health,
            MaxHealth = pd.maxHealth,
            Geo = pd.geo,
            Silk = pd.silk,
            FacingRight = hero.cState.facingRight
        };
    }

    public void Restore()
    {
        var hero = HeroController.instance;
        if (hero == null) return;

        var heroGO = (hero as MonoBehaviour)?.gameObject;
        if (heroGO == null) return;

        var rb = heroGO.GetComponent<Rigidbody2D>();
        var pd = PlayerData.instance;

        if (pd == null) return;

        // Restore position
        heroGO.transform.position = Position;

        // Restore velocity
        if (rb != null)
            rb.linearVelocity = Velocity;

        // Restore stats
        pd.health = Health;
        pd.maxHealth = MaxHealth;
        pd.geo = Geo;
        pd.silk = Silk;

        // Restore facing direction
        if (hero.cState.facingRight != FacingRight)
        {
            hero.FlipSprite();
        }
    }
}
