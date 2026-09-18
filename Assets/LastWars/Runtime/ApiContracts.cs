using System;
using UnityEngine;

namespace LastWars.Client
{
    // Wire names follow the FastAPI schemas. Fixed resource keys come from ResourceType.
    // Nullable timestamps stay strings; unused nullable integers are deliberately omitted.
    [Serializable] public sealed class AnonymousRequest { public string display_name; public string locale = "pt-BR"; }
    [Serializable] public sealed class PlayerDto { public string id; public string display_name; }
    [Serializable] public sealed class SessionDto { public string access_token; public string token_type; public PlayerDto player; }
    [Serializable] public sealed class ResourcesDto
    {
        public long food, iron, gold, oil;
        public bool Covers(ResourcesDto cost) => cost != null && food >= cost.food && iron >= cost.iron && gold >= cost.gold && oil >= cost.oil;
        public string Summary() => $"Comida {food:N0}   Ferro {iron:N0}\nOuro {gold:N0}   Petróleo {oil:N0}";
    }
    [Serializable] public sealed class GridDto { public int min_x, max_x, min_y, max_y; }
    [Serializable] public sealed class BuildingDto
    {
        public string id, type, status, construction_started_at, construction_finish_time;
        public int level, grid_x, grid_y, width, height, durability, max_durability;
    }
    [Serializable] public sealed class BaseDto
    {
        public string player_id;
        public ResourcesDto resources;
        public BuildingDto[] buildings;
        public int available_builders, total_builders;
        public GridDto grid_bounds;
    }
    [Serializable] public sealed class StatDto { public long current, next, difference; }
    [Serializable] public sealed class EffectsDto
    {
        public StatDto power, production_per_hour, max_storage_capacity, loot_protection,
            hero_exp_per_hour, hero_level_limit, hp, attack, defense, hospital_capacity,
            wall_durability, free_construction_speedup_seconds;
    }
    [Serializable] public sealed class UnlocksDto { public string[] unlocks; }
    [Serializable] public sealed class UpgradeDto
    {
        public string building_id, building_type, construction_time_formatted;
        public int current_level, next_level;
        public ResourcesDto required_resources;
        public EffectsDto stat_effects;
        public UnlocksDto unlocked_features;
    }
    [Serializable] public sealed class TutorialDto { public string status, npc_message; public int current_step; public bool completed; }
    [Serializable] public sealed class ApiErrorDto { public string detail; }
    [Serializable] public sealed class ObstacleDto
    {
        public string id, kind; public int grid_x, grid_y, width, height; public bool is_cleared;
    }
    [Serializable] public sealed class ObstacleList { public ObstacleDto[] items; }

    public static class Contracts
    {
        public static bool ValidSession(SessionDto session) => session != null &&
            !string.IsNullOrWhiteSpace(session.access_token) && session.player != null &&
            Guid.TryParse(session.player.id, out _) && session.token_type == "bearer";

        public static bool ValidBase(BaseDto data, string playerId)
        {
            if (data == null || data.player_id != playerId || data.resources == null ||
                data.buildings == null || data.grid_bounds == null) return false;
            foreach (var item in data.buildings)
                if (item == null || !Guid.TryParse(item.id, out _) || string.IsNullOrEmpty(item.type) || item.width <= 0 || item.height <= 0) return false;
            return data.grid_bounds.max_x > data.grid_bounds.min_x && data.grid_bounds.max_y > data.grid_bounds.min_y;
        }

        public static string Name(string type)
        {
            switch (type)
            {
                case "command_center": case "headquarters": return "Sede";
                case "iron_mine": return "Mina de ferro";
                case "gold_refinery": return "Refinaria de ouro";
                case "food_farm": return "Fazenda";
                case "oil_pump": return "Bomba de petróleo";
                case "barracks": return "Quartel";
                case "drill_ground": return "Campo de treinamento";
                case "hospital": return "Hospital";
                case "wall": return "Muro";
                case "builder_hut": case "builder_house": return "Casa do construtor";
                case "iron_warehouse": return "Armazém de ferro";
                case "tech_center": return "Centro de pesquisa";
                default: return type.Replace('_', ' ');
            }
        }
        public static string State(string value)
        {
            switch (value)
            {
                case "completed": return "Operacional";
                case "pending": return "Aguardando construção";
                case "building": return "Em construção";
                case "ready_to_upgrade": return "Pronto para concluir";
                case "destroyed": return "Destruído";
                case "repairing": return "Em reparo";
                default: return value;
            }
        }
        public static bool Produces(string type) => type == "food_farm" || type == "iron_mine" || type == "gold_refinery" || type == "oil_pump";
    }
}
