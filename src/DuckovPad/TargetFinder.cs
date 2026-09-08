using System;
using Duckov.Utilities;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Everything the aim code needs to talk about a frame in screen space: where the duck is
    /// on screen, and which way the reticle currently points from it.
    /// </summary>
    internal struct AimFrame
    {
        public Camera Camera;
        public Vector3 Origin;

        /// <summary>The duck's screen position — the centre of the reticle's circle.</summary>
        public Vector2 Anchor;

        /// <summary>Current aim direction in screen degrees, measured from screen right.</summary>
        public float AimAngle;

        public readonly float ScreenAngleTo(Vector3 worldPoint)
        {
            Vector2 offset = (Vector2)Camera.WorldToScreenPoint(worldPoint) - Anchor;
            return offset.sqrMagnitude < 0.0001f ? AimAngle : Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Finds hostile damage receivers near the player, shared by aim assist and lock-on.
    ///
    /// Candidates are ranked by how far they sit from the crosshair on screen, not by world
    /// angle. The two disagree badly under this game's 55-degree pitched camera: a world cone
    /// projects to a screen wedge noticeably narrower top-to-bottom than side-to-side, so
    /// ranking in world space made assist reach for enemies the player could plainly see were
    /// nowhere near the crosshair.
    /// </summary>
    internal sealed class TargetFinder
    {
        public static bool HasLineOfSight(Vector3 origin, Vector3 point)
        {
            return !Physics.Linecast(origin + Vector3.up * 0.9f, point,
                GameplayDataSettings.Layers.wallLayerMask, QueryTriggerInteraction.Ignore);
        }

        private readonly Collider[] _candidates = new Collider[64];

        /// <summary>Diagnostics, surfaced by the debug overlay.</summary>
        public int LastColliderCount { get; private set; }
        public int LastHostileCount { get; private set; }
        public string LastFailureReason { get; private set; } = "not searched yet";

        public struct Result
        {
            public Transform Transform;
            public Vector3 Point;

            /// <summary>Where the target sits on screen, in degrees from the duck.</summary>
            public float ScreenAngle;

            /// <summary>Unsigned screen-space angle between the crosshair and the target.</summary>
            public float Angle;

            /// <summary>How far the target is from the duck on screen, in pixels.</summary>
            public float ScreenRadius;

            /// <summary>Flattened world distance, used for range limits and scoring.</summary>
            public float Distance;

            /// <summary>The wedge this target actually qualified under, stickiness included.
            /// Assist fades out against this, so a sticky target does not snap off at the rim.</summary>
            public float EffectiveCone;

            public bool Found;
        }

        /// <summary>
        /// Search a screen-space wedge around the crosshair for the best hostile.
        /// </summary>
        /// <param name="sticky">Target to bias toward, so aim does not flick between enemies.</param>
        /// <param name="ignore">Target to exclude, used when cycling lock-on to the next enemy.</param>
        /// <param name="minAngleDegrees">Skip targets closer to the crosshair than this, so a
        /// deliberate flick switches to a different enemy rather than back to the current one.</param>
        public Result Find(
            in AimFrame frame,
            float maxDistance,
            float maxAngleDegrees,
            bool requireLineOfSight,
            Transform sticky,
            float stickinessDegrees,
            Transform ignore = null,
            float minAngleDegrees = 0f)
        {
            var result = default(Result);
            LastColliderCount = 0;
            LastHostileCount = 0;

            if (frame.Camera == null)
            {
                LastFailureReason = "no camera";
                return result;
            }

            LayerMask receiverMask;
            LayerMask wallMask;
            try
            {
                receiverMask = GameplayDataSettings.Layers.damageReceiverLayerMask;
                wallMask = GameplayDataSettings.Layers.wallLayerMask;
            }
            catch (Exception e)
            {
                LastFailureReason = "layer masks unavailable: " + e.Message;
                return result;
            }

            // Collide (not Ignore): some damage receivers are trigger colliders, and
            // filtering them out is what made the first version of aim assist find nothing.
            int count = Physics.OverlapSphereNonAlloc(
                frame.Origin, maxDistance, _candidates, receiverMask, QueryTriggerInteraction.Collide);

            LastColliderCount = count;
            if (count <= 0)
            {
                LastFailureReason = "no colliders on the damage-receiver layer within " + maxDistance + "m";
                return result;
            }

            float bestScore = float.MaxValue;
            Vector3 eye = frame.Origin + Vector3.up * 0.9f;

            for (int i = 0; i < count; i++)
            {
                var collider = _candidates[i];
                if (collider == null) continue;

                var receiver = collider.GetComponent<DamageReceiver>();
                if (receiver == null) receiver = collider.GetComponentInParent<DamageReceiver>();
                if (receiver == null) continue;

                if (!Team.IsEnemy(Teams.player, receiver.Team)) continue;
                if (receiver.IsDead) continue;

                LastHostileCount++;

                if (ignore != null && (collider.transform == ignore || collider.transform.IsChildOf(ignore)))
                    continue;

                Vector3 point = collider.bounds.center;
                Vector3 offset = point - frame.Origin;
                offset.y = 0f;

                float distance = offset.magnitude;
                if (distance < 0.5f || distance > maxDistance) continue;

                Vector3 screen = frame.Camera.WorldToScreenPoint(point);
                if (screen.z <= 0f) continue; // behind the camera

                Vector2 screenOffset = (Vector2)screen - frame.Anchor;
                float screenRadius = screenOffset.magnitude;
                if (screenRadius < 1f) continue;

                float screenAngle = Mathf.Atan2(screenOffset.y, screenOffset.x) * Mathf.Rad2Deg;
                float angle = Mathf.Abs(AimMath.DeltaAngle(frame.AimAngle, screenAngle));

                float effectiveMaxAngle = maxAngleDegrees;
                if (collider.transform == sticky || (sticky != null && collider.transform.IsChildOf(sticky)))
                    effectiveMaxAngle += stickinessDegrees;

                if (angle > effectiveMaxAngle) continue;
                if (angle < minAngleDegrees) continue;

                // Closest to the crosshair wins, with a mild preference for nearer enemies
                // and a bonus for whoever we were already tracking.
                float score = angle + distance * 0.2f;
                if (collider.transform == sticky) score -= stickinessDegrees;
                if (score >= bestScore) continue;

                // Line of sight last: it is the only per-candidate raycast, so it only runs
                // for enemies that are actually about to win.
                if (requireLineOfSight &&
                    Physics.Linecast(eye, point, wallMask, QueryTriggerInteraction.Ignore))
                    continue;

                bestScore = score;
                result.Transform = collider.transform;
                result.Point = point;
                result.ScreenAngle = screenAngle;
                result.Angle = angle;
                result.ScreenRadius = screenRadius;
                result.Distance = distance;
                result.EffectiveCone = effectiveMaxAngle;
                result.Found = true;
            }

            if (!result.Found)
            {
                LastFailureReason = LastHostileCount == 0
                    ? "found " + count + " colliders but no living hostiles"
                    : LastHostileCount + " hostile(s) nearby, none inside the " + maxAngleDegrees + " degree wedge";
            }
            else
            {
                LastFailureReason = null;
            }

            return result;
        }
    }
}
