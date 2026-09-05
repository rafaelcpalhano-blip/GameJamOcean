using System.Collections;
using UnityEngine;

namespace GameJamOcean.Audio
{
    public sealed class GameAudio : MonoBehaviour
    {
        private OceanAudioSettings settings;
        private AudioSource ambience, effects, uiEffects, randomAmbience;
        public static GameAudio Instance { get; private set; }
        private Coroutine randomSounds;
        private Coroutine letterSounds;
        public float BackgroundVolume { get; private set; }
        public float EffectsVolume { get; private set; }
        public float PanelOpenDuration => settings != null && settings.panelOpen != null
            ? settings.panelOpen.length : 0f;
        private void Awake()
        {
            Instance = this;
            settings = Resources.Load<OceanAudioSettings>("OceanAudioSettings");
            ambience = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            uiEffects = gameObject.AddComponent<AudioSource>();
            randomAmbience = gameObject.AddComponent<AudioSource>();
            uiEffects.playOnAwake = randomAmbience.playOnAwake = false;
            uiEffects.spatialBlend = randomAmbience.spatialBlend = 0f;
            ambience.playOnAwake = effects.playOnAwake = false;
            ambience.loop = true;
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
            PlayerPrefs.SetFloat("GameJamOcean.Settings.Effects", EffectsVolume);
        }
        public void SetScene(string scene)
        {
            if (randomSounds != null) StopCoroutine(randomSounds);
            randomSounds = null;
            randomAmbience.Stop();
            effects.Stop();
            if (settings == null) { ambience.Stop(); return; }
            AudioClip next = scene == "OceanScene_3D" ? settings.oceanAmbience
                : scene == "DiveScene" ? settings.diveAmbience : null;
            if (next != ambience.clip) { ambience.Stop(); ambience.clip = next; }
            if (next == null) return;
            if (!ambience.isPlaying) ambience.Play();
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
        private void OnDestroy() { if (Instance == this) Instance = null; }
        // Route future obstacle/dive effects through this source to respect the effects slider.
        public void PlayEffect(AudioClip clip) { if (clip != null) effects.PlayOneShot(clip); }
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
