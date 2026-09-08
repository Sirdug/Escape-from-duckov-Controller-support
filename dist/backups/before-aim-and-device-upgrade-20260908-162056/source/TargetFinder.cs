using System;
using Duckov.Utilities;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Finds hostile damage receivers near the player, shared by aim assist and lock-on.
    /// </summary>
    internal sealed class TargetFinder
    {
        private readonly Collider[] _candidates = new Collider[64];

        /// <summary>Diagnostics, surfaced by the debug overlay.</summary>
        public int LastColliderCount { get; private set; }
        public int LastHostileCount { get; private set; }
        public string LastFailureReason { get; private set; } = "not searched yet";

        public struct Result
        {
            public Transform Transform;
            public Vector3 Point;
            /// <summary>Flattened, normalised direction from the player to the target.</summary>
            public Vector3 Direction;
            public float Angle;
            public float Distance;
            public bool Found;
        }

        /// <summary>
        /// Search a cone around <paramref name="aimDirection"/> for the best hostile.
        /// </summary>
        /// <param name="sticky">Target to bias toward, so aim doesn't flick between enemies.</param>
        public Result Find(
            Vector3 origin,
            Vector3 aimDirection,
            float maxDistance,
            float maxAngleDegrees,
            bool requireLineOfSight,
            Transform sticky,
            float stickinessDegrees)
        {
            var result = default(Result);
            LastColliderCount = 0;
            LastHostileCount = 0;

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
                origin, maxDistance, _candidates, receiverMask, QueryTriggerInteraction.Collide);

            LastColliderCount = count;
            if (count <= 0)
            {
                LastFailureReason = "no colliders on the damage-receiver layer within " + maxDistance + "m";
                return result;
            }

            float bestScore = float.MaxValue;
            Vector3 eye = origin + Vector3.up * 0.9f;

            for (int i = 0; i < count; i++)
            {
                var collider = _candidates[i];
                if (collider == null) continue;

                var receiver = collider.GetComponent<DamageReceiver>();
                if (receiver == null) receiver = collider.GetComponentInParent<DamageReceiver>();
                if (receiver == null) continue;

                if (receiver.Team == Teams.player) continue;
                if (receiver.IsDead) continue;

                LastHostileCount++;

                Vector3 point = collider.bounds.center;
                Vector3 offset = point - origin;
                offset.y = 0f;

                float distance = offset.magnitude;
                if (distance < 0.5f || distance > maxDistance) continue;

                Vector3 direction = offset / distance;
                float angle = Vector3.Angle(aimDirection, direction);

                float effectiveMaxAngle = maxAngleDegrees;
                if (collider.transform == sticky || (sticky != null && collider.transform.IsChildOf(sticky)))
                    effectiveMaxAngle += stickinessDegrees;

                if (angle > effectiveMaxAngle) continue;

                if (requireLineOfSight &&
                    Physics.Linecast(eye, point, wallMask, QueryTriggerInteraction.Ignore))
                    continue;

                // Closest to the crosshair wins, with a mild preference for nearer enemies
                // and a bonus for whoever we were already tracking.
                float score = angle + distance * 0.2f;
                if (collider.transform == sticky) score -= stickinessDegrees;

                if (score < bestScore)
                {
                    bestScore = score;
                    result.Transform = collider.transform;
                    result.Point = point;
                    result.Direction = direction;
                    result.Angle = angle;
                    result.Distance = distance;
                    result.Found = true;
                }
            }

            if (!result.Found)
            {
                LastFailureReason = LastHostileCount == 0
                    ? "found " + count + " colliders but no living hostiles"
                    : LastHostileCount + " hostile(s) nearby, none inside the " + maxAngleDegrees + "° cone";
            }
            else
            {
                LastFailureReason = null;
            }

            return result;
        }
    }
}
