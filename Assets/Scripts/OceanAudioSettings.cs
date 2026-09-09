using System;
using UnityEngine;

namespace GameJamOcean.Audio
{
    [Serializable]
    public sealed class TornadoRoute3D
    {
        [Tooltip("Exactly three positions normalized inside the navigable ocean bounds.")]
        public Vector2[] normalizedPoints = new Vector2[3];
    }

    [CreateAssetMenu(menuName = "GameJamOcean/Audio Settings")]
    public sealed class OceanAudioSettings : ScriptableObject
    {
        [Header("Scene Ambience and Music")]
        public AudioClip oceanAmbience;
        [Range(0f, 1f)] public float oceanAmbienceVolume = .65f;
        public AudioClip oceanMusic;
        [Range(0f, 1f)] public float oceanMusicVolume = .5f;
        public AudioClip diveMusic;
        [Range(0f, 1f)] public float diveMusicVolume = .5f;
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
        public AudioClip coinCollected;
        [Range(0f, 2f)] public float coinCollectedVolume = 1f;
        public AudioClip coinCounting;
        [Range(0f, 2f)] public float coinCountingVolume = 1f;
        [Range(0f, 4f)] public float coinCountingGainMultiplier = 2f;
        [Min(.1f)] public float coinCountingTargetDuration = 2f;
        [Header("DiveScene Effects")]
        public AudioClip powerUpCollected;
        [Range(0f, 2f)] public float powerUpCollectedVolume = 1f;
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
        [Range(0f, 1f)] public float diveAmbienceVolume = .65f;

        [Header("Ocean VFX — Boat collision")]
        public GameObject boatCollisionEffect;
        [Min(.1f)] public float boatCollisionEffectLifetime = 2.5f;
        [Min(.01f)] public float boatCollisionEffectScale = 1f;
        public float boatCollisionWaterOffset;

        [Header("Ocean VFX — Tornado obstacle")]
        public GameObject tornadoPrefab;
        public AudioClip tornadoSound;
        [Range(0f, 2f)] public float tornadoSoundVolume = 1.1f;
        [Min(.1f)] public float tornadoSoundMinimumDistance = 12f;
        [Min(.1f)] public float tornadoSoundMaximumDistance = 45f;
        [Min(0f)] public float tornadoIslandClearance = 4f;
        [Min(1)] public int tornadoCount = 6;
        [Min(1)] public int tornadoMinimumActive = 3;
        [Min(.1f)] public float tornadoSpeed = 2f;
        [Min(0f)] public float tornadoZigzagAmplitude = 1.25f;
        [Min(.1f)] public float tornadoZigzagCycles = 1.5f;
        [Min(.01f)] public float tornadoVisualScale = 2.3f;
        [Min(.1f)] public float tornadoColliderRadius = 1.25f;
        [Min(.1f)] public float tornadoPullRadius = 8f;
        [Min(0f)] public float tornadoPullAcceleration = 1.25f;
        [Min(.1f)] public float tornadoSpiralCaptureRadius = 3f;
        [Min(.1f)] public float tornadoCenterRadius = 1.5f;
        [Min(.1f)] public float tornadoSpiralDuration = 1.75f;
        [Min(0f)] public float tornadoLaunchSpeed = 9f;
        [Min(0f)] public float tornadoLaunchControlLockSeconds = .6f;
        [Header("Ocean VFX — Tornado and dive buoy")]
        [Min(.1f)] public float tornadoBuoyCaptureRadius = 2.5f;
        [Min(.1f)] public float tornadoBuoySpiralDuration = 1.5f;
        [Min(0f)] public float tornadoBuoyLaunchSpeed = 8f;
        [Min(0f)] public float tornadoBuoyLaunchUpSpeed = 7f;
        [Min(.1f)] public float tornadoBuoyDestructionDelay = 2.5f;
        [Min(.1f)] public float tornadoBuoyTargetMinimumInterval = 12f;
        [Min(.1f)] public float tornadoBuoyTargetMaximumInterval = 20f;
        [Min(.1f)] public float tornadoBuoyTargetMaximumTravelTime = 8f;
        [Min(0f)] public float tornadoBoat1Damage = 20f;
        [Min(0f)] public float tornadoBoat2Damage = 15f;
        [Min(0f)] public float tornadoBoat3Damage = 10f;
        [Min(.1f)] public float tornadoCenterCooldown = 1.25f;
        public float tornadoWaterOffset = -1.5f;
        [Min(0f)] public float tornadoMinimumHiddenSeconds = 10f;
        [Min(0f)] public float tornadoMaximumHiddenSeconds = 20f;
        [Min(0f)] public float tornadoInitialDelayMaximum = 20f;
        [Min(.05f)] public float tornadoDisappearanceFadeSeconds = .6f;
        public TornadoRoute3D[] tornadoRoutes;

        [Header("Ocean enemies — Shark")]
        public AudioClip sharkDrama;
        [Range(0f, 2f)] public float sharkDramaVolume = .7f;
        [Min(0f)] public float sharkDramaFadeOutSeconds = .5f;
        [Min(.05f)] public float sharkDramaEscapeFadeOutSeconds = .2f;
    }
}
