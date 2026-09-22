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

namespace AlbatrossWolfHit
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.wjy13.albatrosswolfhit";
        public const string PluginName = "Albatross Wolf Hit";
        public const string PluginVersion = "0.1.0";

        private const string AudioFileName = "howl_bgm.ogg";
        private const string ImageFileName = "howl_flash.png";
        private const byte BowheadLavaProjectileType = 2;

        private Harmony _harmony;
        private GameObject _effectRoot;
        private RawImage _flashImage;
        private Texture2D _wolfTexture;
        private AudioSource _audioSource;
        private AudioClip _audioClip;
        private Coroutine _flashCoroutine;
        private bool _audioLoading;
        private bool _playWhenLoaded;
        private int _triggerCount;
        private float _lastTriggerTime = float.NegativeInfinity;

        private ConfigEntry<bool> _enabled;
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
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Waiting for albatross projectiles to hit the local player.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();

            if (_audioClip != null)
            {
                Destroy(_audioClip);
            }

            if (_wolfTexture != null)
            {
                Destroy(_wolfTexture);
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
                "Enable the albatross-only wolf image and Animals effect.");

            _flashEnabled = Config.Bind("Visual", "FlashEnabled", true,
                "Show the full-screen wolf image when an albatross projectile hits the local player.");
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
                new ConfigDescription("Maximum image shake in pixels.",
                    new AcceptableValueRange<float>(0f, 100f)));

            _audioEnabled = Config.Bind("Audio", "AudioEnabled", true,
                "Restart Animals whenever an albatross projectile hits the local player.");
            _audioVolume = Config.Bind("Audio", "Volume", 1f,
                new ConfigDescription("Animals playback volume.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _speedMultiplier = Config.Bind("Audio", "SpeedMultiplierPerHit", 1.1f,
                new ConfigDescription("Each consecutive albatross hit multiplies playback speed by this value.",
                    new AcceptableValueRange<float>(1f, 2f)));
            _maximumSpeed = Config.Bind("Audio", "MaximumSpeed", 2f,
                new ConfigDescription("Maximum Animals playback speed.",
                    new AcceptableValueRange<float>(1f, 3f)));
            _resetAfterSeconds = Config.Bind("Audio", "ResetAfterSeconds", 15f,
                new ConfigDescription("Reset the speed chain after this many seconds without an albatross hit. Set to 0 to never reset until the game closes.",
                    new AcceptableValueRange<float>(0f, 600f)));
        }

        private void CreateEffectObjects()
        {
            _effectRoot = new GameObject("AlbatrossWolfHit_Effects");
            DontDestroyOnLoad(_effectRoot);

            _audioSource = _effectRoot.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.ignoreListenerPause = true;

            GameObject canvasObject = new GameObject("AlbatrossWolfHit_Canvas");
            canvasObject.transform.SetParent(_effectRoot.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            GameObject imageObject = new GameObject("AlbatrossWolfHit_Image");
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
                _wolfTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "AlbatrossWolfHit_Texture"
                };
                if (!ImageConversion.LoadImage(_wolfTexture, File.ReadAllBytes(imagePath), false))
                {
                    Destroy(_wolfTexture);
                    _wolfTexture = null;
                    Logger.LogWarning("Unity could not decode the wolf image: " + imagePath);
                    return;
                }

                _flashImage.texture = _wolfTexture;
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
                _audioClip.name = "AlbatrossWolfHit_Animals";
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

        internal void TriggerAlbatrossHit()
        {
            if (!_enabled.Value)
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

            Logger.LogDebug($"Albatross projectile hit triggered wolf effect at {speed:0.00}x.");
        }

        private float CurrentSpeed()
        {
            return Mathf.Min(
                Mathf.Pow(_speedMultiplier.Value, Math.Max(0, _triggerCount - 1)),
                _maximumSpeed.Value);
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

        private static bool IsAlbatrossProjectile(Projectile projectile)
        {
            if (projectile == null || !projectile.FromNpc ||
                projectile.TypeId == BowheadLavaProjectileType)
            {
                return false;
            }

            return AlbatrossProjectileClassifier.MatchesActiveAlbatross(projectile.TypeId);
        }

        private static class AlbatrossProjectileClassifier
        {
            private static readonly FieldInfo PoopInfoField =
                AccessTools.Field(typeof(Albatross), "_poopInfo");

            internal static bool MatchesActiveAlbatross(byte projectileType)
            {
                try
                {
                    Albatross[] albatrosses = UnityEngine.Object.FindObjectsByType<Albatross>();
                    foreach (Albatross albatross in albatrosses)
                    {
                        WeaponInfo info = PoopInfoField?.GetValue(albatross) as WeaponInfo;
                        if (info != null && info.ProjectileType == projectileType)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log?.LogWarning("Could not inspect active albatross projectile type: " + exception.Message);
                }

                // In the current game build, the only other FromNpc projectile is
                // Bowhead Whale lava (type 2), which is rejected above. Keeping this
                // fallback handles the rare case where the bird despawns before impact.
                return true;
            }
        }

        [HarmonyPatch]
        private static class ProjectileHitPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(ProjectileManager),
                    "Hit",
                    new[] { typeof(Projectile), typeof(ProjectileType), typeof(RaycastHit) });
            }

            private static void Prefix(Projectile projectile, RaycastHit hit)
            {
                if (Instance == null || !IsAlbatrossProjectile(projectile) || hit.transform == null)
                {
                    return;
                }

                Player hitPlayer = PlayerManager.GetPlayerFromBodyPart(hit.transform);
                if (hitPlayer != null && Player.LocalPlayer != null && hitPlayer == Player.LocalPlayer)
                {
                    Instance.TriggerAlbatrossHit();
                }
            }
        }
    }
}
