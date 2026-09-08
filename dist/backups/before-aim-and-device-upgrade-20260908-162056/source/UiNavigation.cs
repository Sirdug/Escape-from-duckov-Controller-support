using System.Collections.Generic;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>Directional menu input, independent of frame rate and cursor speed.</summary>
    internal sealed class UiNavigation
    {
        private Vector2 _heldDirection;
        private float _nextRepeat;

        public void Reset()
        {
            _heldDirection = Vector2.zero;
            _nextRepeat = 0f;
        }

        public Vector2 ReadStep(Vector2 stick, Vector2 dpad, float time, float deadzone)
        {
            Vector2 direction = dpad;
            if (direction == Vector2.zero)
            {
                // Cursor acceleration must not delay a menu step. Hysteresis avoids
                // repeated presses from noise near the threshold or a diagonal.
                float release = Mathf.Max(deadzone, 0.25f);
                float press = Mathf.Max(release + 0.1f, 0.5f);
                float threshold = _heldDirection == Vector2.zero ? press : release;
                if (stick.magnitude >= threshold)
                {
                    bool horizontal = Mathf.Abs(stick.x) >= Mathf.Abs(stick.y);
                    if (_heldDirection.x != 0f && Mathf.Abs(stick.y) < Mathf.Abs(stick.x) * 1.25f)
                        horizontal = true;
                    else if (_heldDirection.y != 0f && Mathf.Abs(stick.x) < Mathf.Abs(stick.y) * 1.25f)
                        horizontal = false;
                    direction = horizontal
                        ? new Vector2(Mathf.Sign(stick.x), 0f)
                        : new Vector2(0f, Mathf.Sign(stick.y));
                }
            }
            if (direction == Vector2.zero)
            {
                Reset();
                return Vector2.zero;
            }
            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = time + 0.35f;
                return direction;
            }
            if (time < _nextRepeat) return Vector2.zero;
            _nextRepeat = time + 0.12f;
            return direction;
        }

        public static bool IsTargetSize(Rect rect, float width, float height)
        {
            return rect.width >= 6f && rect.height >= 6f
                   && rect.width < width * 0.85f && rect.height < height * 0.85f
                   && rect.width * rect.height < width * height * 0.2f;
        }

        public static int FindNearest(IReadOnlyList<Rect> rects, Vector2 cursor, float radius)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            float bestArea = float.MaxValue;
            bool inside = false;
            for (int i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];
                bool contains = rect.Contains(cursor);
                float area = rect.width * rect.height;
                float distance = Vector2.Distance(cursor, rect.center);
                if (contains)
                {
                    // Prefer the slot to an enclosing panel, regardless of scan order.
                    if (inside && area >= bestArea) continue;
                    inside = true;
                    bestArea = area;
                }
                else if (inside || distance > radius || distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        public static int FindNext(IReadOnlyList<Rect> rects, Vector2 cursor, Vector2 direction, float coneDegrees)
        {
            if (direction.sqrMagnitude < 0.0001f) return -1;
            direction = direction.normalized;
            float cosLimit = Mathf.Cos(coneDegrees * Mathf.Deg2Rad);
            int current = FindNearest(rects, cursor, 0f);
            if (current >= 0) cursor = rects[current].center;
            float bestScore = float.MaxValue;
            int best = -1;
            for (int i = 0; i < rects.Count; i++)
            {
                if (i == current) continue;
                Vector2 offset = rects[i].center - cursor;
                float distance = offset.magnitude;
                if (distance < 4f) continue;
                float forward = Vector2.Dot(offset, direction);
                if (forward / distance < cosLimit) continue;
                float lateral = Mathf.Abs(Vector2.Dot(offset, new Vector2(-direction.y, direction.x)));
                float score = forward + lateral * 2.5f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }
    }
}
