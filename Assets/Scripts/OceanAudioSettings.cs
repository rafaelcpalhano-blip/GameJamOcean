using UnityEngine;

namespace GameJamOcean.Audio
{
    [CreateAssetMenu(menuName = "GameJamOcean/Audio Settings")]
    public sealed class OceanAudioSettings : ScriptableObject
    {
        public AudioClip oceanAmbience;
        public AudioClip panelOpen;
        public AudioClip writing;
        [Header("Diving")]
        public AudioClip harpoonShot;
        public AudioClip diveAmbience;
        public AudioClip diveRandomAmbience;
        [Min(.1f)] public float randomMinimumInterval = 2f;
        [Min(.1f)] public float randomMaximumInterval = 5f;
        [Range(0f, 1f)] public float randomAmbienceGain = .6f;
        [Range(0f, 1f)] public float ambienceGain = .65f;
    }
}
