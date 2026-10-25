#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;


    /// <summary>Owns cached alpha centroids, not source sprites/textures. GPU readback resources
    /// are released immediately, including on failure. All distance diagnostics use holding-hand pixels.</summary>
    public sealed class AttackPreviewChecks : IDisposable
    {
        private readonly AttackPreviewCheckSettings _settings;
        private readonly Dictionary<Sprite, SpritePixels> _pixels = new Dictionary<Sprite, SpritePixels>();

        private sealed class SpritePixels
        {
            public readonly int Width, Height;
            public readonly float[] Alpha;
            public readonly Vector2[] OpaquePoints;
            public readonly Vector2 Centroid;
            public SpritePixels(Sprite sprite, Color[] colors, int width, int height)
            {
                Width = width; Height = height;
                Alpha = new float[colors.Length];
                var points = new List<Vector2>();
                double weight = 0, xSum = 0, ySum = 0;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float alpha = colors[y * width + x].a;
                    Alpha[y * width + x] = alpha;
                    if (alpha <= 0f) continue;
                    var center = new Vector2(x + 0.5f, y + 0.5f);
                    points.Add((center - sprite.pivot) / sprite.pixelsPerUnit);
                    weight += alpha;
                    xSum += center.x * alpha; ySum += center.y * alpha;
                }
                OpaquePoints = points.ToArray();
                // Transparent sprites have no drawn hand: the evaluator reports a diagnostic rather
                // than crashing a whole batch. The explicit centroid helper rejects that input.
                Centroid = weight > 0 ? (new Vector2((float)(xSum / weight), (float)(ySum / weight)) -
                    sprite.pivot) / sprite.pixelsPerUnit : Vector2.zero;
            }
        }
        private bool _disposed;
        private readonly List<AttackPreviewCheck> _measurements = new List<AttackPreviewCheck>();
        /// <summary>Observations from the most recent frame, including successful applicable checks.</summary>
        public IReadOnlyList<AttackPreviewCheck> Measurements { get; }

        public AttackPreviewChecks(AttackPreviewCheckSettings settings = null)
        {
            _settings = settings ?? new AttackPreviewCheckSettings();
            _settings.Validate();
            Measurements = _measurements.AsReadOnly();
        }

        /// <summary>Inputs are pivot-relative (forward,right), not screen coordinates. Near-pivot
        /// samples cannot define an angle. Blade and sprite-centroid motion must have the same sense.</summary>
        public static AttackPreviewCheck CheckSlashArc(IReadOnlyList<Vector2> grips, IReadOnlyList<Vector2> tips,
            IReadOnlyList<Vector2> vfx, AttackPreviewHand hand, float epsilon, float seconds)
        {
            float Sweep(IReadOnlyList<Vector2> points)
            {
                bool hasPrevious = false; float previous = 0, sum = 0;
                foreach (var point in points)
                {
                    if (point.magnitude <= epsilon) continue;
                    float angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
                    if (hasPrevious) sum += Mathf.DeltaAngle(previous, angle);
                    previous = angle; hasPrevious = true;
                }
                return sum;
            }
            float sign = hand == AttackPreviewHand.Off ? 1 : -1;
            float sweep = Sweep(grips), bladeSweep = Sweep(tips), vfxSweep = Sweep(vfx);
            bool forward = false;
            foreach (var p in grips) forward |= p.x > epsilon;
            bool valid = grips.Count >= 3 && tips.Count >= 3 && vfx.Count >= 2;
            if (valid)
            {
                Vector2 start = grips[0], end = grips[grips.Count - 1];
                valid = start.x < -epsilon && start.y * sign < -epsilon && end.y * sign > epsilon && forward &&
                    Mathf.Abs(sweep - sign * 180) <= 30 && bladeSweep * sign > 0 && vfxSweep * sign > 0;
            }
            return new AttackPreviewCheck(AttackPreviewCheckId.SlashArcContract, AttackPreviewSeverity.Error,
                hand, seconds, sweep, 30,
                $"Body-frame slash: grip sweep {sweep:F1}°, blade {bladeSweep:F1}°, VFX {vfxSweep:F1}°; " +
                $"expected {sign * 180:F0}° ±30°, back-own-side to opposite-side through forward; epsilon {epsilon:F4}.", !valid);
        }

        public Vector3 DrawnHandWorldPoint(SpriteRenderer hand)
        {
            ThrowIfDisposed();
            if (hand == null || hand.sprite == null) throw new ArgumentException("A hand sprite is required.", nameof(hand));
            var pixels = GetPixels(hand.sprite);
            if (pixels.OpaquePoints.Length == 0)
                throw new ArgumentException("Hand sprite has no opaque pixels.", nameof(hand));
            return hand.transform.TransformPoint(ApplyFlips(pixels.Centroid, hand));
        }

        public Vector3 WeaponGripWorldPoint(SpriteRenderer weapon, Vector2 gripPoint)
        {
            ThrowIfDisposed();
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            // GripPoint is already measured from the sprite pivot in sprite units. The presenter
            // applies -R*S*GripPoint, so do not convert it from texture pixels or subtract pivot again.
            return weapon.transform.TransformPoint(ApplyFlips(gripPoint, weapon));
        }

        private static Vector3 ApplyFlips(Vector2 point, SpriteRenderer renderer) =>
            new Vector3(renderer.flipX ? -point.x : point.x, renderer.flipY ? -point.y : point.y, 0f);

        private SpritePixels GetPixels(Sprite sprite)
        {
            if (_pixels.TryGetValue(sprite, out var pixels)) return pixels;
            pixels = ReadSpritePixels(sprite);
            _pixels.Add(sprite, pixels);
            return pixels;
        }

        private static SpritePixels ReadSpritePixels(Sprite sprite)
        {
            var source = sprite.texture;
            Texture2D readback = null;
            RenderTexture temporary = null;
            var previous = RenderTexture.active;
            try
            {
                Texture2D readable = source;
                if (!source.isReadable)
                {
                    temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readback = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
                    { hideFlags = HideFlags.HideAndDontSave };
                    readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                    readback.Apply(false, false);
                    readable = readback;
                }
                Rect rect = sprite.rect;
                int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                var pixels = readable.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
                return new SpritePixels(sprite, pixels, width, height);
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback != null) Object.DestroyImmediate(readback);
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        public IReadOnlyList<AttackPreviewCheck> Evaluate(AttackPreviewFrame frame)
        {
            ThrowIfDisposed();
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            _measurements.Clear();
            var results = new List<AttackPreviewCheck>();
            if (!frame.HasHeldItem)
            {
                // Idle restoration still applies to an empty hand, but it cannot own held-item checks.
                if (frame.IsAfterDuration && frame.HandRenderer != null && frame.HandRenderer.sprite != null && frame.Grip != null)
                    CheckIdle(results, frame);
                return results;
            }
            if (frame.HandRenderer == null || frame.HandRenderer.sprite == null || frame.Grip == null)
            {
                Add(results, frame, AttackPreviewCheckId.HandSpriteIdentity, AttackPreviewSeverity.Error,
                    1, 0, "Holding hand renderer, sprite or grip is missing.");
                return results;
            }
            if (GetPixels(frame.HandRenderer.sprite).OpaquePoints.Length == 0)
            {
                Add(results, frame, AttackPreviewCheckId.HandSpriteIdentity, AttackPreviewSeverity.Error,
                    1, 0, "Holding hand sprite has no drawn opaque pixels.");
                return results;
            }
            Vector3 drawn = DrawnHandWorldPoint(frame.HandRenderer);
            if (frame.CheckWeaponGrip && Visible(frame.WeaponRenderer))
                CheckPivot(results, frame, AttackPreviewCheckId.WeaponGripOnHand,
                    Pixels(frame, WeaponGripWorldPoint(frame.WeaponRenderer, frame.WeaponGripPoint) - drawn),
                    "Weapon sprite grip is displaced from the drawn holding hand.");
            if (frame.CheckGripTransform)
                CheckPivot(results, frame, AttackPreviewCheckId.GripTransformOnHand,
                    Pixels(frame, frame.Grip.position - drawn), "Grip transform is displaced from the drawn holding hand.");
            CheckIdentity(results, frame);
            if (frame.CheckHeldItemDepth)
            {
                CheckDepth(results, frame);
                CheckOcclusion(results, frame);
            }
            if (Visible(frame.VfxRenderer) && frame.ExpectedVfxWorldOrigin.HasValue)
            {
                float distance = Pixels(frame, frame.VfxRenderer.transform.position - frame.ExpectedVfxWorldOrigin.Value);
                if (distance > _settings.VfxTolerancePixels)
                    Add(results, frame, AttackPreviewCheckId.VfxOrigin, AttackPreviewSeverity.Warning,
                        distance, _settings.VfxTolerancePixels, "Release/cast VFX differs from its resolved authored or live-anchor pose (pixels).");
                else RecordPass(frame, AttackPreviewCheckId.VfxOrigin, distance, _settings.VfxTolerancePixels,
                    "Release/cast VFX matches its resolved pose (pixels).");
            }
            else if (Visible(frame.VfxRenderer))
            {
                float measured = Pixels(frame, frame.VfxRenderer.transform.position - frame.Grip.position);
                // BladeReach is in VisualRoot units; transform the world delta into that space before
                // comparing reach. Distance is reported in holding-hand pixels like the other checks.
                var root = frame.VisualRoot;
                float reachDistance = root != null
                    ? root.InverseTransformVector(frame.VfxRenderer.transform.position - frame.Grip.position).magnitude
                    : Vector3.Distance(frame.VfxRenderer.transform.position, frame.Grip.position);
                float toleranceWorld = _settings.VfxTolerancePixels / frame.HandRenderer.sprite.pixelsPerUnit;
                if (reachDistance > frame.BladeReach + toleranceWorld)
                    Add(results, frame, AttackPreviewCheckId.VfxOrigin, AttackPreviewSeverity.Warning,
                        measured, measured * (frame.BladeReach + toleranceWorld) / reachDistance,
                        "Attack VFX origin exceeds blade reach plus tolerance from the attacking grip.");
                else RecordPass(frame, AttackPreviewCheckId.VfxOrigin, measured,
                    reachDistance > 0 ? measured * (frame.BladeReach + toleranceWorld) / reachDistance :
                    (frame.BladeReach + toleranceWorld) * frame.HandRenderer.sprite.pixelsPerUnit,
                    "Attack VFX origin is within blade reach plus tolerance (pixels).");
            }
            if (frame.IsAfterDuration) CheckIdle(results, frame);
            return results;
        }

        private void CheckPivot(List<AttackPreviewCheck> results, AttackPreviewFrame frame,
            AttackPreviewCheckId id, float distance, string message)
        {
            if (distance > _settings.PivotErrorPixels)
                Add(results, frame, id, AttackPreviewSeverity.Error, distance, _settings.PivotErrorPixels, message);
            else if (distance > _settings.PivotWarningPixels)
                Add(results, frame, id, AttackPreviewSeverity.Warning, distance, _settings.PivotWarningPixels, message);
            else RecordPass(frame, id, distance, _settings.PivotWarningPixels, "Grip distance is within tolerance (pixels).");
        }

        private void CheckIdentity(List<AttackPreviewCheck> results, AttackPreviewFrame frame)
        {
            var expected = frame.ExpectedHandSprites;
            if (expected != null && expected.Count > 0)
            {
                for (int i = 0; i < expected.Count; i++)
                {
                    if (expected[i] != frame.HandRenderer.sprite) continue;
                    RecordPass(frame, AttackPreviewCheckId.HandSpriteIdentity, 0, 0, "Hand sprite matches its anatomical idle source.");
                    return;
                }
                Add(results, frame, AttackPreviewCheckId.HandSpriteIdentity, AttackPreviewSeverity.Error,
                    1, 0, "Holding hand sprite is not referenced by its anatomical hand's idle clips.");
                return;
            }
            string handName = frame.Hand == AttackPreviewHand.Main ? "RightHand" : "LeftHand";
            bool matches = frame.HandRenderer.sprite.name.IndexOf(handName, StringComparison.OrdinalIgnoreCase) >= 0;
            Add(results, frame, AttackPreviewCheckId.HandSpriteIdentity,
                matches ? AttackPreviewSeverity.Warning : AttackPreviewSeverity.Error, matches ? 0 : 1, 0,
                $"Idle clip sprite references unavailable; using name fallback for {handName} identity.");
        }

        private void CheckDepth(List<AttackPreviewCheck> results, AttackPreviewFrame frame)
        {
            if (!Visible(frame.WeaponRenderer) || frame.BodyRenderer == null) return;
            int weaponSide = CompareDepth(frame.WeaponRenderer, frame.BodyRenderer);
            int handSide = CompareDepth(frame.HandRenderer, frame.BodyRenderer);
            // Visual contract only: holding hand and weapon must be on the same side of the body.
            // Their relative order alone says nothing about fist visibility; measure opaque overlap separately.
            if (weaponSide != handSide)
                Add(results, frame, AttackPreviewCheckId.DepthOrder, AttackPreviewSeverity.Error, 1, 0,
                    "Weapon and holding hand are on opposite sides of the body.");
            else RecordPass(frame, AttackPreviewCheckId.DepthOrder, 0, 0, "Weapon and holding hand share the body's side.");
        }

        private void CheckOcclusion(List<AttackPreviewCheck> results, AttackPreviewFrame frame)
        {
            if (frame.HeldItemCategory != LootCategory.Weapon) return;
            var hand = frame.HandRenderer;
            var weapon = frame.WeaponRenderer;
            if (!Visible(hand) || !Visible(weapon) || hand.color.a <= 0f || weapon.color.a <= 0f) return;
            int depth = CompareDepth(weapon, hand);
            float fraction = 0f;
            if (depth >= 0)
            {
                var handPixels = GetPixels(hand.sprite);
                var weaponPixels = GetPixels(weapon.sprite);
                int covered = 0;
                foreach (Vector2 handPoint in handPixels.OpaquePoints)
                {
                    Vector3 world = hand.transform.TransformPoint(ApplyFlips(handPoint, hand));
                    Vector3 local = weapon.transform.InverseTransformPoint(world);
                    // Renderer flips reflect sprite coordinates about the sprite pivot, independently
                    // of transform scale. Undo those flips before looking up the weapon's texel.
                    float localX = weapon.flipX ? -local.x : local.x;
                    float localY = weapon.flipY ? -local.y : local.y;
                    int x = Mathf.FloorToInt(localX * weapon.sprite.pixelsPerUnit + weapon.sprite.pivot.x);
                    int y = Mathf.FloorToInt(localY * weapon.sprite.pixelsPerUnit + weapon.sprite.pivot.y);
                    if (x >= 0 && x < weaponPixels.Width && y >= 0 && y < weaponPixels.Height &&
                        weaponPixels.Alpha[y * weaponPixels.Width + x] > 0f) covered++;
                }
                // Each drawn hand texel contributes one world-space center sample. Transparent padding
                // contributes nothing; this is opaque-pixel coverage, NOT alpha-weighted centroid distance.
                fraction = (float)covered / handPixels.OpaquePoints.Length;
            }
            if (depth == 0 && fraction > 0f)
                Add(results, frame, AttackPreviewCheckId.HandOccludedByWeapon, AttackPreviewSeverity.Warning,
                    fraction, 0, "Hand and weapon have tied sorting layer/order; overlap exists but draw order is unknown (fraction).");
            else if (fraction > _settings.OcclusionErrorFraction)
                Add(results, frame, AttackPreviewCheckId.HandOccludedByWeapon, AttackPreviewSeverity.Error,
                    fraction, _settings.OcclusionErrorFraction, "Weapon hides too much of the drawn holding hand (opaque-pixel fraction).");
            else if (fraction > _settings.OcclusionWarningFraction)
                Add(results, frame, AttackPreviewCheckId.HandOccludedByWeapon, AttackPreviewSeverity.Warning,
                    fraction, _settings.OcclusionWarningFraction, "Weapon partially hides the drawn holding hand (opaque-pixel fraction).");
            else RecordPass(frame, AttackPreviewCheckId.HandOccludedByWeapon, fraction,
                _settings.OcclusionWarningFraction, "Drawn holding hand occlusion is within tolerance (opaque-pixel fraction).");
        }

        private static int CompareDepth(SpriteRenderer a, SpriteRenderer b)
        {
            int layer = SortingLayer.GetLayerValueFromID(a.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(b.sortingLayerID));
            return layer != 0 ? layer : a.sortingOrder.CompareTo(b.sortingOrder);
        }

        private void CheckIdle(List<AttackPreviewCheck> results, AttackPreviewFrame frame)
        {
            Transform hand = frame.HandRenderer.transform;
            float distance = Mathf.Max(
                Pixels(frame, hand.parent != null ? hand.parent.TransformVector(hand.localPosition - frame.NeutralHandLocalPosition) : hand.localPosition - frame.NeutralHandLocalPosition),
                Pixels(frame, frame.Grip.parent.TransformVector(frame.Grip.localPosition - frame.NeutralGripLocalPosition)));
            // A persistent secondary-grip pin intentionally replaces locomotion translation.
            // Its position is independently checked against the weapon's secondary grip above.
            if (frame.HasPinnedSupportIdle)
                distance = Pixels(frame, frame.Grip.parent.TransformVector(frame.Grip.localPosition - frame.NeutralGripLocalPosition));
            float angle = Mathf.Max(Quaternion.Angle(hand.localRotation, frame.NeutralHandLocalRotation),
                Quaternion.Angle(frame.Grip.localRotation, frame.NeutralGripLocalRotation));
            bool scaleChanged = Vector3.Distance(hand.localScale, frame.NeutralHandLocalScale) > 0.001f ||
                Vector3.Distance(frame.Grip.localScale, frame.NeutralGripLocalScale) > 0.001f;
            if (frame.HasNeutralPivot && hand.parent != null)
            {
                Transform pivot = hand.parent;
                Vector3 delta = pivot.localPosition - frame.NeutralPivotLocalPosition;
                distance = Mathf.Max(distance, Pixels(frame, pivot.parent != null ? pivot.parent.TransformVector(delta) : delta));
                angle = Mathf.Max(angle, Quaternion.Angle(pivot.localRotation, frame.NeutralPivotLocalRotation));
                scaleChanged |= Vector3.Distance(pivot.localScale, frame.NeutralPivotLocalScale) > 0.001f;
            }
            bool spriteChanged = frame.HandRenderer.sprite != frame.NeutralHandSprite;
            if (!frame.IsInIdleState)
                Add(results, frame, AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error,
                    1, 0, "Hand Animator layer is not in its idle state after the attack.");
            else if (distance > _settings.IdleTolerancePixels)
                Add(results, frame, AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error,
                    distance, _settings.IdleTolerancePixels, "Hand or grip has residual position after the attack (pixels).");
            else if (angle > _settings.IdleRotationToleranceDegrees)
                Add(results, frame, AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error,
                    angle, _settings.IdleRotationToleranceDegrees, "Hand or grip has residual rotation after the attack (degrees).");
            else if (scaleChanged)
                Add(results, frame, AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error, 1, 0,
                    "Hand, grip or pivot scale did not return to the current idle pose.");
            else if (spriteChanged)
                Add(results, frame, AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error, 1, 0,
                    "Hand sprite does not match the idle clip at the current Animator state time.");
            else if (frame.HasTransientOffHandOverlay)
                Add(results, frame, AttackPreviewCheckId.ReturnsToIdle, AttackPreviewSeverity.Error, 1, 0,
                    "Transient Off Hand overlay remains applied after the attack.");
            else RecordPass(frame, AttackPreviewCheckId.ReturnsToIdle, distance, _settings.IdleTolerancePixels,
                "Hand and grip match idle at the current Animator state time (position in pixels).");
        }

        private static float Pixels(AttackPreviewFrame frame, Vector3 worldDelta) =>
            frame.HandRenderer.transform.InverseTransformVector(worldDelta).magnitude * frame.HandRenderer.sprite.pixelsPerUnit;
        internal static bool Visible(SpriteRenderer renderer) => renderer != null && renderer.enabled &&
            renderer.gameObject.activeInHierarchy && renderer.sprite != null;
        private void Add(List<AttackPreviewCheck> results, AttackPreviewFrame frame, AttackPreviewCheckId id,
            AttackPreviewSeverity severity, float measured, float threshold, string message)
        {
            var check = new AttackPreviewCheck(id, severity, frame.Hand, frame.TimeSeconds, measured, threshold, message);
            results.Add(check);
            _measurements.Add(check);
        }
        private void RecordPass(AttackPreviewFrame frame, AttackPreviewCheckId id, float measured, float threshold, string message) =>
            _measurements.Add(new AttackPreviewCheck(id, AttackPreviewSeverity.Warning, frame.Hand,
                frame.TimeSeconds, measured, threshold, message, false));
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackPreviewChecks));
        }
        public void Dispose() { _pixels.Clear(); _measurements.Clear(); _disposed = true; }
    }
#endif
