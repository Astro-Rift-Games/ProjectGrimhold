#if UNITY_EDITOR
using System;
using System.Collections.Generic;


    public enum AttackPreviewOutcome { Pass, Warning, Fail }
    /// <summary>Ranges merge consecutive sampled violations by id/hand/severity, never across a clean frame.
    /// WorstChecks ranks violations first, then severity, then magnitude per id/hand. Outcome retains the highest severity.
    /// Grid: i/rate below end, plus exact end once; end = Duration + TailSeconds.</summary>
    public sealed class AttackPreviewReport
    {
        private readonly List<AttackPreviewCheck> _worst = new List<AttackPreviewCheck>();
        private readonly List<AttackPreviewFailureRange> _ranges = new List<AttackPreviewFailureRange>();
        private Dictionary<(AttackPreviewCheckId, AttackPreviewHand, AttackPreviewSeverity), AttackPreviewFailureRange> _active =
            new Dictionary<(AttackPreviewCheckId, AttackPreviewHand, AttackPreviewSeverity), AttackPreviewFailureRange>();
        public AttackPreviewCombination Combination { get; }
        public AttackPreviewOutcome Outcome { get; private set; }
        public int SampleCount { get; private set; }
        public float EndSeconds { get; private set; }
        public IReadOnlyList<AttackPreviewCheck> WorstChecks { get; }
        public IReadOnlyList<AttackPreviewFailureRange> FailureRanges { get; }
        public AttackPreviewReport(AttackPreviewCombination combination)
        {
            Combination = combination ?? throw new ArgumentNullException(nameof(combination));
            WorstChecks = _worst.AsReadOnly();
            FailureRanges = _ranges.AsReadOnly();
        }

        private static bool IsWorse(AttackPreviewCheck candidate, AttackPreviewCheck current)
        {
            if (candidate.IsViolation != current.IsViolation) return candidate.IsViolation;
            if (candidate.IsViolation && candidate.Severity != current.Severity) return candidate.Severity > current.Severity;
            return candidate.MeasuredValue > current.MeasuredValue;
        }

        public void AddSample(float seconds, IReadOnlyList<AttackPreviewCheck> observations)
        {
            if (!float.IsFinite(seconds) || seconds < 0 || (SampleCount > 0 && seconds <= EndSeconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), "Samples must be finite, nonnegative and strictly increasing.");
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            var next = new Dictionary<(AttackPreviewCheckId, AttackPreviewHand, AttackPreviewSeverity), AttackPreviewFailureRange>();
            foreach (var check in observations)
            {
                int index = _worst.FindIndex(c => c.Id == check.Id && c.Hand == check.Hand);
                if (index < 0) _worst.Add(check);
                else if (IsWorse(check, _worst[index])) _worst[index] = check;
                if (!check.IsViolation) continue;
                var key = (check.Id, check.Hand, check.Severity);
                if (!next.ContainsKey(key))
                {
                    if (_active.TryGetValue(key, out var range)) range.EndSeconds = seconds;
                    else { range = new AttackPreviewFailureRange(check, seconds); _ranges.Add(range); }
                    next.Add(key, range);
                }
                var outcome = check.Severity == AttackPreviewSeverity.Error ? AttackPreviewOutcome.Fail : AttackPreviewOutcome.Warning;
                if (outcome > Outcome) Outcome = outcome;
            }
            _active = next;
            SampleCount++;
            EndSeconds = seconds;
        }
    }
#endif
