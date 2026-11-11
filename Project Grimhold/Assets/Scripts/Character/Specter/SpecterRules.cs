using UnityEngine;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Reglas puras y deterministas de combate, incorporeidad y temporizaciones del Espectro (US-78 / TASK-272 / TASK-285).
    /// </summary>
    public static class SpecterRules
    {
        public const float ChannelDurationSeconds = 1.25f;
        public const float DetonationDurationSeconds = 0.3f;
        public const float RecoveryDurationSeconds = 1.5f;
        public const float FadeOutDurationSeconds = 0.4f;
        public const float InvisibleDurationSeconds = 0.5f;
        public const float FadeInDurationSeconds = 0.4f;
        public const float ReactiveDisplacementCooldownSeconds = 4.0f;
        public const float AttackCooldownSeconds = 3.0f;

        public const float ChannelDuration = ChannelDurationSeconds;
        public const float DetonationDuration = DetonationDurationSeconds;
        public const float RecoveryDuration = RecoveryDurationSeconds;
        public const float FadeOutDuration = FadeOutDurationSeconds;
        public const float HiddenDuration = InvisibleDurationSeconds;
        public const float FadeInDuration = FadeInDurationSeconds;
        public const float ReactiveDisplacementCooldown = ReactiveDisplacementCooldownSeconds;
        public const float AttackCooldown = AttackCooldownSeconds;

        public const float AttackRange = 5.0f;
        public const float AttackRadius = 1.5f;
        public const float DetonationRadius = AttackRadius;
        public const float TeleportMinRadius = 3.0f;
        public const float TeleportMaxRadius = 5.0f;
        public const float MinTeleportDistance = TeleportMinRadius;
        public const float MaxTeleportDistance = TeleportMaxRadius;
        public const float LeashRadius = 15.0f;
        public const float DetonationDamage = 25.0f;

        /// <summary>
        /// Determina si el estado actual es incorpóreo (inmune a daño físico).
        /// </summary>
        public static bool IsIncorporeal(SpecterStateType state) =>
            state == SpecterStateType.Reposo ||
            state == SpecterStateType.Persecucion ||
            state == SpecterStateType.Reaparicion;

        /// <summary>
        /// Determina si el estado actual está manifestado (físicamente vulnerable).
        /// </summary>
        public static bool IsManifested(SpecterStateType state) =>
            state == SpecterStateType.Canalizacion ||
            state == SpecterStateType.Detonacion ||
            state == SpecterStateType.Recuperacion;

        /// <summary>
        /// Determina si el espectro está oculto o desvaneciéndose (sin colisionador ni daño).
        /// </summary>
        public static bool IsHidden(SpecterStateType state) =>
            state == SpecterStateType.Desvanecimiento ||
            state == SpecterStateType.Desvanecido;

        /// <summary>
        /// Determina si el espectro es elegible para recibir daño físico.
        /// Sólo es vulnerable durante la canalización, detonación y los 1.5s de recuperación (§3).
        /// </summary>
        public static bool CanReceivePhysicalDamage(SpecterStateType state) =>
            IsManifested(state);

        /// <summary>
        /// Determina si el espectro es elegible para recibir daño mágico.
        /// Afecta en todos los estados visibles; inmune durante Desvanecimiento, Desvanecido o Muerte (§3, TASK-285).
        /// </summary>
        public static bool CanReceiveMagicalDamage(SpecterStateType state) =>
            state != SpecterStateType.Desvanecimiento &&
            state != SpecterStateType.Desvanecido &&
            state != SpecterStateType.Muerte;

        /// <summary>
        /// Determina si la entidad puede procesar daño en general.
        /// Inmune durante Desvanecimiento, Desvanecido o Muerte (§3, TASK-285).
        /// </summary>
        public static bool CanReceiveDamage(SpecterStateType state) =>
            state != SpecterStateType.Desvanecimiento &&
            state != SpecterStateType.Desvanecido &&
            state != SpecterStateType.Muerte;

        /// <summary>
        /// Calcula el daño mitigado por las reglas de incorporeidad del espectro (§3, TASK-285).
        /// Inmunidad estricta al componente físico en estados incorpóreos; daño mágico efectivo en estados visibles.
        /// Inmunidad total tanto a físico como a mágico durante Desvanecimiento, Desvanecido o Muerte.
        /// </summary>
        public static float CalculateMitigatedDamage(SpecterStateType state, float amount, DamageType damageType)
        {
            if (amount <= 0f ||
                state == SpecterStateType.Desvanecimiento ||
                state == SpecterStateType.Desvanecido ||
                state == SpecterStateType.Muerte)
            {
                return 0f;
            }

            if (damageType == DamageType.Physical)
            {
                return CanReceivePhysicalDamage(state) ? amount : 0f;
            }

            if (damageType == DamageType.Magical || damageType == DamageType.TrueDamage)
            {
                return CanReceiveMagicalDamage(state) ? amount : 0f;
            }

            return 0f;
        }

        /// <summary>
        /// Valida si el daño mágico no letal puede activar un desplazamiento espectral reactivo (§5).
        /// No se activa durante la ventana de recuperación de 1.5s (debe concluir íntegramente) ni si el cooldown de 4s está en curso.
        /// </summary>
        public static bool CanTriggerReactiveDisplacement(SpecterStateType state, float remainingCooldownSeconds, bool isFatal)
        {
            if (isFatal) return false;
            if (remainingCooldownSeconds > 0f) return false;
            if (state == SpecterStateType.Recuperacion) return false;
            if (state == SpecterStateType.Desvanecimiento ||
                state == SpecterStateType.Desvanecido ||
                state == SpecterStateType.Reaparicion ||
                state == SpecterStateType.Muerte)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida si el daño mágico no letal puede activar un desplazamiento espectral reactivo (§5).
        /// </summary>
        public static bool CanTriggerReactiveDisplacement(SpecterStateType state, bool isMagicDamage, bool isFatal, bool isCooldownActive)
        {
            if (!isMagicDamage) return false;
            return CanTriggerReactiveDisplacement(state, isCooldownActive ? 1f : 0f, isFatal);
        }
    }
}
