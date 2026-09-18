using System;

namespace LastWars.Client
{
    [Serializable] public sealed class MoveBuildingRequest { public int grid_x, grid_y; }
    [Serializable] public sealed class MoveBuildingResponse
    { public string building_id; public int grid_x, grid_y, width, height; }

    public static class GridPlacementRules
    {
        public static string Validate(BaseDto state, ObstacleDto[] obstacles, BuildingDto building, int x, int y)
        {
            if (state?.grid_bounds == null || state.buildings == null || obstacles == null || building == null ||
                building.width <= 0 || building.height <= 0) return "Atualize a base e os obstáculos antes de mover.";
            var g = state.grid_bounds;
            if (x < g.min_x || y < g.min_y || (long)x + building.width > g.max_x || (long)y + building.height > g.max_y)
                return "Fora do setor liberado pela sede.";
            foreach (var other in state.buildings)
                if (other.id != building.id && Overlaps(x, y, building.width, building.height, other.grid_x, other.grid_y, other.width, other.height))
                    return "Espaço ocupado por outro edifício.";
            foreach (var obstacle in obstacles)
                if (!obstacle.is_cleared && Overlaps(x, y, building.width, building.height, obstacle.grid_x, obstacle.grid_y, obstacle.width, obstacle.height))
                    return "Remova o obstáculo antes de ocupar estas células.";
            return null;
        }
        static bool Overlaps(int x, int y, int w, int h, int ox, int oy, int ow, int oh) =>
            x < (long)ox + ow && (long)x + w > ox && y < (long)oy + oh && (long)y + h > oy;
    }
}
