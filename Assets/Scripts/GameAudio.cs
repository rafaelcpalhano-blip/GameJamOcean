using System.Collections;
using UnityEngine;

namespace GameJamOcean.Audio
{
    public sealed class GameAudio : MonoBehaviour
    {
        private OceanAudioSettings settings;
        private AudioSource ambience, sceneMusic, effects, uiEffects, uiClickEffects, randomAmbience,
            engine, boatTurbo, divePauseAmbience, coinCounting, coinCountingBoost;
        public static GameAudio Instance { get; private set; }
        private Coroutine randomSounds;
        private Coroutine letterSounds;
        private int currentBoatLevel = 1;
        private float engineVolumeMultiplier = 1f;
        private float targetEngineVolumeMultiplier = 1f;
        private float engineMenuVolumeMultiplier = 1f;
        private float targetEngineMenuVolumeMultiplier = 1f;
        private float boatTurboFadeMultiplier = 1f;
        private Coroutine boatTurboFade;
        private Coroutine sceneMusicFade;
        private float sceneMusicFadeMultiplier = 1f;
        public float BackgroundVolume { get; private set; }
        public float EffectsVolume { get; private set; }
        public float PanelOpenDuration => settings != null && settings.panelOpen != null
            ? settings.panelOpen.length : 0f;
        public float DiverWaterJumpDuration => settings != null && settings.diverWaterJump != null
            ? settings.diverWaterJump.length : 0f;
        private void Awake()
        {
            Instance = this;
            settings = Resources.Load<OceanAudioSettings>("OceanAudioSettings");
            ambience = gameObject.AddComponent<AudioSource>();
            sceneMusic = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            uiEffects = gameObject.AddComponent<AudioSource>();
            uiClickEffects = gameObject.AddComponent<AudioSource>();
            randomAmbience = gameObject.AddComponent<AudioSource>();
            engine = gameObject.AddComponent<AudioSource>();
            boatTurbo = gameObject.AddComponent<AudioSource>();
            divePauseAmbience = gameObject.AddComponent<AudioSource>();
            coinCounting = gameObject.AddComponent<AudioSource>();
            coinCountingBoost = gameObject.AddComponent<AudioSource>();
            uiEffects.playOnAwake = uiClickEffects.playOnAwake = randomAmbience.playOnAwake = false;
            uiEffects.spatialBlend = uiClickEffects.spatialBlend = randomAmbience.spatialBlend = 0f;
            ambience.playOnAwake = effects.playOnAwake = false;
            ambience.loop = true;
            sceneMusic.loop = true;
            sceneMusic.playOnAwake = false;
            sceneMusic.spatialBlend = 0f;
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 0f;
            boatTurbo.loop = false;
            boatTurbo.playOnAwake = false;
            boatTurbo.spatialBlend = 0f;
            divePauseAmbience.loop = true;
            divePauseAmbience.playOnAwake = false;
            divePauseAmbience.spatialBlend = 0f;
            divePauseAmbience.ignoreListenerPause = true;
            coinCounting.loop = false;
            coinCounting.playOnAwake = false;
            coinCounting.spatialBlend = 0f;
            coinCountingBoost.loop = false;
            coinCountingBoost.playOnAwake = false;
            coinCountingBoost.spatialBlend = 0f;
            ambience.spatialBlend = effects.spatialBlend = 0f;
            uiEffects.ignoreListenerPause = true; // Only letters bypass pause, never combat audio.
            uiClickEffects.ignoreListenerPause = true;
            SetBackground(PlayerPrefs.GetFloat("GameJamOcean.Settings.Background", 1f));
            SetEffects(PlayerPrefs.GetFloat("GameJamOcean.Settings.Effects", 1f));
        }
        private void Update()
        {
            // Loop is the primary mechanism; this also recovers after WebGL focus/audio-context interruptions.
            if (ambience != null && ambience.clip != null && !ambience.isPlaying && !AudioListener.pause)
                ambience.Play();
            if (sceneMusic != null && sceneMusic.clip != null && !sceneMusic.isPlaying && !AudioListener.pause)
                sceneMusic.Play();
            UpdateEngineVolume();
        }
        public void SetBackground(float value)
        {
            BackgroundVolume = Mathf.Clamp01(value);
            ApplyBackgroundVolumes();
            PlayerPrefs.SetFloat("GameJamOcean.Settings.Background", BackgroundVolume);
        }
        private void ApplyBackgroundVolumes()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            float ambienceVolume = settings == null ? 1f : scene == "DiveScene"
                ? settings.diveAmbienceVolume : settings.oceanAmbienceVolume;
            ambience.volume = BackgroundVolume * ambienceVolume;
            float musicVolume = settings == null ? 1f : scene == "DiveScene"
                ? settings.diveMusicVolume : settings.oceanMusicVolume;
            sceneMusic.volume = BackgroundVolume * musicVolume * sceneMusicFadeMultiplier;
            randomAmbience.volume = BackgroundVolume * (settings != null ? settings.randomAmbienceGain : 1f);
        }
        public void SetEffects(float value)
        {
            EffectsVolume = Mathf.Clamp01(value);
            effects.volume = EffectsVolume;
            uiEffects.volume = EffectsVolume;
            uiClickEffects.volume = EffectsVolume;
            RefreshCoinCountingVolume();
            RefreshEngineVolume();
            RefreshBoatTurboVolume();
            if (divePauseAmbience != null && divePauseAmbience.isPlaying && settings != null)
                divePauseAmbience.volume = EffectsVolume * settings.divePauseAmbienceVolume;
            PlayerPrefs.SetFloat("GameJamOcean.Settings.Effects", EffectsVolume);
        }
        public void SetScene(string scene)
        {
            if (randomSounds != null) StopCoroutine(randomSounds);
            randomSounds = null;
            randomAmbience.Stop();
            effects.Stop();
            coinCounting.Stop();
            coinCountingBoost.Stop();
            StopBoatTurboImmediately();
            SetDivePauseAmbience(false);
            if (scene != "OceanScene_3D") StopBoatEngine();
            if (settings == null) { ambience.Stop(); return; }
            AudioClip next = scene == "OceanScene_3D" ? settings.oceanAmbience
                : scene == "DiveScene" ? settings.diveAmbience : null;
            AudioClip nextMusic = scene == "OceanScene_3D" ? settings.oceanMusic
                : scene == "DiveScene" ? settings.diveMusic : null;
            if (next != ambience.clip) { ambience.Stop(); ambience.clip = next; }
            if (nextMusic != sceneMusic.clip) { sceneMusic.Stop(); sceneMusic.clip = nextMusic; }
            if (sceneMusicFade != null) StopCoroutine(sceneMusicFade);
            sceneMusicFade = null;
            sceneMusicFadeMultiplier = nextMusic != null ? 0f : 1f;
            SetBackground(BackgroundVolume);
            if (next != null && !ambience.isPlaying) ambience.Play();
            if (nextMusic != null)
            {
                if (!sceneMusic.isPlaying) sceneMusic.Play();
                sceneMusicFade = StartCoroutine(FadeInSceneMusic(2f));
            }
            if (next == null && nextMusic == null) return;
            if (scene == "OceanScene_3D")
            {
                int boatLevel = GameJamOcean.Progression.GameProgress.HasInstance
                    ? GameJamOcean.Progression.GameProgress.Instance.SelectedBoatLevel : 1;
                PlayBoatEngine(boatLevel);
            }
            if (scene == "DiveScene" && settings.diveRandomAmbience != null)
                randomSounds = StartCoroutine(RandomDiveSounds());
        }
        private IEnumerator FadeInSceneMusic(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                sceneMusicFadeMultiplier = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
                ApplyBackgroundVolumes();
                yield return null;
            }
            sceneMusicFadeMultiplier = 1f;
            ApplyBackgroundVolumes();
            sceneMusicFade = null;
        }
        public void EnsureAmbiencePlaying()
        {
            if (ambience.clip != null && !ambience.isPlaying) ambience.Play();
            if (sceneMusic.clip != null && !sceneMusic.isPlaying) sceneMusic.Play();
        }
        private IEnumerator RandomDiveSounds()
        {
            while (true)
            {
                float min = Mathf.Max(.1f, settings.randomMinimumInterval);
                float max = Mathf.Max(min, settings.randomMaximumInterval);
                yield return new WaitForSeconds(Random.Range(min, max));
                randomAmbience.clip = settings.diveRandomAmbience;
                randomAmbience.Play();
                yield return new WaitForSeconds(settings.diveRandomAmbience.length);
            }
        }
        public void PlayHarpoon()
        {
            if (settings != null && settings.harpoonShot != null)
                effects.PlayOneShot(settings.harpoonShot, 0.15f);
        }
        public void PlayCoinReward()
        {
            if (settings != null && settings.coinReward != null)
                effects.PlayOneShot(settings.coinReward);
        }
        public void PlayCoinCollected() => PlayEffect(settings?.coinCollected,
            settings != null ? settings.coinCollectedVolume : 1f);
        public void StartCoinCounting()
        {
            if (settings == null || coinCounting == null || settings.coinCounting == null) return;
            coinCounting.Stop();
            coinCounting.clip = settings.coinCounting;
            coinCountingBoost.clip = settings.coinCounting;
            coinCounting.loop = false;
            coinCountingBoost.loop = false;
            float pitch = Mathf.Clamp(settings.coinCounting.length
                / Mathf.Max(.1f, settings.coinCountingTargetDuration), .1f, 3f);
            coinCounting.pitch = pitch;
            coinCountingBoost.pitch = pitch;
            RefreshCoinCountingVolume();
            coinCounting.Play();
            if (coinCountingBoost.volume > 0f) coinCountingBoost.Play();
        }
        public void StopCoinCounting()
        {
            if (coinCounting != null) coinCounting.Stop();
            if (coinCountingBoost != null) coinCountingBoost.Stop();
        }
        private void RefreshCoinCountingVolume()
        {
            if (settings == null) return;
            float configuredVolume = EffectsVolume * settings.coinCountingVolume
                * settings.coinCountingGainMultiplier;
            if (coinCounting != null) coinCounting.volume = Mathf.Clamp01(configuredVolume);
            if (coinCountingBoost != null) coinCountingBoost.volume = Mathf.Clamp01(configuredVolume - 1f);
        }
        public void PlayPowerUpCollected() => PlayEffect(settings?.powerUpCollected,
            settings != null ? settings.powerUpCollectedVolume : 1f);
        private void OnDestroy() { if (Instance == this) Instance = null; }
        // Route future obstacle/dive effects through this source to respect the effects slider.
        public void PlayEffect(AudioClip clip) => PlayEffect(clip, 1f);
        public void PlayEffect(AudioClip clip, float volumeScale)
        {
            if (clip != null) effects.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 2f));
        }
        public void PlayUIButtonHover(AudioClip clip, float volumeScale)
        {
            if (clip == null || uiEffects == null) return;
            uiEffects.Stop();
            uiEffects.clip = clip;
            uiEffects.volume = EffectsVolume * Mathf.Clamp(volumeScale, 0f, 1f);
            uiEffects.Play();
        }
        public void PlayUIButtonClick(AudioClip clip, float volumeScale)
        {
            if (clip == null || uiClickEffects == null) return;
            uiClickEffects.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1f));
        }
        public void PlayChestOpen(bool bigChest) => PlayEffect(settings?.chestOpen,
            settings == null ? 1f : settings.chestOpenVolume
                * (bigChest ? settings.bigChestVolumeMultiplier : 1f));
        public void PlayAquaticDash(bool enemy) => PlayEffect(settings?.aquaticDash,
            settings == null ? 1f : settings.aquaticDashVolume
                * (enemy ? settings.enemyDashVolumeMultiplier : 1f));
        public void PlayVillageConstruction() => PlayEffect(settings?.villageConstruction,
            settings != null ? settings.villageConstructionVolume : 1f);
        public void StartBoatTurbo()
        {
            if (settings == null || boatTurbo == null || settings.boatTurbo == null) return;
            if (boatTurboFade != null) StopCoroutine(boatTurboFade);
            boatTurboFade = null;
            boatTurboFadeMultiplier = 1f;
            boatTurbo.Stop();
            boatTurbo.clip = settings.boatTurbo;
            RefreshBoatTurboVolume();
            boatTurbo.Play();
        }
        public void StopBoatTurbo()
        {
            if (boatTurbo == null || !boatTurbo.isPlaying) return;
            if (boatTurboFade != null) StopCoroutine(boatTurboFade);
            boatTurboFade = StartCoroutine(FadeBoatTurboOut());
        }
        private void StopBoatTurboImmediately()
        {
            if (boatTurboFade != null) StopCoroutine(boatTurboFade);
            boatTurboFade = null;
            boatTurboFadeMultiplier = 1f;
            if (boatTurbo != null) boatTurbo.Stop();
            RefreshBoatTurboVolume();
        }
        private IEnumerator FadeBoatTurboOut()
        {
            float duration = settings != null ? settings.boatTurboFadeOutSeconds : .18f;
            float elapsed = 0f;
            while (boatTurbo != null && boatTurbo.isPlaying && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                boatTurboFadeMultiplier = 1f - Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
                RefreshBoatTurboVolume();
                yield return null;
            }
            if (boatTurbo != null) boatTurbo.Stop();
            boatTurboFadeMultiplier = 1f;
            RefreshBoatTurboVolume();
            boatTurboFade = null;
        }
        private void RefreshBoatTurboVolume()
        {
            if (boatTurbo != null)
                boatTurbo.volume = EffectsVolume * (settings != null ? settings.boatTurboVolume : .1f)
                    * boatTurboFadeMultiplier;
        }
        public void PlayBoatUpgrade() => PlayEffect(settings?.boatUpgrade,
            settings != null ? settings.boatUpgradeVolume : 1f);
        public void PlayDiverWaterJump() => PlayEffect(settings?.diverWaterJump,
            settings != null ? settings.diverWaterJumpVolume : 1f);
        public void PlayBoatCollision(float volumeMultiplier = 1f) => PlayEffect(settings?.boatCollision,
            (settings != null ? settings.boatCollisionVolume : 1f) * Mathf.Max(0f, volumeMultiplier));

        public void PlayBoatEngine(int boatLevel)
        {
            if (settings == null || engine == null) return;
            currentBoatLevel = Mathf.Clamp(boatLevel, 1, 3);
            AudioClip clip = boatLevel == 2 ? settings.boat2Engine
                : boatLevel == 3 ? settings.boat3Engine : settings.boat1Engine;
            if (engine.clip != clip)
            {
                engine.Stop();
                engine.clip = clip;
            }
            RefreshEngineVolume(boatLevel);
            if (clip != null && !engine.isPlaying) engine.Play();
        }
        public void SetBoatEngineState(bool accelerating, bool turboActive)
        {
            targetEngineVolumeMultiplier = turboActive ? 1.5f : accelerating ? 1.35f : 1f;
            if (turboActive)
            {
                engineVolumeMultiplier = 1.5f;
                RefreshEngineVolume(currentBoatLevel);
            }
        }
        public void SetBoatEngineTemporarilyMuted(bool muted)
        {
            targetEngineMenuVolumeMultiplier = muted ? 0f : 1f;
        }
        public void SetDivePauseAmbience(bool playing)
        {
            if (divePauseAmbience == null) return;
            if (!playing || settings == null || settings.divePauseAmbience == null)
            {
                divePauseAmbience.Stop();
                return;
            }
            divePauseAmbience.clip = settings.divePauseAmbience;
            divePauseAmbience.volume = EffectsVolume * settings.divePauseAmbienceVolume;
            if (!divePauseAmbience.isPlaying) divePauseAmbience.Play();
        }
        public void StopBoatEngine()
        {
            engineVolumeMultiplier = targetEngineVolumeMultiplier = 1f;
            engineMenuVolumeMultiplier = targetEngineMenuVolumeMultiplier = 1f;
            if (engine != null) { engine.Stop(); engine.clip = null; }
        }
        private void UpdateEngineVolume()
        {
            if (engine == null) return;
            float speed = settings != null ? settings.engineVolumeTransitionSpeed : 3f;
            float muteSpeed = settings != null ? settings.engineMenuMuteTransitionSpeed : 8f;
            float previousEngine = engineVolumeMultiplier;
            float previousMenu = engineMenuVolumeMultiplier;
            engineVolumeMultiplier = Mathf.MoveTowards(engineVolumeMultiplier, targetEngineVolumeMultiplier,
                Mathf.Max(.1f, speed) * Time.unscaledDeltaTime);
            engineMenuVolumeMultiplier = Mathf.MoveTowards(engineMenuVolumeMultiplier,
                targetEngineMenuVolumeMultiplier, Mathf.Max(.1f, muteSpeed) * Time.unscaledDeltaTime);
            if (!Mathf.Approximately(previousEngine, engineVolumeMultiplier)
                || !Mathf.Approximately(previousMenu, engineMenuVolumeMultiplier))
                RefreshEngineVolume(currentBoatLevel);
        }
        private void RefreshEngineVolume(int boatLevel = 0)
        {
            if (settings == null || engine == null) return;
            if (boatLevel == 0 && GameJamOcean.Progression.GameProgress.HasInstance)
                boatLevel = GameJamOcean.Progression.GameProgress.Instance.SelectedBoatLevel;
            float individual = boatLevel == 2 ? settings.boat2EngineVolume
                : boatLevel == 3 ? settings.boat3EngineVolume : settings.boat1EngineVolume;
            engine.volume = EffectsVolume * individual * engineVolumeMultiplier * engineMenuVolumeMultiplier;
        }
        public void StartLetter()
        {
            StopLetter();
            letterSounds = StartCoroutine(LetterSequence());
        }
        private IEnumerator LetterSequence()
        {
            if (settings == null) yield break;
            if (settings.panelOpen != null)
            {
                uiEffects.PlayOneShot(settings.panelOpen);
                yield return new WaitForSecondsRealtime(settings.panelOpen.length);
            }
            for (int i = 0; i < 2; i++)
            {
                if (settings.writing == null) break;
                uiEffects.PlayOneShot(settings.writing);
                yield return new WaitForSecondsRealtime(settings.writing.length);
            }
            letterSounds = null;
        }
        public void StopLetter()
        {
            if (letterSounds != null) StopCoroutine(letterSounds);
            letterSounds = null;
            if (uiEffects != null) uiEffects.Stop();
        }
    }
}
