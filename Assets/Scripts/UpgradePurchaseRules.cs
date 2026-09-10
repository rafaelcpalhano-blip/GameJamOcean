namespace GameJamOcean.Progression
{
    // Pure transaction rules: no UI, scene or Unity dependencies.
    public static class UpgradePurchaseRules
    {
        public static bool TryApply(ref int gold, ref int level, int expectedLevel,
            int maximumLevel, int cost, out string message)
        {
            if (level < 0 || level >= maximumLevel) { message = "upgrade.error.maximum"; return false; }
            if (level != expectedLevel) { message = "upgrade.error.offer_changed"; return false; }
            if (cost < 0) { message = "upgrade.error.invalid_price"; return false; }
            if (gold < cost) { message = "upgrade.error.insufficient_gold"; return false; }
            gold -= cost;
            level++;
            message = "upgrade.purchase.success";
            return true;
        }
    }
}
