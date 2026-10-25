#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;


    /// <summary>Core has no progress UI. Batch callback runs before each cell; false cancels
    /// without evaluating that cell. Returns completed reports only; owns/disposes batch resources.
    /// Defend is sustained and exempt from idle-return and missing-attack-VFX checks.</summary>
    public sealed class AttackPreviewValidator
    {
        private readonly AttackPreviewCheckSettings _settings;
        public AttackPreviewValidator(AttackPreviewCheckSettings settings = null)
        {
            _settings = settings ?? new AttackPreviewCheckSettings();
            _settings.Validate();
        }

        public AttackPreviewReport Validate(AttackPreviewRig rig, AttackPreviewCombination combination)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));
            if (combination == null) throw new ArgumentNullException(nameof(combination));
            _settings.Validate();
            using var checks = new AttackPreviewChecks(_settings);
            return Validate(rig, combination, checks);
        }

        private AttackPreviewReport Validate(AttackPreviewRig rig, AttackPreviewCombination combination, AttackPreviewChecks checks)
        {
            var report = new AttackPreviewReport(combination);
            if (combination.PresentationError != null)
            {
                report.AddSample(0, new[] { new AttackPreviewCheck(AttackPreviewCheckId.PresentationConfiguration,
                    AttackPreviewSeverity.Error, combination.ConfigurationHand, 0, 1, 0, combination.PresentationError) });
                return report;
            }
            rig.Configure(combination, sequential: true);
            using var source = new AttackPreviewFrameSource(rig, combination);
            bool isAttack = combination.Action != AttackPreviewAction.Defend;
            var attackingLoot = combination.Action == AttackPreviewAction.MainAttack ? combination.MainHand : combination.OffHand;
            var weapon = attackingLoot != null ? attackingLoot.WeaponDefinition : null;
            bool expectsVfx = isAttack && weapon != null && weapon.Presentation.AttackVfx != null;
            bool sawVfx = false;
            var slashSet = weapon != null ? weapon.Presentation.AttackAnimationSet : null;
            bool slash = isAttack && slashSet != null && slashSet.OffHandStroke == OffHandStroke.Mirrored;
            Vector2 window = slashSet != null ? slashSet.SlashStrokeWindow : Vector2.zero;
            var arcGrips = new List<Vector2>(); var arcTips = new List<Vector2>(); var arcVfx = new List<Vector2>();
            Vector2 tip = Vector2.zero;
            if (slash)
            {
                using var data = new UnityEditor.SerializedObject(weapon);
                tip = data.FindProperty("_presentation").FindPropertyRelative("_bladeTip").vector2Value;
            }
            Vector2 facing = PlayerWeaponPresentationMath.CanonicalFacing(combination.FacingIndex);
            Vector2 right = new Vector2(facing.y, -facing.x);
            Vector2 BodyPoint(Vector3 world)
            {
                Vector2 local = rig.VisualRoot.InverseTransformPoint(world);
                return new Vector2(Vector2.Dot(local, facing), Vector2.Dot(local, right));
            }
            float end = rig.Duration + _settings.TailSeconds;
            if (!float.IsFinite(end) || end * _settings.SampleRate >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(_settings.SampleRate), "Preview sample count is too large.");
            int intervals = Mathf.CeilToInt(end * _settings.SampleRate);
            var times = new SortedSet<float>();
            for (int sample = 0; sample <= intervals; sample++) times.Add(sample == intervals ? end : sample / _settings.SampleRate);
            if (slash && slashSet.CanonicalSlash) { times.Add(window.x); times.Add(window.y); }
            foreach (float seconds in times)
            {
                rig.Evaluate(seconds);
                var observations = new List<AttackPreviewCheck>();
                for (int hand = 0; hand < 2; hand++)
                {
                    var frame = source.Capture(hand, seconds, isAttack && seconds > rig.Duration);
                    sawVfx |= AttackPreviewChecks.Visible(frame.VfxRenderer);
                    checks.Evaluate(frame);
                    observations.AddRange(checks.Measurements);
                    bool attacker = hand == (combination.Action == AttackPreviewAction.OffAttack ? 1 : 0);
                    if (slash && attacker && seconds >= window.x && seconds <= window.y)
                    {
                        arcGrips.Add(BodyPoint(frame.Grip.position));
                        arcTips.Add(BodyPoint(frame.WeaponRenderer.transform.TransformPoint(tip)));
                        if (AttackPreviewChecks.Visible(frame.VfxRenderer))
                            arcVfx.Add(BodyPoint(checks.DrawnHandWorldPoint(frame.VfxRenderer)));
                    }
                }
                if (seconds == end && slash)
                {
                    int hand = combination.Action == AttackPreviewAction.OffAttack ? 1 : 0;
                    var sprite = rig.HandRenderers[hand].sprite;
                    float epsilon = sprite != null ? 2f / sprite.pixelsPerUnit : 2f / 32f;
                    observations.Add(AttackPreviewChecks.CheckSlashArc(arcGrips, arcTips, arcVfx,
                        hand == 1 ? AttackPreviewHand.Off : AttackPreviewHand.Main, epsilon, seconds));
                }
                if (seconds == end && expectsVfx && !sawVfx)
                    observations.Add(new AttackPreviewCheck(AttackPreviewCheckId.VfxOrigin, AttackPreviewSeverity.Warning,
                        combination.Action == AttackPreviewAction.OffAttack ? AttackPreviewHand.Off : AttackPreviewHand.Main,
                        seconds, 1, 0, "Configured attack VFX never appeared during the sampled attack (presence check)."));
                report.AddSample(seconds, observations);
            }
            return report;
        }

        public IReadOnlyList<AttackPreviewReport> ValidateAll(IReadOnlyList<AttackPreviewCombination> combinations,
            Func<int, int, bool> continueValidation = null)
        {
            if (combinations == null) throw new ArgumentNullException(nameof(combinations));
            _settings.Validate();
            var reports = new List<AttackPreviewReport>();
            // Defer scene creation until the first callback approves work. A cancelled batch owns no scene.
            AttackPreviewRig rig = null;
            using var checks = new AttackPreviewChecks(_settings);
            try
            {
                for (int index = 0; index < combinations.Count; index++)
                {
                    if (continueValidation != null && !continueValidation(index, combinations.Count)) break;
                    rig ??= new AttackPreviewRig();
                    reports.Add(Validate(rig, combinations[index], checks));
                }
            }
            finally { rig?.Dispose(); }
            return reports.AsReadOnly();
        }
    }
#endif
