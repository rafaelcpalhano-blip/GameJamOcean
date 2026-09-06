namespace GameJamOcean.Progression
{
    // Pure transaction rules: no UI, scene or Unity dependencies.
    public static class UpgradePurchaseRules
    {
        public static bool TryApply(ref int gold, ref int level, int expectedLevel,
            int maximumLevel, int cost, out string message)
        {
            if (level < 0 || level >= maximumLevel) { message = "Nível máximo atingido."; return false; }
            if (level != expectedLevel) { message = "Oferta mudou; atualize o painel."; return false; }
            if (cost < 0) { message = "Preço inválido."; return false; }
            if (gold < cost) { message = "Ouro insuficiente."; return false; }
            gold -= cost;
            level++;
            message = "Upgrade adquirido!";
            return true;
        }
    }
}
