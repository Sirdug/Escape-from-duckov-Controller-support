using System;
using System.Collections.Generic;
using DuckovPad;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        _checks++;
    }

    private static void Main()
    {
        var nav = new UiNavigation();
        Vector2 Step(Vector2 stick, float time) => nav.ReadStep(stick, Vector2.zero, time, 0.18f);
        Check(Step(Vector2.right, 0f) == Vector2.right, "A stick tap immediately advances one slot");
        Check(Step(Vector2.right, 0.1f) == Vector2.zero, "A short hold does not skip slots");
        Check(Step(Vector2.zero, 0.2f) == Vector2.zero, "Releasing the stick emits no movement");
        Check(Step(Vector2.zero, 5f) == Vector2.zero, "Idle stays stationary indefinitely");
        Check(Step(Vector2.right, 5.1f) == Vector2.right, "The next tap advances immediately");
        Check(Step(Vector2.right, 5.46f) == Vector2.right, "Holding repeats after the initial delay");
        Check(Step(Vector2.right, 5.5f) == Vector2.zero, "Repeat is rate limited");
        Check(Step(Vector2.right, 5.6f) == Vector2.right, "Holding continues to repeat");
        Check(Step(Vector2.left, 5.61f) == Vector2.left, "Changing direction responds immediately");
        Step(Vector2.zero, 5.7f);
        Check(Step(new Vector2(0.1f, -0.15f), 5.8f) == Vector2.zero, "Stick drift cannot move focus");
        Check(Step(new Vector2(0.49f, 0f), 5.9f) == Vector2.zero, "A partial push does not trigger navigation");
        Check(Step(new Vector2(0.6f, 0.55f), 6f) == Vector2.right, "Diagonals choose a single axis");
        Check(Step(new Vector2(0.55f, 0.6f), 6.01f) == Vector2.zero, "Diagonal noise does not switch axes");
        nav.Reset();
        Check(nav.ReadStep(Vector2.left, Vector2.up, 7f, 0.18f) == Vector2.up, "D-pad wins over conflicting stick input");
        Check(nav.ReadStep(Vector2.zero, Vector2.up, 7.36f, 0.18f) == Vector2.up, "D-pad also supports hold repeat");

        var grid = new List<Rect>();
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 5; col++) grid.Add(new Rect(100 + col * 90, 500 - row * 90, 80, 80));
        Check(UiNavigation.FindNext(grid, grid[6].center, Vector2.right, 65f) == 7, "Right picks the adjacent slot");
        Check(UiNavigation.FindNext(grid, grid[6].center, Vector2.left, 65f) == 5, "Left picks the adjacent slot");
        Check(UiNavigation.FindNext(grid, grid[6].center, Vector2.up, 65f) == 1, "Up keeps the column");
        Check(UiNavigation.FindNext(grid, grid[6].center, Vector2.down, 65f) == 11, "Down keeps the column");
        Check(UiNavigation.FindNext(grid, grid[0].center, Vector2.left, 65f) == -1, "An edge does not wrap or drift");
        Check(UiNavigation.FindNext(grid, grid[6].center + new Vector2(-30, 0), Vector2.right, 65f) == 7,
            "A free cursor inside a slot navigates from that slot");
        var nested = new[] { new Rect(0, 0, 1000, 800), grid[6] };
        Check(UiNavigation.FindNearest(nested, grid[6].center, 46f) == 1, "A containing panel cannot steal slot hover");
        Check(!UiNavigation.IsTargetSize(new Rect(0, 20, 1920, 900), 1920, 1080), "The screenshot backdrop is excluded");
        Check(UiNavigation.IsTargetSize(grid[6], 1920, 1080), "Inventory slots are accepted");
        Check(UiNavigation.FindNearest(grid, new Vector2(1800, 100), 46f) == -1, "Free cursor is not pulled across the screen");
        grid.Add(new Rect(1300, 410, 80, 80));
        Check(UiNavigation.FindNext(grid, grid[9].center, Vector2.right, 65f) == 15, "Focus crosses the gap to the other inventory");

        foreach (int fps in new[] { 30, 60, 144 })
        {
            nav.Reset();
            Vector2 cursor = grid[0].center;
            int steps = 0;
            for (int frame = 0; frame < fps * 2; frame++)
                if (nav.ReadStep(Vector2.right, Vector2.zero, frame / (float)fps, 0.18f) != Vector2.zero) steps++;
            Check(steps >= 13 && steps <= 15, "Hold speed is consistent at " + fps + " FPS");
            nav.Reset();
            Vector2 direction = nav.ReadStep(Vector2.right, Vector2.zero, 0f, 0.18f);
            cursor = grid[UiNavigation.FindNext(grid, cursor, direction, 65f)].center;
            Vector2 selected = cursor;
            for (int frame = 1; frame < fps * 5; frame++)
            {
                direction = nav.ReadStep(Vector2.zero, Vector2.zero, frame / (float)fps, 0.18f);
                if (direction == Vector2.zero) continue;
                int next = UiNavigation.FindNext(grid, cursor, direction, 65f);
                if (next >= 0) cursor = grid[next].center;
            }
            Check(cursor == selected, "Selection stays on its slot after release at " + fps + " FPS");
        }
        Console.WriteLine("Passed " + _checks + " navigation regression checks.");
    }
}
