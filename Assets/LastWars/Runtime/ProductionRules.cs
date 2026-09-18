using System;

namespace LastWars.Client
{
    public enum ResourceKind { Food, Iron, Gold, Oil }
    [Serializable] public sealed class ProductionDto
    {
        public string building_id, building_type;
        public ResourcesDto production_per_hour, stored_resources;
        public long local_capacity;
    }
    [Serializable] public sealed class ProductionStorageDto
    {
        public long global_capacity_per_resource;
        public ResourcesDto wallet_balances;
        public ProductionDto[] local_production;
    }
    public struct ProductionState
    {
        public bool Ready, Active;
        public int Seconds;
        public float Progress;
    }
    public static class ProductionRules
    {
        public static ResourceKind Kind(string type)
        {
            switch (type)
            {
                case "iron_mine": return ResourceKind.Iron;
                case "gold_refinery": return ResourceKind.Gold;
                case "oil_pump": return ResourceKind.Oil;
                default: return ResourceKind.Food;
            }
        }
        public static long Amount(ResourcesDto value, ResourceKind kind)
        {
            if (value == null) return 0;
            switch (kind) { case ResourceKind.Iron: return value.iron; case ResourceKind.Gold: return value.gold; case ResourceKind.Oil: return value.oil; default: return value.food; }
        }
        public static string Name(ResourceKind kind)
        {
            switch (kind) { case ResourceKind.Iron: return "Ferro"; case ResourceKind.Gold: return "Ouro"; case ResourceKind.Oil: return "Petróleo"; default: return "Comida"; }
        }
        // The production endpoint accrues whole units and resets its timestamp on each GET.
        // Start the monotonic estimate at response receipt (conservative for network latency).
        public static ProductionState Evaluate(ProductionDto value, double elapsed)
        {
            if (value == null || value.stored_resources == null || value.production_per_hour == null || value.local_capacity <= 0)
                return default;
            var kind = Kind(value.building_type);
            if (Amount(value.stored_resources, kind) > 0) return new ProductionState { Ready = true, Active = true, Progress = 1 };
            long rate = Amount(value.production_per_hour, kind);
            if (rate <= 0) return default;
            double duration = 3600.0 / rate;
            elapsed = Math.Max(0, elapsed);
            double remaining = Math.Max(0, duration - elapsed);
            return new ProductionState { Active = true, Ready = remaining == 0,
                Seconds = (int)Math.Min(int.MaxValue, Math.Ceiling(remaining)),
                Progress = (float)Math.Min(1, elapsed / duration) };
        }
        public static string UpgradeBlocker(BaseDto state, UpgradeDto quote, bool fresh)
        {
            if (!fresh || state?.resources == null || quote?.required_resources == null) return "Não foi possível verificar os recursos. Reabra para tentar novamente.";
            if (!state.resources.Covers(quote.required_resources)) return "Recursos insuficientes para este nível.";
            if (state.available_builders <= 0) return "Nenhum construtor disponível.";
            return null;
        }
    }
}
