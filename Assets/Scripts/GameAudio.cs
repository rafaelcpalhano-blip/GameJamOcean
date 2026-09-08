using System.Collections;
using UnityEngine;

namespace GameJamOcean.Audio
{
    public sealed class GameAudio : MonoBehaviour
    {
        private OceanAudioSettings settings;
        private AudioSource ambience, effects, uiEffects, randomAmbience, engine, boatTurbo, divePauseAmbience;
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
            effects = gameObject.AddComponent<AudioSource>();
            uiEffects = gameObject.AddComponent<AudioSource>();
            randomAmbience = gameObject.AddComponent<AudioSource>();
            engine = gameObject.AddComponent<AudioSource>();
            boatTurbo = gameObject.AddComponent<AudioSource>();
            divePauseAmbience = gameObject.AddComponent<AudioSource>();
            uiEffects.playOnAwake = randomAmbience.playOnAwake = false;
            uiEffects.spatialBlend = randomAmbience.spatialBlend = 0f;
            ambience.playOnAwake = effects.playOnAwake = false;
            ambience.loop = true;
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
            ambience.spatialBlend = effects.spatialBlend = 0f;
            uiEffects.ignoreListenerPause = true; // Only letters bypass pause, never combat audio.
            SetBackground(PlayerPrefs.GetFloat("GameJamOcean.Settings.Background", 1f));
            SetEffects(PlayerPrefs.GetFloat("GameJamOcean.Settings.Effects", 1f));
        }
        private void Update()
        {
            // Loop is the primary mechanism; this also recovers after WebGL focus/audio-context interruptions.
            if (ambience != null && ambience.clip != null && !ambience.isPlaying && !AudioListener.pause)
                ambience.Play();
            UpdateEngineVolume();
        }
        public void SetBackground(float value)
        {
            BackgroundVolume = Mathf.Clamp01(value);
            ambience.volume = BackgroundVolume * (settings != null ? settings.ambienceGain : 1f);
            randomAmbience.volume = BackgroundVolume * (settings != null ? settings.randomAmbienceGain : 1f);
            PlayerPrefs.SetFloat("GameJamOcean.Settings.Background", BackgroundVolume);
        }
        public void SetEffects(float value)
        {
            EffectsVolume = Mathf.Clamp01(value);
            effects.volume = EffectsVolume;
            uiEffects.volume = EffectsVolume;
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
            StopBoatTurboImmediately();
            SetDivePauseAmbience(false);
            if (scene != "OceanScene_3D") StopBoatEngine();
            if (settings == null) { ambience.Stop(); return; }
            AudioClip next = scene == "OceanScene_3D" ? settings.oceanAmbience
                : scene == "DiveScene" ? settings.diveAmbience : null;
            if (next != ambience.clip) { ambience.Stop(); ambience.clip = next; }
            if (next == null) return;
            if (!ambience.isPlaying) ambience.Play();
            if (scene == "OceanScene_3D")
            {
                int boatLevel = GameJamOcean.Progression.GameProgress.HasInstance
                    ? GameJamOcean.Progression.GameProgress.Instance.SelectedBoatLevel : 1;
                PlayBoatEngine(boatLevel);
            }
            if (scene == "DiveScene" && settings.diveRandomAmbience != null)
                randomSounds = StartCoroutine(RandomDiveSounds());
        }
        public void EnsureAmbiencePlaying()
        {
            if (ambience.clip != null && !ambience.isPlaying) ambience.Play();
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
        public void PlayPowerUpCollected() => PlayEffect(settings?.powerUpCollected,
            settings != null ? settings.powerUpCollectedVolume : 1f);
        private void OnDestroy() { if (Instance == this) Instance = null; }
        // Route future obstacle/dive effects through this source to respect the effects slider.
        public void PlayEffect(AudioClip clip) => PlayEffect(clip, 1f);
        public void PlayEffect(AudioClip clip, float volumeScale)
        {
            if (clip != null) effects.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 2f));
        }
        public void PlayUIEffect(AudioClip clip, float volumeScale)
        {
            if (clip == null || uiEffects == null) return;
            uiEffects.Stop();
            uiEffects.clip = clip;
            uiEffects.volume = EffectsVolume * Mathf.Clamp(volumeScale, 0f, 1f);
            uiEffects.Play();
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
        public void PlayBoatCollision() => PlayEffect(settings?.boatCollision,
            settings != null ? settings.boatCollisionVolume : 1f);

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
