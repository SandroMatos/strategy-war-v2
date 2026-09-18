using System;
using LastWars.Client;

public static class GridPlacementRulesTests
{
    public static void Run()
    {
        var moving = new BuildingDto { id = "moving", grid_x = 6, grid_y = 6, width = 2, height = 3 };
        var other = new BuildingDto { id = "other", grid_x = 10, grid_y = 10, width = 2, height = 2 };
        var state = new BaseDto { grid_bounds = new GridDto { min_x = 6, min_y = 6, max_x = 16, max_y = 16 }, buildings = new[] { moving, other } };
        var obstacles = new[] { new ObstacleDto { grid_x = 13, grid_y = 8, width = 2, height = 2 } };
        Check(GridPlacementRules.Validate(state, obstacles, moving, 6, 6) == null, "self excluded");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 14, 13) == null, "footprint fits exact upper boundary");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 15, 13) != null, "width outside boundary");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 14, 14) != null, "height outside boundary");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 5, 6) != null, "locked sector");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 9, 8) != null, "partial building collision");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 8, 10) == null, "touching edges allowed");
        Check(GridPlacementRules.Validate(state, obstacles, moving, 12, 6) != null, "partial obstacle collision");
        obstacles[0].is_cleared = true;
        Check(GridPlacementRules.Validate(state, obstacles, moving, 12, 6) == null, "cleared obstacle ignored");
        Check(GridPlacementRules.Validate(state, null, moving, 6, 6) != null, "missing blockers fail closed");
        Check(GridPlacementRules.Validate(state, obstacles, moving, int.MaxValue, 6) != null, "overflow rejected");
        Check(moving.grid_x == 6 && moving.grid_y == 6, "preview leaves authoritative snapshot unchanged");
    }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static void Main() { Run(); }
}
