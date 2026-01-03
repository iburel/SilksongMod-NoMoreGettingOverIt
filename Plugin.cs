using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NoMoreGettingOverIt;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private ConfigEntry<KeyCode>? _rewindKey;
    private ConfigEntry<float>? _saveInterval;
    private ConfigEntry<int>? _maxSavestates;

    private readonly List<SaveState> _savestates = new();
    private float _timer;
    private bool _isRestoring;

    private void Awake()
    {
        _rewindKey = Config.Bind("Keybinds", "RewindKey", KeyCode.F2, "Key to rewind to the previous savestate");
        _saveInterval = Config.Bind("Settings", "SaveInterval", 10f, "Interval in seconds between automatic savestates");
        _maxSavestates = Config.Bind("Settings", "MaxSavestates", 12, "Maximum number of savestates to keep (12 = 2 minutes at 10s interval)");

        Logger.LogInfo("NoMoreGettingOverIt loaded! Press F2 to rewind.");
    }

    private void Update()
    {
        if (_isRestoring) return;

        var hero = HeroController.instance;
        if (hero == null) return;

        if (Input.GetKeyDown(_rewindKey!.Value))
        {
            LoadSaveState();
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _saveInterval!.Value)
        {
            _timer = 0f;
            CreateSaveState(hero);
        }
    }

    private void CreateSaveState(HeroController hero)
    {
        var gm = GameManager.instance;
        if (gm == null) return;

        var pd = PlayerData.instance;
        if (pd == null) return;

        if (gm.IsInSceneTransition) return;
        if (hero.cState.dead) return;
        if (hero.cState.transitioning) return;

        var heroGO = hero.gameObject;
        var rb = heroGO.GetComponent<Rigidbody2D>();

        var savestate = new SaveState
        {
            Position = heroGO.transform.position,
            Velocity = rb != null ? rb.linearVelocity : Vector2.zero,
            Health = pd.health,
            MaxHealth = pd.maxHealth,
            Geo = pd.geo,
            Silk = pd.silk,
            SceneName = gm.GetSceneNameString(),
            FacingRight = hero.cState.facingRight
        };

        _savestates.Add(savestate);

        while (_savestates.Count > _maxSavestates!.Value)
        {
            _savestates.RemoveAt(0);
        }

        Logger.LogInfo($"Savestate created ({_savestates.Count}/{_maxSavestates.Value})");
    }

    private void LoadSaveState()
    {
        if (_savestates.Count == 0)
        {
            Logger.LogWarning("No savestate available to load!");
            return;
        }

        var hero = HeroController.instance;
        if (hero == null) return;

        var gm = GameManager.instance;
        if (gm == null) return;

        var savestate = _savestates[^1];
        _savestates.RemoveAt(_savestates.Count - 1);
        _timer = 0f;

        if (gm.GetSceneNameString() != savestate.SceneName)
        {
            StartCoroutine(LoadCrossSceneSaveState(savestate));
        }
        else
        {
            RestoreSameSceneSaveState(savestate, hero);
        }
    }

    private IEnumerator LoadCrossSceneSaveState(SaveState savestate)
    {
        _isRestoring = true;

        // Load target scene
        Addressables.LoadSceneAsync($"Scenes/{savestate.SceneName}");
        yield return new WaitUntil(() => USceneManager.GetActiveScene().name == savestate.SceneName);

        // Wait for hero to spawn
        while (HeroController.instance == null)
        {
            yield return null;
        }
        yield return null;

        var hero = HeroController.instance;
        var heroGO = hero.gameObject;
        var rb = heroGO.GetComponent<Rigidbody2D>();

        // Restore stats
        var pd = PlayerData.instance;
        pd.health = savestate.Health;
        pd.maxHealth = savestate.MaxHealth;
        pd.geo = savestate.Geo;
        pd.silk = savestate.Silk;

        // Restore facing direction
        if (hero.cState.facingRight != savestate.FacingRight)
        {
            hero.FlipSprite();
        }

        // Snap camera to position
        var cam = GameCameras.instance;
        if (cam != null && cam.cameraController != null)
        {
            cam.cameraController.SnapTo(savestate.Position.x, savestate.Position.y);
        }

        // Lock position for 0.6s to counter entry animation
        float endTime = Time.time + 0.6f;
        while (Time.time < endTime)
        {
            heroGO.transform.position = savestate.Position;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            yield return null;
        }

        // Restore velocity after lock
        if (rb != null)
        {
            rb.linearVelocity = savestate.Velocity;
        }

        Logger.LogInfo($"Rewound to {savestate.SceneName}! ({_savestates.Count} remaining)");
        _isRestoring = false;
    }

    private void RestoreSameSceneSaveState(SaveState savestate, HeroController hero)
    {
        var heroGO = hero.gameObject;
        heroGO.transform.position = savestate.Position;

        var rb = heroGO.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = savestate.Velocity;
        }

        var pd = PlayerData.instance;
        pd.health = savestate.Health;
        pd.maxHealth = savestate.MaxHealth;
        pd.geo = savestate.Geo;
        pd.silk = savestate.Silk;

        // Restore facing direction
        if (hero.cState.facingRight != savestate.FacingRight)
        {
            hero.FlipSprite();
        }

        var cam = GameCameras.instance;
        if (cam != null && cam.cameraController != null)
        {
            cam.cameraController.SnapTo(savestate.Position.x, savestate.Position.y);
        }

        Logger.LogInfo($"Rewound! ({_savestates.Count} remaining)");
    }

    private class SaveState
    {
        public Vector3 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Geo { get; set; }
        public int Silk { get; set; }
        public string SceneName { get; set; } = string.Empty;
        public bool FacingRight { get; set; }
    }
}
