using System;
using DuckovPad;

internal static class Program
{
    private static int _checks;
    private static void Check(bool result, string name)
    {
        if (!result) throw new Exception(name);
        _checks++;
    }

    private static bool Near(float a, float b, float tolerance = 0.001f) => Math.Abs(a - b) < tolerance;

    private static void Main()
    {
        StickShaping();
        AngleMath();
        TurnResponse();
        AssistCurves();
        ControllerProfiles();
        PointerFocusChecks();
        Console.WriteLine("Passed " + _checks + " aiming and controller profile checks.");
    }

    private static void StickShaping()
    {
        Check(AimMath.ShapeMagnitude(0.1f, 0.2f, 0.95f, 1.5f) == 0, "Drift stays inside deadzone");
        Check(AimMath.ShapeMagnitude(0.2f, 0.2f, 0.95f, 1.5f) == 0, "Deadzone boundary stays still");
        Check(AimMath.ShapeMagnitude(0.21f, 0.2f, 0.95f, 1.5f) > 0, "Movement starts just outside the configured deadzone");
        Check(AimMath.ShapeMagnitude(1f, 0.2f, 0.95f, 1.5f) == 1, "Full stick reaches full scale");
        Check(AimMath.ShapeMagnitude(1.42f, 0.2f, 0.95f, 1.5f) == 1, "Diagonal overrange is clamped");
        float previous = 0;
        for (int i = 0; i <= 100; i++)
        {
            float value = AimMath.ShapeMagnitude(i / 100f, 0.2f, 0.95f, 1.5f);
            Check(value >= previous && value <= 1, "Stick response stays monotonic at " + i);
            previous = value;
        }
    }

    private static void AngleMath()
    {
        Check(Near(AimMath.DeltaAngle(10, 30), 20), "Delta is signed and short");
        Check(Near(AimMath.DeltaAngle(179, -179), 2), "Delta wraps forward across the seam");
        Check(Near(AimMath.DeltaAngle(-179, 179), -2), "Delta wraps back across the seam");
        Check(Math.Abs(AimMath.DeltaAngle(0, 180)) <= 180, "A half turn stays in range");
        for (int a = -540; a <= 540; a += 7)
        for (int b = -540; b <= 540; b += 13)
        {
            float delta = AimMath.DeltaAngle(a, b);
            Check(delta > -180.001f && delta <= 180.001f, "Delta stays inside one turn for " + a + " to " + b);
        }

        Check(AimMath.SmoothAngle(0, 180, 28, 1f / 60) > 0, "An exact 180 degree reversal cannot stall");
        Check(AimMath.SmoothAngle(179, -179, 28, 1f / 60) > 179, "Wraparound takes the short path");
        Check(AimMath.SmoothAngle(-179, 179, 28, 1f / 60) < -179, "Reverse wraparound takes the short path");
        Check(Near(AimMath.SmoothAngle(90, 90, 28, 1), 90), "Steady direction does not wander");
        Check(Near(AimMath.SmoothAngle(0, 90, 0, 0.016f), 90), "Zero smoothing is immediate");
        Check(Near(AimMath.SmoothAngle(0, 90, 28, 0), 0), "No elapsed time means no smoothing step");

        Check(Near(AimMath.MoveTowards(0, 10, 3), 3), "MoveTowards steps by the limit");
        Check(Near(AimMath.MoveTowards(0, 10, 30), 10), "MoveTowards never overshoots");
        Check(Near(AimMath.MoveTowards(0, -10, 3), -3), "MoveTowards works downward");
        Check(Near(AimMath.MoveTowards(5, 5, 3), 5), "MoveTowards at the target holds still");
    }

    private static void TurnResponse()
    {
        // A capped approach must still land exactly on the target and never sail past it.
        float angle = 0;
        for (int i = 0; i < 600; i++) angle = AimMath.ApproachAngle(angle, 90, 26, 1000, 1f / 120);
        Check(Near(angle, 90, 0.01f), "A held stick direction is reached exactly");

        Check(Near(AimMath.ApproachAngle(0, 5, 26, 1000, 10f), 5), "A huge step cannot overshoot the target");
        Check(Near(AimMath.ApproachAngle(0, -5, 26, 1000, 10f), -5), "Overshoot protection works downward");

        // The speed cap is what makes a sweep take a predictable amount of time instead of
        // covering most of the distance in the first frame.
        Check(Near(AimMath.ApproachAngle(0, 180, 26, 600, 0.1f), 60, 0.01f), "The sweep cap limits a big turn");
        Check(AimMath.ApproachAngle(0, 180, 26, 0, 0.1f) > 60, "An uncapped turn is free to go faster");

        // A response of zero means "arrive immediately", which is why AimDriver floors the
        // combined turn scale: at zero it would teleport the crosshair, not freeze it.
        Check(Near(AimMath.ApproachAngle(0, 90, 0, 0, 1f / 60), 90), "Zero response arrives at once");
        Check(AimMath.ApproachAngle(0, 90, 26 * 0.02f, 1000 * 0.02f, 1f / 60) < 1f, "A floored scale creeps instead");

        // Frame rate must not change the feel.
        float slow = 0, fast = 0;
        for (int i = 0; i < 30; i++) slow = AimMath.ApproachAngle(slow, 150, 8, 0, 1f / 30);
        for (int i = 0; i < 120; i++) fast = AimMath.ApproachAngle(fast, 150, 8, 0, 1f / 120);
        Check(Near(slow, fast, 0.02f), "30 fps and 120 fps produce the same aim response");

        // The complaint that started this: the same sweep has to take the same time whichever
        // way the player is pointing. Angles only ever enter through their difference, so a
        // turn near the seam behaves exactly like one in the middle of the range.
        float baseline = AimMath.ApproachAngle(0, 60, 26, 900, 1f / 60);
        for (int start = -350; start <= 350; start += 10)
        {
            float travelled = AimMath.DeltaAngle(start, AimMath.ApproachAngle(start, start + 60, 26, 900, 1f / 60));
            Check(Near(travelled, baseline, 0.01f), "Turn rate is the same starting from " + start);
        }

        // Part-stick has to buy fine control without ever capping what a full stick can do.
        Check(Near(AimMath.PrecisionScale(1f, 0.72f), 1f), "Full deflection always turns at full speed");
        Check(Near(AimMath.PrecisionScale(1f, 0f), 1f), "Zero precision range still turns at full speed");
        Check(Near(AimMath.PrecisionScale(0.2f, 0f), 1f), "Zero precision range keeps small pushes fast");
        Check(AimMath.PrecisionScale(0.2f, 0.72f) < 0.35f, "A light push turns much more slowly");
        Check(AimMath.PrecisionScale(0.2f, 0.72f) > 0f, "A light push still moves");
        float last = 0;
        for (int i = 0; i <= 100; i++)
        {
            float scale = AimMath.PrecisionScale(i / 100f, 0.72f);
            Check(scale >= last - 1e-6f && scale <= 1f, "Precision scaling stays monotonic at " + i);
            last = scale;
        }
    }

    private static void AssistCurves()
    {
        Check(Near(AimMath.ConeFalloff(0, 20), 1), "Assist is strongest dead on target");
        Check(Near(AimMath.ConeFalloff(20, 20), 0), "Assist is gone at the rim of the wedge");
        Check(Near(AimMath.ConeFalloff(40, 20), 0), "Assist stays gone outside the wedge");
        Check(Near(AimMath.ConeFalloff(-10, 20), AimMath.ConeFalloff(10, 20)), "The wedge is symmetric");
        Check(Near(AimMath.ConeFalloff(5, 0), 0), "A zero wedge assists nothing");

        // Smoothstep, not a ramp: the derivative has to die at the rim or targets pop on and
        // off as the crosshair crosses the boundary.
        float step = AimMath.ConeFalloff(19.5f, 20) - AimMath.ConeFalloff(20f, 20);
        float middle = AimMath.ConeFalloff(9.5f, 20) - AimMath.ConeFalloff(10f, 20);
        Check(step < middle * 0.25f, "Assist fades out rather than snapping off at the rim");

        float previous = 1.01f;
        for (int i = 0; i <= 40; i++)
        {
            float value = AimMath.ConeFalloff(i * 0.5f, 20);
            Check(value <= previous + 1e-6f && value >= 0f, "Assist falloff is monotonic at " + i);
            previous = value;
        }

        Check(Near(AimMath.Ease(0), 0), "Lock-on blend starts at nothing");
        Check(Near(AimMath.Ease(1), 1), "Lock-on blend finishes fully on target");
        Check(Near(AimMath.Ease(0.5f), 0.5f), "Lock-on blend is symmetric about its midpoint");
        Check(AimMath.Ease(0.02f) < 0.02f, "Lock-on eases in rather than jumping");
        Check(AimMath.Ease(-1) == 0 && AimMath.Ease(2) == 1, "Lock-on blend clamps");

        Check(Near(AimMath.Clamp(5, 0, 3), 3), "Clamp holds the ceiling");
        Check(Near(AimMath.Clamp(-5, 0, 3), 0), "Clamp holds the floor");
        Check(Near(AimMath.Clamp01(0.4f), 0.4f), "Clamp01 passes values through");
    }

    private static void PointerFocusChecks()
    {
        Check(!PointerFocus.ShouldYield(true, 40000f, 0f), "A rejected Mac cursor warp cannot steal pad focus");
        Check(PointerFocus.ShouldYield(true, 121f, 4f), "Real Mac mouse movement takes focus");
        Check(!PointerFocus.ShouldYield(true, 40000f, 40000f, true), "Mac warp motion cannot interrupt active stick input");
        Check(PointerFocus.ShouldYield(true, 40000f, 4f, false), "Mouse takeover works after the controller settles");
        Check(!PointerFocus.ShouldYield(true, 4f, 4f), "Cursor rounding does not steal focus");
        Check(!PointerFocus.ShouldYield(true, 121f, 0.001f), "Tiny Mac mouse noise is ignored");
        Check(PointerFocus.ShouldYield(false, 121f, 0f), "Existing Windows cursor arbitration is preserved");
        Check(!PointerFocus.ShouldYield(false, 100f, 4f), "Divergence at the tolerance stays with the pad");
    }

    private static void ControllerProfiles()
    {
        Check(ControllerProfile.Detect("k_ESteamInputType_SteamDeckController") == ControllerFamily.SteamDeck, "Steam reports a physical Deck");
        Check(ControllerProfile.Detect("Valve Steam Deck") == ControllerFamily.SteamDeck, "Native Deck metadata");
        Check(ControllerProfile.Detect("XInputControllerWindows Xbox 360") == ControllerFamily.Xbox, "Virtual Xbox is not guessed to be Deck");
        Check(ControllerProfile.Detect("DualSense Wireless Controller") == ControllerFamily.PlayStation, "DualSense detection");
        Check(ControllerProfile.Detect("DualShock4GamepadHID") == ControllerFamily.PlayStation, "DualShock detection");
        Check(ControllerProfile.Detect("k_ESteamInputType_PS5Controller") == ControllerFamily.PlayStation, "Steam PS5 detection");
        Check(ControllerProfile.Detect("SwitchProControllerHID") == ControllerFamily.Switch, "Switch detection");
        Check(ControllerProfile.Detect("Steam Controller") == ControllerFamily.SteamController, "Steam Controller detection");
        Check(ControllerProfile.Detect("Unknown HID controller") == ControllerFamily.Generic, "Unknown devices stay generic");
        Check(ControllerProfile.Detect(null) == ControllerFamily.Generic, "Missing metadata is safe");
        Check(ControllerProfile.Label("buttonSouth", ControllerFamily.PlayStation) == "Cross", "PS confirmation label");
        Check(ControllerProfile.Label("buttonEast", ControllerFamily.PlayStation) == "Circle", "PS cancel label");
        Check(ControllerProfile.Label("buttonWest", ControllerFamily.Switch) == "Y", "Nintendo physical west label");
        Check(ControllerProfile.Label("buttonSouth", ControllerFamily.Switch) == "B", "Nintendo physical south label");
        Check(ControllerProfile.Label("leftTrigger", ControllerFamily.SteamDeck) == "L2", "Deck trigger label");
        Check(ControllerProfile.Label("leftShoulder", ControllerFamily.SteamDeck) == "L1", "Deck shoulder label");
        Check(ControllerProfile.Label("buttonSouth", ControllerFamily.Xbox) == "A", "Xbox confirmation label");
    }
}
