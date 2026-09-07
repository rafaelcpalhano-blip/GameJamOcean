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
        [Tooltip("Low looping ambience used only while the DiveScene pause menu is open.")]
        public AudioClip divePauseAmbience;
        [Range(0f, 1f)] public float divePauseAmbienceVolume = .12f;
        public AudioClip coinReward;
        [Header("DiveScene Effects")]
        public AudioClip chestOpen;
        [Range(0f, 2f)] public float chestOpenVolume = 1f;
        [Range(0f, 2f)] public float bigChestVolumeMultiplier = 1.2f;
        public AudioClip aquaticDash;
        [Range(0f, 2f)] public float aquaticDashVolume = 1.2f;
        [Range(0f, 2f)] public float enemyDashVolumeMultiplier = .65f;

        [Header("OceanScene Effects")]
        public AudioClip villageConstruction;
        [Range(0f, 2f)] public float villageConstructionVolume = 1f;
        public AudioClip boatTurbo;
        [Range(0f, 2f)] public float boatTurboVolume = .1f;
        [Tooltip("Short unscaled fade used when the boat turbo is released.")]
        [Min(.01f)] public float boatTurboFadeOutSeconds = .18f;
        public AudioClip boatUpgrade;
        [Range(0f, 2f)] public float boatUpgradeVolume = 1f;
        public AudioClip diverWaterJump;
        [Range(0f, 2f)] public float diverWaterJumpVolume = 1f;
        public AudioClip boatCollision;
        [Range(0f, 2f)] public float boatCollisionVolume = 1f;

        [Header("Boat Engines")]
        public AudioClip boat1Engine;
        [Range(0f, 2f)] public float boat1EngineVolume = .15f;
        public AudioClip boat2Engine;
        [Range(0f, 2f)] public float boat2EngineVolume = .15f;
        public AudioClip boat3Engine;
        [Range(0f, 2f)] public float boat3EngineVolume = .15f;
        [Tooltip("How quickly the engine returns between its base and accelerating volume.")]
        [Min(.1f)] public float engineVolumeTransitionSpeed = 3f;
        [Tooltip("How quickly the upgrade menu fades the engine in and out.")]
        [Min(.1f)] public float engineMenuMuteTransitionSpeed = 8f;
        [Min(.1f)] public float randomMinimumInterval = 2f;
        [Min(.1f)] public float randomMaximumInterval = 5f;
        [Range(0f, 1f)] public float randomAmbienceGain = .6f;
        [Range(0f, 1f)] public float ambienceGain = .65f;
    }
}
