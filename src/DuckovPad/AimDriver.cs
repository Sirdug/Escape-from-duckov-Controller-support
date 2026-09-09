using System;
using System.Reflection;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Turns the right stick into an aim point.
    ///
    /// The game aims by keeping a virtual mouse position (<c>InputManager._aimMousePosCache</c>),
    /// raycasting it onto a ground plane at the duck's height, and handing the resulting world
    /// point to the character. Rather than fake mouse movement — which fights the game's own
    /// cursor warping and produces drifting, relative aim — we write that virtual mouse position
    /// directly, then let the untouched vanilla pipeline (recoil, obstacle sweeps, head targeting)
    /// run on top by calling SetAimInputUsingMouse with a zero delta.
    ///
    /// Everything here is reasoned about in <em>screen</em> space: an angle around the duck and a
    /// radius in pixels. The camera looks down at 55° and is yawed 30°, so a circle drawn around
    /// the duck in the world projects to a tilted ellipse — aim built on world angles and world
    /// reticle distances therefore sweeps at a different rate, and pumps the crosshair in and out
    /// by different amounts, depending on which way the player is pointing. Keeping the reticle on
    /// a genuine screen-space circle removes that entirely: the crosshair travels at one speed in
    /// every direction, and never needs its reach cut short to stay on screen.
    /// </summary>
    internal sealed class AimDriver
    {
        private static readonly FieldInfo AimCacheField =
            typeof(InputManager).GetField("_aimMousePosCache", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AimSyncedField =
            typeof(InputManager).GetField("aimMousePosFirstSynced", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Keeps the crosshair clear of the very edge of the window.</summary>
        private const float ScreenMargin = 44f;

        private readonly PadConfig _config;
        private readonly TargetFinder _finder = new TargetFinder();

        private string _mode;
        private bool _initialized;

        /// <summary>Where the stick is pointing, in screen degrees. Player intent, before help.</summary>
        private float _aimAngle;

        /// <summary>How fast the player is sweeping, deg/sec, lightly smoothed.</summary>
        private float _turnRate;

        /// <summary>Reticle distance from the duck, in pixels.</summary>
        private float _radiusPx;

        // Aim assist
        private Transform _assistTarget;
        private float _assistOffset;      // accumulated pull, degrees, added to _aimAngle
        private float _assistScreenRadius;
        private float _friction;          // 0..1, how much the turn rate is damped this frame

        // Lock-on
        private Transform _lockTarget;
        private float _lockWeight;        // 0..1, eased blend onto the locked target
        private float _lockAngle;
        private float _lockRadius;
        private Vector3 _lockPoint;
        private bool _lockBreakLatched;
        private Transform _lockVisual;

        // Recoil carried between frames (see WriteAimPosition).
        private Vector2 _recoilOffset;
        private float _recoilIdle;

        // Trackpad / gyro
        private bool _pointerAim;
        private Vector2 _pointerScreen;

        /// <summary>Last aim point written, in screen space.</summary>
        public Vector2 ReticleScreenPosition { get; private set; }
        public bool HasReticle { get; private set; }

        // Diagnostics for the debug overlay.
        public bool LockedOn => _lockTarget != null;
        public Vector3 LockPoint => _lockPoint;

        /// <summary>
        /// What the lock is visually attached to, which outlasts <see cref="LockedOn"/> for the
        /// length of the release ease — the outline and the crosshair have to fade out together,
        /// and the crosshair is still travelling back after the target has been let go.
        /// </summary>
        public Transform LockVisualTarget => _lockWeight > 0.001f ? _lockVisual : null;

        /// <summary>Eased 0..1 lock blend, matching what the crosshair is doing.</summary>
        public float LockBlend => AimMath.Ease(_lockWeight);
        public string AssistTargetName { get; private set; } = "none";
        public float AssistAngle { get; private set; }
        public float AssistWeight { get; private set; }
        public float AssistPull => _assistOffset;
        public float Friction => _friction;
        public int NearbyColliders => _finder.LastColliderCount;
        public int NearbyHostiles => _finder.LastHostileCount;
        public string AssistStatus { get; private set; } = "idle";

        public AimDriver(PadConfig config)
        {
            _config = config;
            _mode = config.Aim.Mode;
            if (AimCacheField == null)
                Log.Error("InputManager._aimMousePosCache not found — the game's aim internals changed. " +
                          "Gamepad aiming will fall back to relative mode.");
        }

        public void Reset()
        {
            _initialized = false;
            _aimAngle = 90f;
            _turnRate = 0f;
            _radiusPx = 0f;
            _assistTarget = null;
            _assistOffset = 0f;
            _assistScreenRadius = 0f;
            _friction = 0f;
            AssistWeight = 0f;
            _lockTarget = null;
            _lockVisual = null;
            _lockWeight = 0f;
            _lockBreakLatched = false;
            _recoilOffset = Vector2.zero;
            _recoilIdle = 0f;
            _pointerAim = false;
            HasReticle = false;
        }

        public void ClearLock()
        {
            _lockTarget = null;
            _lockBreakLatched = false;
        }

        /// <summary>Toggle or engage lock-on against the best target in front of the player.</summary>
        public bool ToggleLockOn(CharacterMainControl character)
        {
            if (_lockTarget != null)
            {
                _lockTarget = null;
                return false;
            }

            return AcquireLock(character);
        }

        public bool AcquireLock(CharacterMainControl character)
        {
            if (!TryBuildFrame(character, out var frame)) return false;

            var snap = _config.AimSnap;
            var result = _finder.Find(frame, snap.MaxDistance, snap.MaxAngleDegrees,
                snap.RequireLineOfSight, _lockTarget, 0f);

            if (!result.Found) return false;

            _lockTarget = result.Transform;
            _lockPoint = result.Point;
            _lockAngle = result.ScreenAngle;
            _lockRadius = result.ScreenRadius;
            _lockBreakLatched = false;
            return true;
        }

        /// <summary>
        /// Drive one frame of aiming. Returns false if the world isn't in a state where
        /// aiming makes sense, in which case the caller should leave aim alone.
        /// </summary>
        public bool Update(InputManager inputManager, CharacterMainControl character, bool adsHeld, bool firing, float deltaTime)
        {
            HasReticle = false;

            if (inputManager == null || character == null) return false;
            if (!TryBuildFrame(character, out var frame)) return false;

            var aim = _config.Aim;
            if (!string.Equals(_mode, aim.Mode, StringComparison.OrdinalIgnoreCase))
            {
                Reset();
                _mode = aim.Mode;
                if (!TryBuildFrame(character, out frame)) return false;
            }

            bool relative = string.Equals(aim.Mode, "relative", StringComparison.OrdinalIgnoreCase)
                            || AimCacheField == null;

            Vector2 stick = Pad.RightStick(aim.Deadzone, aim.OuterDeadzone, aim.ResponseCurve);

            // A long frame (loading hitch, alt-tab) must not teleport the aim or unwind the
            // whole assist offset in one step.
            float dt = Mathf.Clamp(deltaTime, 0.0001f, 0.1f);

            if (relative) return UpdateRelative(inputManager, stick, adsHeld, dt);

            if (UpdatePointer(frame, stick, dt, out var pointerPoint))
            {
                WriteAimPosition(inputManager, pointerPoint, dt);
                ReticleScreenPosition = inputManager.AimScreenPoint;
                HasReticle = true;
                return true;
            }

            float magnitude = Mathf.Clamp01(stick.magnitude);
            UpdatePlayerAngle(frame, aim, stick, magnitude, adsHeld, dt);

            // The crosshair the player can actually see is where assist searches from.
            frame.AimAngle = _aimAngle + _assistOffset;
            UpdateAssist(frame, adsHeld, firing, dt);

            frame.AimAngle = _aimAngle + _assistOffset;
            float lead = UpdateLock(frame, magnitude, dt);

            float freeAngle = _aimAngle + _assistOffset;
            float angle = freeAngle;
            float radius = DesiredRadius(aim, adsHeld);

            if (_lockWeight > 0.0001f)
            {
                // Lock strength scales the pin: 1 fully locks the cursor/aim marker onto
                // the target, lower loosens toward assist-like help while still showing
                // the marker + outline. 0 leaves aim free (target only indicated).
                float pin = AimMath.Ease(_lockWeight) * Mathf.Clamp01(_config.AimSnap.Strength);
                angle = freeAngle + AimMath.DeltaAngle(freeAngle, _lockAngle + lead) * pin;

                // No upper cap: the crosshair should end up sitting on the enemy, however far
                // out it is. FitRadius below is what keeps it inside the window.
                radius = Mathf.Lerp(radius, Mathf.Max(40f, _lockRadius), pin);
            }

            _radiusPx = _radiusPx <= 0f ? radius : Mathf.Lerp(_radiusPx, radius, 1f - Mathf.Exp(-12f * dt));

            float radians = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            // Only ever shortens, and only in the corner cases the fixed radius cannot cover,
            // so the crosshair keeps the angle the player asked for instead of being squared
            // off against the window edge the way the game's own clamp would do it.
            float fitted = FitRadius(frame.Anchor, direction, _radiusPx);

            WriteAimPosition(inputManager, frame.Anchor + direction * fitted, dt);

            ReticleScreenPosition = inputManager.AimScreenPoint;
            HasReticle = true;
            return true;
        }

        // ------------------------------------------------------------------

        private bool TryBuildFrame(CharacterMainControl character, out AimFrame frame)
        {
            frame = default;
            if (character == null) return false;

            var levelManager = LevelManager.Instance;
            if (levelManager == null || levelManager.GameCamera == null) return false;

            var camera = levelManager.GameCamera.renderCamera;
            if (camera == null) return false;

            Vector3 origin = character.transform.position;
            Vector3 centre = origin + Vector3.up * 0.5f;

            if (!_initialized)
            {
                // Start pointing where the duck is already facing, so control is picked up
                // without the crosshair jumping.
                Vector2 anchor = camera.WorldToScreenPoint(centre);
                Vector2 ahead = camera.WorldToScreenPoint(centre + character.transform.forward * 4f);
                Vector2 facing = ahead - anchor;
                _aimAngle = facing.sqrMagnitude > 0.01f
                    ? Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg
                    : 90f;
                _initialized = true;
            }

            frame = new AimFrame
            {
                Camera = camera,
                Origin = origin,
                Anchor = camera.WorldToScreenPoint(centre),
                AimAngle = _aimAngle + _assistOffset
            };
            return true;
        }

        /// <summary>
        /// Move the player's own aim angle toward wherever the stick points.
        ///
        /// Full deflection still means "point there" — this stays absolute aiming. What the
        /// deflection buys below full is time: the turn rate scales down as the stick returns
        /// to centre, so a light push walks the crosshair across a target instead of throwing
        /// it, which is the whole of fine adjustment on a stick.
        /// </summary>
        private void UpdatePlayerAngle(in AimFrame frame, PadConfig.AimSettings aim, Vector2 stick, float magnitude, bool adsHeld, float dt)
        {
            float previous = _aimAngle;

            if (magnitude > 0.0001f)
            {
                float stickAngle = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
                float target = string.Equals(aim.Mode, "classic", StringComparison.OrdinalIgnoreCase)
                    ? WorldAngleToScreen(frame, stickAngle)
                    : stickAngle;

                // Floored, and not only for taste: a response of zero means "instant" to
                // ApproachAngle, so letting the combined scale reach zero would teleport the
                // crosshair rather than freeze it — the exact opposite of what full precision,
                // full friction and full ADS damping are asking for.
                float scale = Mathf.Max(0.02f,
                    AimMath.PrecisionScale(magnitude, aim.PrecisionRange)
                    * (adsHeld ? Mathf.Clamp(aim.AdsPrecision, 0.1f, 1f) : 1f)
                    * (1f - _friction));

                _aimAngle = AimMath.ApproachAngle(_aimAngle, target,
                    aim.TurnResponse * scale, aim.MaxTurnSpeed * scale, dt);
            }
            // Stick released: the duck keeps facing the last direction, which is what
            // players expect from a twin-stick shooter.

            // Assist reads this to tell a deliberate sweep from a tracking adjustment. Lightly
            // smoothed, because a raw per-frame rate is noisy enough to flicker the pull.
            float instant = Mathf.Abs(AimMath.DeltaAngle(previous, _aimAngle)) / dt;
            _turnRate = Mathf.Lerp(_turnRate, instant, 1f - Mathf.Exp(-16f * dt));
        }

        /// <summary>
        /// Aim assist, as friction plus a bounded pull rather than a blend onto the target.
        ///
        /// The old version slerped the aim direction a fixed fraction of the way onto whatever
        /// the cone found, which is what made it feel cheap: at any strength worth having it
        /// simply took the aim off the player, and it snapped from one enemy to the next.
        /// Friction — quietly slowing the turn rate while the crosshair is near a target — does
        /// most of the work of making enemies easy to stay on without ever moving the aim on
        /// the player's behalf. On top of that sits a pull that accumulates into a single
        /// offset angle, capped at a few degrees and unwound as soon as the target leaves the
        /// wedge, so it can track a strafing enemy but can never point somewhere the player
        /// did not.
        /// </summary>
        private void UpdateAssist(in AimFrame frame, bool adsHeld, bool firing, float dt)
        {
            var settings = _config.AimAssist;

            if (!settings.Enabled || settings.Strength <= 0f)
            {
                ReleaseAssist(settings, settings.Enabled ? "strength is 0" : "disabled", dt);
                return;
            }

            if (settings.OnlyWhileFiringOrAds && !adsHeld && !firing)
            {
                ReleaseAssist(settings, "waiting for fire/ADS", dt);
                return;
            }

            // While locked on, the lock owns the crosshair; a second source of pull underneath
            // it would only fight the ease-in.
            if (_lockTarget != null)
            {
                ReleaseAssist(settings, "lock-on has the aim", dt);
                return;
            }

            var result = _finder.Find(frame, settings.MaxDistance, settings.MaxAngleDegrees,
                settings.RequireLineOfSight, _assistTarget, settings.StickinessDegrees);

            if (!result.Found)
            {
                ReleaseAssist(settings, _finder.LastFailureReason ?? "no target", dt);
                return;
            }

            _assistTarget = result.Transform;
            _assistScreenRadius = result.ScreenRadius;
            AssistTargetName = result.Transform.name;
            AssistAngle = result.Angle;
            AssistStatus = "assisting";

            float strength = Mathf.Clamp01(settings.Strength);
            float falloff = AimMath.ConeFalloff(result.Angle, result.EffectiveCone);
            AssistWeight = falloff * strength;

            // A deliberate sweep is never fought: past FlickDegreesPerSecond the pull steps
            // aside and the drag eases off too, so crossing a crowd on the way to a different
            // enemy does not feel like wading. The pull gives up much more of itself than the
            // friction does — the friction is the half that never aims for the player.
            float flick = settings.FlickDegreesPerSecond > 0f
                ? Mathf.Clamp01(_turnRate / settings.FlickDegreesPerSecond)
                : 0f;
            float authority = 1f - 0.85f * flick;

            // Capped short of 1: friction scales the player's own turn rate, so letting it
            // reach full would leave the stick unable to pull the crosshair off an enemy at all.
            _friction = Mathf.Clamp(
                settings.Friction * AssistWeight * (adsHeld ? 1.15f : 1f) * (1f - 0.5f * flick),
                0f, 0.85f);

            float error = AimMath.DeltaAngle(frame.AimAngle, result.ScreenAngle);
            float desired = AimMath.Clamp(_assistOffset + error, -settings.MaxPullDegrees, settings.MaxPullDegrees);
            _assistOffset = AimMath.MoveTowards(_assistOffset, desired,
                settings.MagnetDegreesPerSecond * AssistWeight * authority * dt);
        }

        private void ReleaseAssist(PadConfig.AimAssistSettings settings, string status, float dt)
        {
            _assistTarget = null;
            _assistScreenRadius = 0f;
            AssistTargetName = "none";
            AssistWeight = 0f;
            AssistStatus = status;
            _friction = 0f;
            _assistOffset = AimMath.MoveTowards(_assistOffset, 0f, settings.ReleaseDegreesPerSecond * dt);
        }

        /// <summary>
        /// Keep the lock-on blend up to date and report how far the stick is leading the target.
        ///
        /// The lock no longer teleports the crosshair: it drives a weight that eases on over
        /// EngageTime and off over ReleaseTime, and the angle and radius it eases toward are
        /// held after the target is dropped so the hand-back is just as smooth as the grab.
        /// </summary>
        private float UpdateLock(in AimFrame frame, float magnitude, float dt)
        {
            var snap = _config.AimSnap;
            bool valid = snap.Enabled && _lockTarget != null && IsValidTarget(_lockTarget);

            // Held past the drop so the outline can fade out with the crosshair rather than
            // vanishing the instant the lock is let go.
            if (_lockTarget != null) _lockVisual = _lockTarget;

            if (valid)
            {
                Vector3 point = TargetPoint(_lockTarget);
                Vector3 offset = point - frame.Origin;
                offset.y = 0f;
                float flat = offset.magnitude;

                if (flat < 0.3f || flat > snap.MaxDistance * 1.25f) valid = false;
                else if (snap.RequireLineOfSight && !TargetFinder.HasLineOfSight(frame.Origin, point)) valid = false;
                else
                {
                    Vector3 screen = frame.Camera.WorldToScreenPoint(point);
                    Vector2 screenOffset = (Vector2)screen - frame.Anchor;
                    if (screen.z <= 0f || screenOffset.sqrMagnitude < 1f)
                    {
                        valid = false;
                    }
                    else
                    {
                        _lockPoint = point;
                        _lockAngle = Mathf.Atan2(screenOffset.y, screenOffset.x) * Mathf.Rad2Deg;
                        _lockRadius = screenOffset.magnitude;
                    }
                }
            }

            float lead = 0f;

            if (valid)
            {
                float away = AimMath.DeltaAngle(_lockAngle, _aimAngle);

                bool pushingAway = snap.BreakOnStickInput && Pad.RightStickRaw.magnitude > 0.7f &&
                                   Mathf.Abs(away) > snap.BreakAngleDegrees;

                // Latched to the moment the push starts. Without that, a held stick re-fires
                // every frame and one flick spins the lock through every enemy in the room.
                if (!pushingAway) _lockBreakLatched = false;

                if (pushingAway && !_lockBreakLatched)
                {
                    _lockBreakLatched = true;

                    // A hard push past the break angle means "not this one" — hand it the next
                    // enemy that way if there is one, and only drop the lock if there isn't.
                    if (!snap.SwitchTargetOnFlick || !CycleLock(frame))
                    {
                        _lockTarget = null;
                        valid = false;
                    }
                }
                else if (!pushingAway && snap.LeadDegrees > 0f && magnitude > 0f)
                {
                    // Small stick pressure slides the crosshair around the target so a moving
                    // enemy can be led, instead of the stick doing nothing at all while locked.
                    lead = AimMath.Clamp(away, -snap.LeadDegrees, snap.LeadDegrees) * magnitude;
                }

                // With the stick at rest, walk the player's own angle onto the target as well,
                // so releasing the lock hands control back where the crosshair already is.
                if (valid && magnitude < 0.2f)
                    _aimAngle = AimMath.ApproachAngle(_aimAngle, _lockAngle, 6f, 540f, dt);
            }

            if (!valid)
            {
                _lockTarget = null;
                _lockBreakLatched = false;
            }

            float engage = snap.EngageTime > 0f ? dt / snap.EngageTime : 1f;
            float release = snap.ReleaseTime > 0f ? dt / snap.ReleaseTime : 1f;
            _lockWeight = AimMath.MoveTowards(_lockWeight, valid ? 1f : 0f, valid ? engage : release);

            return lead;
        }

        /// <summary>
        /// Swap the lock to the next enemy on the side the stick was flicked toward. Searching
        /// from the player's own angle rather than the crosshair is what makes that directional:
        /// the stick is already pointing where the flick went.
        /// </summary>
        private bool CycleLock(in AimFrame frame)
        {
            var snap = _config.AimSnap;
            var search = frame;
            search.AimAngle = _aimAngle;

            var result = _finder.Find(search, snap.MaxDistance, snap.MaxAngleDegrees,
                snap.RequireLineOfSight, null, 0f, _lockTarget);

            if (!result.Found) return false;

            _lockTarget = result.Transform;
            _lockPoint = result.Point;
            _lockAngle = result.ScreenAngle;
            _lockRadius = result.ScreenRadius;
            return true;
        }

        // ------------------------------------------------------------------

        private float MaxRadius()
            => Mathf.Max(48f, Mathf.Min(Screen.width, Screen.height) * 0.5f - ScreenMargin);

        /// <summary>
        /// How far from the duck the crosshair sits, in pixels — the same in every direction,
        /// which is the whole point of working in screen space.
        /// </summary>
        private float DesiredRadius(PadConfig.AimSettings aim, bool adsHeld)
        {
            float max = MaxRadius();
            float radius = Mathf.Clamp01(aim.Reach) * max;
            if (adsHeld) radius *= Mathf.Max(0.2f, aim.AdsReachMultiplier);
            radius = Mathf.Clamp(radius, 48f, max);

            // Settle the crosshair onto the enemy it is helping with, so the help is visible
            // rather than mysterious. Bounded either side of the resting reach so this can
            // never turn back into the crosshair pumping in and out as targets come and go.
            float depth = Mathf.Clamp01(_config.AimAssist.DepthPull);
            if (depth > 0f && AssistWeight > 0f && _assistScreenRadius > 0f)
            {
                float pulled = Mathf.Clamp(_assistScreenRadius, radius * 0.6f, Mathf.Min(radius * 1.6f, max));
                radius = Mathf.Lerp(radius, pulled, AssistWeight * depth);
            }

            return radius;
        }

        /// <summary>
        /// Largest radius along <paramref name="direction"/> that still lands inside the window.
        /// Closed form rather than the binary search this used to do, so it costs nothing and
        /// varies smoothly with the aim angle instead of stepping.
        /// </summary>
        private static float FitRadius(Vector2 anchor, Vector2 direction, float radius)
        {
            float limit = Mathf.Min(
                SlabLimit(anchor.x, direction.x, ScreenMargin, Screen.width - ScreenMargin),
                SlabLimit(anchor.y, direction.y, ScreenMargin, Screen.height - ScreenMargin));
            return Mathf.Max(0f, Mathf.Min(radius, limit));
        }

        private static float SlabLimit(float origin, float direction, float min, float max)
        {
            // Already outside this slab: the duck is off-screen, so let the game clamp rather
            // than collapsing the reticle onto it.
            if (origin < min || origin > max) return float.MaxValue;
            if (Mathf.Abs(direction) < 1e-5f) return float.MaxValue;
            return direction > 0f ? (max - origin) / direction : (min - origin) / direction;
        }

        /// <summary>
        /// Camera-relative flattened basis — the same one the game uses to turn movement stick
        /// input into world motion — expressed as the screen angle it lands on. Classic mode
        /// aims by world heading; everything downstream still works in screen degrees.
        /// </summary>
        private static float WorldAngleToScreen(in AimFrame frame, float worldAngleDegrees)
        {
            var camera = frame.Camera;

            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = camera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            float radians = worldAngleDegrees * Mathf.Deg2Rad;
            Vector3 direction = right * Mathf.Cos(radians) + forward * Mathf.Sin(radians);
            return frame.ScreenAngleTo(frame.Origin + Vector3.up * 0.5f + direction * 6f);
        }

        /// <summary>
        /// Steam Input trackpads and gyro emit mouse movement alongside a pad. Preserve their
        /// cursor until the player deliberately uses the stick again.
        /// </summary>
        private bool UpdatePointer(in AimFrame frame, Vector2 stick, float dt, out Vector2 point)
        {
            point = default;

            if (!ControllerDevice.UsePointer) { _pointerAim = false; return false; }
            if (stick.sqrMagnitude > 0.01f) { _pointerAim = false; return false; }

            Vector2 delta = ControllerDevice.PointerDelta;
            if (delta.sqrMagnitude > 0.001f)
            {
                Vector2 start = _pointerAim || ReticleScreenPosition.sqrMagnitude > 1f
                    ? (_pointerAim ? _pointerScreen : ReticleScreenPosition)
                    : frame.Anchor;

                _pointerScreen = start + delta * _config.Aim.PointerSensitivity;
                _pointerScreen.x = Mathf.Clamp(_pointerScreen.x, ScreenMargin, Screen.width - ScreenMargin);
                _pointerScreen.y = Mathf.Clamp(_pointerScreen.y, ScreenMargin, Screen.height - ScreenMargin);
                _pointerAim = true;
                ClearLock();
                _lockWeight = 0f;
            }

            if (!_pointerAim) return false;

            _assistTarget = null;
            AssistWeight = 0f;
            _friction = 0f;
            _assistOffset = AimMath.MoveTowards(_assistOffset, 0f, 360f * dt);
            AssistStatus = "trackpad / gyro precision";

            // Keep the stick angle in step with the pointer so handing control back to the
            // stick does not throw the crosshair across the screen.
            Vector2 offset = _pointerScreen - frame.Anchor;
            if (offset.sqrMagnitude > 1f)
            {
                _aimAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                _radiusPx = offset.magnitude;
            }

            point = _pointerScreen;
            return true;
        }

        private static bool IsValidTarget(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            var receiver = target.GetComponent<DamageReceiver>();
            if (receiver == null) receiver = target.GetComponentInParent<DamageReceiver>();
            if (receiver == null) return false;

            return !receiver.IsDead && !receiver.isHalfObsticle && Team.IsEnemy(Teams.player, receiver.Team);
        }

        private static Vector3 TargetPoint(Transform target)
        {
            var collider = target.GetComponent<Collider>();
            return collider != null ? collider.bounds.center : target.position;
        }

        /// <summary>Mouse-like mode: the stick nudges the aim point instead of pointing at it.</summary>
        private bool UpdateRelative(InputManager inputManager, Vector2 stick, bool adsHeld, float deltaTime)
        {
            float speed = _config.Aim.RelativeSensitivity * (adsHeld ? _config.Aim.RelativeAdsMultiplier : 1f);
            Vector2 delta = stick * speed * deltaTime;
            if (ControllerDevice.UsePointer) delta += ControllerDevice.PointerDelta * _config.Aim.PointerSensitivity;

            // Hand the delta to the vanilla path, which applies the game's own
            // mouse sensitivity, recoil and clamping.
            inputManager.SetAimInputUsingMouse(delta);
            ReticleScreenPosition = inputManager.AimScreenPoint;
            HasReticle = true;
            return true;
        }

        private void WriteAimPosition(InputManager inputManager, Vector2 screenPoint, float dt)
        {
            if (AimCacheField == null) return;

            bool keepRecoil = _config.Aim.PreserveRecoil;
            Vector2 written = keepRecoil ? screenPoint + _recoilOffset : screenPoint;

            // Mark the cache as synced first, or the getter/setter will overwrite our
            // value with the real mouse position the first time it is touched.
            AimSyncedField?.SetValue(inputManager, true);
            AimCacheField.SetValue(inputManager, written);

            inputManager.SetMousePosition(written);

            // Zero delta: everything downstream (recoil, obstacle sweep, head aim,
            // SetAimPoint on the character) runs exactly as it does for mouse players.
            inputManager.SetAimInputUsingMouse(Vector2.zero);

            if (!keepRecoil)
            {
                _recoilOffset = Vector2.zero;
                return;
            }

            // Writing an absolute aim point every frame would otherwise erase the weapon kick
            // before the player ever saw it: the game adds one frame of recoil to whatever is
            // in the cache, and we overwrite the cache next frame. Reading the difference back
            // out and carrying it lets a burst climb and settle the way it does for a mouse.
            Vector2 kick = ReadAimCache(inputManager) - written;
            float maxRecoil = Screen.height * 0.25f;

            if (kick.sqrMagnitude < 1e-6f)
            {
                _recoilIdle += dt;
                // Nothing is driving recovery any more (weapon holstered or swapped mid-burst),
                // so unwind what is left rather than leaving the crosshair permanently offset.
                if (_recoilIdle > 0.35f)
                    _recoilOffset = Vector2.MoveTowards(_recoilOffset, Vector2.zero, Screen.height * 0.8f * dt);
                return;
            }

            _recoilIdle = 0f;

            // A jump this large is the game's window clamp, not a weapon: carrying it would
            // pin the crosshair to the edge.
            if (kick.sqrMagnitude > maxRecoil * maxRecoil) _recoilOffset = Vector2.zero;
            else _recoilOffset = Vector2.ClampMagnitude(_recoilOffset + kick, maxRecoil);
        }

        private static Vector2 ReadAimCache(InputManager inputManager)
        {
            try
            {
                return (Vector2)AimCacheField.GetValue(inputManager);
            }
            catch (Exception)
            {
                return Vector2.zero;
            }
        }
    }
}
