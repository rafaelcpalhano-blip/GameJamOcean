using System;
using GameJamOcean.Weapons;
using UnityEngine;

namespace GameJamOcean.Progression
{
    public enum UpgradeKind { Island, BoatHull, BoatTurbo, DiverHealth, DiverSpeed, Harpoon }

    [Serializable]
    public sealed class UpgradeTier
    {
        [Min(0)] public int goldCost;
        [Tooltip("Vida absoluta; bônus percentual sobre a base para casco, turbo e velocidade.")]
        public float value;
        public HarpoonProjectile2D harpoonPrefab;
        public UpgradeTier(int cost, float amount) { goldCost = cost; value = amount; }
    }

    [Serializable]
    public sealed class UpgradeDefinition
    {
        public UpgradeKind kind;
        public string displayName;
        public UpgradeTier level1 = new(0, 0);
        public UpgradeTier level2 = new(50, 15);
        public UpgradeTier level3 = new(100, 30);
        [Tooltip("Usado pela Ilha/Aldeia e pelo Arpão no nível 4.")]
        public UpgradeTier islandLevel4 = new(900, 0);
        public int MaximumLevel => kind == UpgradeKind.Island || kind == UpgradeKind.Harpoon ? 4 : 3;
        public UpgradeDefinition(UpgradeKind id, string title, float v1, float v2, float v3, int c2, int c3)
        {
            kind = id; displayName = title;
            level1 = new(0, v1); level2 = new(c2, v2); level3 = new(c3, v3);
        }
        public UpgradeTier Tier(int level) => level == 1 ? level1 : level == 2 ? level2 : level == 3 ? level3 : islandLevel4;
    }

    [CreateAssetMenu(menuName = "GameJamOcean/Upgrade Catalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        public UpgradeDefinition[] upgrades =
        {
            new(UpgradeKind.Island, "Ilha / Aldeia", 0, 0, 0, 200, 500),
            new(UpgradeKind.BoatHull, "Barco — Casco", 0, 15, 30, 60, 120),
            new(UpgradeKind.BoatTurbo, "Barco — Duração do turbo", 0, 20, 40, 70, 140),
            new(UpgradeKind.DiverHealth, "Mergulhador — Vida", 5, 7, 9, 70, 140),
            new(UpgradeKind.DiverSpeed, "Mergulhador — Velocidade", 0, 15, 30, 60, 120),
            new(UpgradeKind.Harpoon, "Mergulhador — Arpão", 0, 0, 0, 80, 160)
        };

        public UpgradeDefinition Find(UpgradeKind kind)
        {
            if (upgrades != null)
                foreach (var definition in upgrades)
                    if (definition != null && definition.kind == kind) return definition;
            return null;
        }

        public bool Validate(out string error)
        {
            for (int id = 0; id < 6; id++)
            {
                UpgradeDefinition definition = null;
                int count = 0;
                if (upgrades != null) foreach (var entry in upgrades)
                    if (entry != null && (int)entry.kind == id) { count++; definition = entry; }
                if (count != 1) { error = $"Catálogo: tipo {(UpgradeKind)id} ausente ou duplicado."; return false; }
                for (int level = 1; level <= definition.MaximumLevel; level++)
                {
                    var tier = definition.Tier(level);
                    if (tier == null || tier.goldCost < 0 || float.IsNaN(tier.value) || float.IsInfinity(tier.value)
                        || tier.value < 0 || (definition.kind == UpgradeKind.DiverHealth && tier.value < 1))
                    { error = $"Configuração inválida: {definition.displayName}, N{level}."; return false; }
                    if (definition.kind == UpgradeKind.Harpoon &&
                        (tier.harpoonPrefab == null || !tier.harpoonPrefab.gameObject.activeSelf
                         || !tier.harpoonPrefab.enabled || tier.harpoonPrefab.GetComponent<Rigidbody2D>() == null
                         || tier.harpoonPrefab.GetComponent<Collider2D>() == null))
                    { error = $"Arpão N{level}: atribua um prefab ativo com projétil, Rigidbody2D e Collider2D."; return false; }
                }
            }
            error = null; return true;
        }
    }
}
