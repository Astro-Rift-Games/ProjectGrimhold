#if UNITY_EDITOR

    public sealed class AttackPreviewCheckSettings
    {
        public float PivotWarningPixels { get; set; } = 1.5f;
        public float PivotErrorPixels { get; set; } = 3f;
        public float IdleTolerancePixels { get; set; } = 1.5f;
        public float IdleRotationToleranceDegrees { get; set; } = 0.1f;
        public float VfxTolerancePixels { get; set; } = 1.5f;
        public float OcclusionWarningFraction { get; set; } = 0.25f;
        public float OcclusionErrorFraction { get; set; } = 0.5f;
        public float SampleRate { get; set; } = 60f;
        public float TailSeconds { get; set; } = 0.1f;

        internal void Validate()
        {
            RequireNonNegative(PivotWarningPixels, nameof(PivotWarningPixels));
            RequireNonNegative(PivotErrorPixels, nameof(PivotErrorPixels));
            RequireNonNegative(IdleTolerancePixels, nameof(IdleTolerancePixels));
            RequireNonNegative(IdleRotationToleranceDegrees, nameof(IdleRotationToleranceDegrees));
            RequireNonNegative(VfxTolerancePixels, nameof(VfxTolerancePixels));
            RequireNonNegative(OcclusionWarningFraction, nameof(OcclusionWarningFraction));
            RequireNonNegative(OcclusionErrorFraction, nameof(OcclusionErrorFraction));
            if (OcclusionErrorFraction > 1f || OcclusionWarningFraction > OcclusionErrorFraction)
                throw new System.ArgumentException("Occlusion fractions require 0 <= warning <= error <= 1.");
            RequireNonNegative(TailSeconds, nameof(TailSeconds));
            if (!float.IsFinite(SampleRate) || SampleRate <= 0f)
                throw new System.ArgumentOutOfRangeException(nameof(SampleRate));
            if (PivotErrorPixels < PivotWarningPixels)
                throw new System.ArgumentException("Error tolerance must not be smaller than warning tolerance.");
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0f) throw new System.ArgumentOutOfRangeException(name);
        }
    }
#endif
