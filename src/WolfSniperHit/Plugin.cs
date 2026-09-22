using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace WolfSniperHit
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.wjy13.wolfsniperhit";
        public const string PluginName = "Wolf Sniper Hit";
        public const string PluginVersion = "0.1.1";

        private const string AudioFileName = "howl_bgm.ogg";
        private const string ImageFileName = "howl_flash.png";

        private Harmony _harmony;
        private GameObject _effectRoot;
        private RawImage _flashImage;
        private AudioSource _audioSource;
        private AudioClip _audioClip;
        private Coroutine _flashCoroutine;
        private bool _audioLoading;
        private bool _playWhenLoaded;
        private int _triggerCount;
        private float _lastTriggerTime = float.NegativeInfinity;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _onlyOnKill;
        private ConfigEntry<bool> _flashEnabled;
        private ConfigEntry<bool> _audioEnabled;
        private ConfigEntry<float> _audioVolume;
        private ConfigEntry<float> _speedMultiplier;
        private ConfigEntry<float> _maximumSpeed;
        private ConfigEntry<float> _resetAfterSeconds;
        private ConfigEntry<float> _fadeInSeconds;
        private ConfigEntry<float> _holdSeconds;
        private ConfigEntry<float> _fadeOutSeconds;
        private ConfigEntry<float> _jitterPixels;
        private ConfigEntry<bool> _detectByScope;
        private ConfigEntry<bool> _detectByName;
        private ConfigEntry<bool> _detectByDamage;
        private ConfigEntry<int> _damageThreshold;

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            CreateEffectObjects();
            LoadImage();
            StartCoroutine(LoadAudio());

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Waiting for local sniper hits.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            if (_audioClip != null)
            {
                Destroy(_audioClip);
            }

            if (_effectRoot != null)
            {
                Destroy(_effectRoot);
            }

            Instance = null;
        }

        private void BindConfig()
        {
            _enabled = Config.Bind("General", "Enabled", true,
                "Enable the complete wolf image and Animals effect.");
            _onlyOnKill = Config.Bind("General", "OnlyOnKill", false,
                "False: trigger whenever the local player's sniper hits. True: trigger only for lethal hits.");

            _detectByScope = Config.Bind("Sniper Detection", "DetectByScope", true,
                "Treat weapons using the game's sniper scope UI as snipers.");
            _detectByName = Config.Bind("Sniper Detection", "DetectByName", true,
                "Treat weapons whose object or localized item name contains 'sniper' as snipers.");
            _detectByDamage = Config.Bind("Sniper Detection", "DetectByDamage", false,
                "Also treat any held weapon at or above DamageThreshold as a sniper fallback.");
            _damageThreshold = Config.Bind("Sniper Detection", "DamageThreshold", 150,
                new ConfigDescription("Minimum weapon damage for damage-based fallback detection.",
                    new AcceptableValueRange<int>(1, 10000)));

            _flashEnabled = Config.Bind("Visual", "FlashEnabled", true,
                "Show the full-screen wolf image when triggered.");
            _fadeInSeconds = Config.Bind("Visual", "FadeInSeconds", 0.12f,
                new ConfigDescription("Wolf image fade-in time, using unscaled time.",
                    new AcceptableValueRange<float>(0.01f, 5f)));
            _holdSeconds = Config.Bind("Visual", "HoldSeconds", 0.20f,
                new ConfigDescription("Time the wolf image remains fully visible.",
                    new AcceptableValueRange<float>(0f, 5f)));
            _fadeOutSeconds = Config.Bind("Visual", "FadeOutSeconds", 0.55f,
                new ConfigDescription("Wolf image fade-out time, using unscaled time.",
                    new AcceptableValueRange<float>(0.01f, 5f)));
            _jitterPixels = Config.Bind("Visual", "JitterPixels", 18f,
                new ConfigDescription("Maximum full-screen image shake in pixels.",
                    new AcceptableValueRange<float>(0f, 100f)));

            _audioEnabled = Config.Bind("Audio", "AudioEnabled", true,
                "Restart Animals from the beginning whenever the effect triggers.");
            _audioVolume = Config.Bind("Audio", "Volume", 1f,
                new ConfigDescription("Animals playback volume.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _speedMultiplier = Config.Bind("Audio", "SpeedMultiplierPerHit", 1.1f,
                new ConfigDescription("Each consecutive trigger multiplies playback speed by this value.",
                    new AcceptableValueRange<float>(1f, 2f)));
            _maximumSpeed = Config.Bind("Audio", "MaximumSpeed", 2f,
                new ConfigDescription("Maximum Animals playback speed.",
                    new AcceptableValueRange<float>(1f, 3f)));
            _resetAfterSeconds = Config.Bind("Audio", "ResetAfterSeconds", 15f,
                new ConfigDescription("Reset the speed chain after this many seconds without a sniper hit. Set to 0 to never reset until the game closes.",
                    new AcceptableValueRange<float>(0f, 600f)));
        }

        private void CreateEffectObjects()
        {
            _effectRoot = new GameObject("WolfSniperHit_Effects");
            DontDestroyOnLoad(_effectRoot);

            _audioSource = _effectRoot.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.ignoreListenerPause = true;

            GameObject canvasObject = new GameObject("WolfSniperHit_Canvas");
            canvasObject.transform.SetParent(_effectRoot.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            GameObject imageObject = new GameObject("WolfSniperHit_Image");
            imageObject.transform.SetParent(canvasObject.transform, false);
            _flashImage = imageObject.AddComponent<RawImage>();
            _flashImage.raycastTarget = false;
            _flashImage.color = new Color(1f, 1f, 1f, 0f);

            RectTransform rect = _flashImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void LoadImage()
        {
            string imagePath = AssetPath(ImageFileName);
            if (!File.Exists(imagePath))
            {
                Logger.LogWarning("Wolf image was not found: " + imagePath);
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(imagePath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "WolfSniperHit_Texture";
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    Destroy(texture);
                    Logger.LogWarning("Unity could not decode the wolf image: " + imagePath);
                    return;
                }

                _flashImage.texture = texture;
            }
            catch (Exception exception)
            {
                Logger.LogError("Could not load wolf image: " + exception);
            }
        }

        private IEnumerator LoadAudio()
        {
            if (_audioLoading || _audioClip != null)
            {
                yield break;
            }

            string audioPath = AssetPath(AudioFileName);
            if (!File.Exists(audioPath))
            {
                Logger.LogWarning("Animals audio was not found: " + audioPath);
                yield break;
            }

            _audioLoading = true;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                       new Uri(audioPath).AbsoluteUri, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogWarning("Could not load Animals audio: " + request.error);
                    _audioLoading = false;
                    yield break;
                }

                _audioClip = DownloadHandlerAudioClip.GetContent(request);
                _audioClip.name = "WolfSniperHit_Animals";
                _audioSource.clip = _audioClip;
            }

            _audioLoading = false;
            if (_playWhenLoaded)
            {
                _playWhenLoaded = false;
                PlayAudio(CurrentSpeed());
            }
        }

        private string AssetPath(string fileName)
        {
            string pluginDirectory = Path.GetDirectoryName(Info.Location);
            return Path.Combine(pluginDirectory ?? Paths.PluginPath, "assets", fileName);
        }

        internal void OnLocalHit(bool killed)
        {
            if (!_enabled.Value || (_onlyOnKill.Value && !killed))
            {
                return;
            }

            Weapon weapon = GetLocalWeapon();
            if (weapon == null || !IsSniper(weapon))
            {
                return;
            }

            if (_resetAfterSeconds.Value > 0f &&
                Time.unscaledTime - _lastTriggerTime > _resetAfterSeconds.Value)
            {
                _triggerCount = 0;
            }

            _lastTriggerTime = Time.unscaledTime;
            _triggerCount++;
            float speed = CurrentSpeed();

            if (_audioEnabled.Value)
            {
                PlayAudio(speed);
            }

            if (_flashEnabled.Value && _flashImage != null && _flashImage.texture != null)
            {
                if (_flashCoroutine != null)
                {
                    StopCoroutine(_flashCoroutine);
                }
                _flashCoroutine = StartCoroutine(FlashImage());
            }

            Logger.LogDebug($"Sniper hit triggered wolf effect at {speed:0.00}x (kill={killed}).");
        }

        private float CurrentSpeed()
        {
            int exponent = Math.Max(0, _triggerCount - 1);
            return Mathf.Min(Mathf.Pow(_speedMultiplier.Value, exponent), _maximumSpeed.Value);
        }

        private void PlayAudio(float speed)
        {
            if (_audioClip == null)
            {
                _playWhenLoaded = true;
                if (!_audioLoading)
                {
                    StartCoroutine(LoadAudio());
                }
                return;
            }

            _audioSource.Stop();
            _audioSource.volume = _audioVolume.Value;
            _audioSource.pitch = speed;
            _audioSource.time = 0f;
            _audioSource.Play();
        }

        private IEnumerator FlashImage()
        {
            RectTransform rect = _flashImage.rectTransform;
            SetFlashAlpha(0f);

            float elapsed = 0f;
            while (elapsed < _fadeInSeconds.Value)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFlashAlpha(Mathf.Clamp01(elapsed / _fadeInSeconds.Value));
                ApplyJitter(rect);
                yield return null;
            }

            SetFlashAlpha(1f);
            elapsed = 0f;
            while (elapsed < _holdSeconds.Value)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyJitter(rect);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < _fadeOutSeconds.Value)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFlashAlpha(1f - Mathf.Clamp01(elapsed / _fadeOutSeconds.Value));
                ApplyJitter(rect);
                yield return null;
            }

            rect.localPosition = Vector3.zero;
            SetFlashAlpha(0f);
            _flashCoroutine = null;
        }

        private void ApplyJitter(RectTransform rect)
        {
            float amount = _jitterPixels.Value;
            rect.localPosition = new Vector3(
                UnityEngine.Random.Range(-amount, amount),
                UnityEngine.Random.Range(-amount, amount),
                0f);
        }

        private void SetFlashAlpha(float alpha)
        {
            _flashImage.color = new Color(1f, 1f, 1f, alpha);
        }

        private Weapon GetLocalWeapon()
        {
            Player player = Player.LocalPlayer;
            if (player == null || player.Holding == null || player.Holding.HeldItem == null)
            {
                return null;
            }

            return player.Holding.HeldItem.Weapon;
        }

        private bool IsSniper(Weapon weapon)
        {
            try
            {
                if (_detectByScope.Value && weapon.Attachments != null && weapon.Attachments.UseSniperUi)
                {
                    return true;
                }

                if (_detectByName.Value)
                {
                    string objectName = weapon.gameObject != null ? weapon.gameObject.name : string.Empty;
                    string itemName = weapon.GetName() ?? string.Empty;
                    if (ContainsSniper(objectName) || ContainsSniper(itemName))
                    {
                        return true;
                    }
                }

                return _detectByDamage.Value && weapon.Damage >= _damageThreshold.Value;
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Could not identify held weapon: " + exception.Message);
                return false;
            }
        }

        private static bool ContainsSniper(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.IndexOf("sniper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("大狙", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("狙击", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private sealed class LocalHitState
        {
            internal bool Candidate;
            internal int HpBefore;
        }

        [HarmonyPatch(typeof(Creature), nameof(Creature.LocalHit))]
        private static class CreatureLocalHitPatch
        {
            private static readonly FieldInfo LocalHpField =
                AccessTools.Field(typeof(Creature), "_localHp");

            private static void Prefix(
                Creature __instance,
                Player player,
                int damage,
                bool rangedHit,
                bool fromNpc,
                ref LocalHitState __state)
            {
                __state = new LocalHitState();
                if (Instance == null || damage <= 0 || !rangedHit || fromNpc ||
                    player == null || Player.LocalPlayer == null || player != Player.LocalPlayer)
                {
                    return;
                }

                __state.Candidate = true;
                __state.HpBefore = ReadLocalHp(__instance);
            }

            private static void Postfix(Creature __instance, LocalHitState __state)
            {
                if (Instance == null || __state == null || !__state.Candidate || __state.HpBefore <= 0)
                {
                    return;
                }

                int hpAfter = ReadLocalHp(__instance);
                if (hpAfter < __state.HpBefore)
                {
                    Instance.OnLocalHit(hpAfter <= 0);
                }
            }

            private static int ReadLocalHp(Creature creature)
            {
                if (creature == null)
                {
                    return 0;
                }

                try
                {
                    if (LocalHpField != null)
                    {
                        return (int)LocalHpField.GetValue(creature);
                    }
                }
                catch (Exception exception)
                {
                    Log?.LogWarning("Could not read creature local HP: " + exception.Message);
                }

                return creature.Hp;
            }
        }

        [HarmonyPatch(typeof(PlayerVitals), nameof(PlayerVitals.LocalHit))]
        private static class PlayerLocalHitPatch
        {
            private static readonly FieldInfo LocalHpField =
                AccessTools.Field(typeof(PlayerVitals), "_localHp");

            private static void Prefix(
                PlayerVitals __instance,
                Player playerWhoHit,
                int damage,
                bool rangedHit,
                bool fromNpc,
                ref LocalHitState __state)
            {
                __state = new LocalHitState();
                if (Instance == null || damage <= 0 || !rangedHit || fromNpc ||
                    playerWhoHit == null || Player.LocalPlayer == null ||
                    playerWhoHit != Player.LocalPlayer)
                {
                    return;
                }

                __state.Candidate = true;
                __state.HpBefore = ReadLocalHp(__instance);
            }

            private static void Postfix(PlayerVitals __instance, LocalHitState __state)
            {
                if (Instance == null || __state == null || !__state.Candidate || __state.HpBefore <= 0)
                {
                    return;
                }

                int hpAfter = ReadLocalHp(__instance);
                if (hpAfter < __state.HpBefore)
                {
                    Instance.OnLocalHit(hpAfter <= 0);
                }
            }

            private static int ReadLocalHp(PlayerVitals vitals)
            {
                if (vitals == null)
                {
                    return 0;
                }

                try
                {
                    if (LocalHpField != null)
                    {
                        return (int)LocalHpField.GetValue(vitals);
                    }
                }
                catch (Exception exception)
                {
                    Log?.LogWarning("Could not read player local HP: " + exception.Message);
                }

                return vitals.Health;
            }
        }
    }
}
