using UnityEngine;
using Fusion;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Visual and client-side presentation for the Catacombs Specter (MVP US-78).
    /// Dynamically modulates sprite transparency for alternating incorporeity
    /// and renders the static 1.5m ground telegraph circle during attack channeling.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpecterAIController))]
    public sealed class SpecterVisualPresenter : NetworkBehaviour
    {
        [Header("Visual Config")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField, Range(0.1f, 1f)] private float _incorporealAlpha = 0.5f;
        [SerializeField, Range(0.1f, 1f)] private float _manifestedAlpha = 1.0f;
        [SerializeField] private Color _incorporealTint = new(0.7f, 0.85f, 1.0f, 0.5f);
        [SerializeField] private Color _manifestedTint = new(1.0f, 0.9f, 0.9f, 1.0f);

        [Header("Telegraph Config")]
        [SerializeField] private LineRenderer _telegraphLine;
        [SerializeField] private Color _telegraphColor = new(0.9f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color _detonationColor = new(1.0f, 0.8f, 0.2f, 0.9f);
        [SerializeField, Range(16, 64)] private int _telegraphSegments = 32;

        [Header("Dependencies")]
        [SerializeField] private SpecterAIController _controller;

        private Color _originalColor = Color.white;

        private void Awake()
        {
            CacheDependencies();
            EnsureTelegraphLine();
        }

        public override void Spawned()
        {
            CacheDependencies();
            EnsureTelegraphLine();
        }

        public override void Render()
        {
            if (_controller == null || _spriteRenderer == null) return;

            UpdateSpritePresentation(_controller.CurrentState);
            UpdateTelegraphPresentation(_controller.CurrentState, _controller.AttackTargetPosition);
        }

        private void UpdateSpritePresentation(SpecterStateType state)
        {
            switch (state)
            {
                case SpecterStateType.Reposo:
                case SpecterStateType.Persecucion:
                    _spriteRenderer.enabled = true;
                    _spriteRenderer.color = _incorporealTint;
                    break;

                case SpecterStateType.Canalizacion:
                case SpecterStateType.Detonacion:
                case SpecterStateType.Recuperacion:
                    _spriteRenderer.enabled = true;
                    _spriteRenderer.color = _manifestedTint;
                    break;

                case SpecterStateType.Desvanecimiento:
                    _spriteRenderer.enabled = true;
                    float fadeOutProgress = GetStateProgress();
                    float alphaOut = Mathf.Lerp(_incorporealAlpha, 0f, fadeOutProgress);
                    _spriteRenderer.color = new Color(_incorporealTint.r, _incorporealTint.g, _incorporealTint.b, alphaOut);
                    break;

                case SpecterStateType.Desvanecido:
                    _spriteRenderer.enabled = false;
                    break;

                case SpecterStateType.Reaparicion:
                    _spriteRenderer.enabled = true;
                    float fadeInProgress = GetStateProgress();
                    float alphaIn = Mathf.Lerp(0f, _incorporealAlpha, fadeInProgress);
                    _spriteRenderer.color = new Color(_incorporealTint.r, _incorporealTint.g, _incorporealTint.b, alphaIn);
                    break;

                case SpecterStateType.Muerte:
                    _spriteRenderer.enabled = true;
                    _spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                    break;
            }
        }

        private void UpdateTelegraphPresentation(SpecterStateType state, Vector2 center)
        {
            if (_telegraphLine == null) return;

            if (state == SpecterStateType.Canalizacion)
            {
                _telegraphLine.enabled = true;
                _telegraphLine.startColor = _telegraphColor;
                _telegraphLine.endColor = _telegraphColor;
                DrawCircle(center, SpecterRules.DetonationRadius);
            }
            else if (state == SpecterStateType.Detonacion)
            {
                _telegraphLine.enabled = true;
                _telegraphLine.startColor = _detonationColor;
                _telegraphLine.endColor = _detonationColor;
                DrawCircle(center, SpecterRules.DetonationRadius);
            }
            else
            {
                _telegraphLine.enabled = false;
            }
        }

        private void DrawCircle(Vector2 center, float radius)
        {
            if (_telegraphLine.positionCount != _telegraphSegments + 1)
            {
                _telegraphLine.positionCount = _telegraphSegments + 1;
            }

            float angleStep = 360f / _telegraphSegments;
            for (int i = 0; i <= _telegraphSegments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;
                _telegraphLine.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private float GetStateProgress()
        {
            if (_controller == null || Runner == null || _controller.StateTimer.ExpiredOrNotRunning(Runner))
            {
                return 1f;
            }

            float remaining = _controller.StateTimer.RemainingTime(Runner) ?? 0f;
            float totalDuration = _controller.CurrentState switch
            {
                SpecterStateType.Canalizacion => SpecterRules.ChannelDuration,
                SpecterStateType.Detonacion => SpecterRules.DetonationDuration,
                SpecterStateType.Recuperacion => SpecterRules.RecoveryDuration,
                SpecterStateType.Desvanecimiento => SpecterRules.FadeOutDuration,
                SpecterStateType.Desvanecido => SpecterRules.HiddenDuration,
                SpecterStateType.Reaparicion => SpecterRules.FadeInDuration,
                _ => 1f
            };

            if (totalDuration <= 0f) return 1f;
            return Mathf.Clamp01(1f - (remaining / totalDuration));
        }

        private void EnsureTelegraphLine()
        {
            if (_telegraphLine != null) return;

            Transform child = transform.Find("TelegraphLine");
            if (child != null && child.TryGetComponent(out LineRenderer existingLine))
            {
                _telegraphLine = existingLine;
                return;
            }

            var lineObj = new GameObject("TelegraphLine");
            lineObj.transform.SetParent(transform, false);
            _telegraphLine = lineObj.AddComponent<LineRenderer>();
            _telegraphLine.useWorldSpace = true;
            _telegraphLine.loop = true;
            _telegraphLine.startWidth = 0.05f;
            _telegraphLine.endWidth = 0.05f;
            _telegraphLine.material = new Material(Shader.Find("Sprites/Default"));
            _telegraphLine.sortingOrder = 5;
            _telegraphLine.enabled = false;
        }

        private void CacheDependencies()
        {
            if (_controller == null) _controller = GetComponent<SpecterAIController>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null) _originalColor = _spriteRenderer.color;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheDependencies();
        }
#endif
    }
}
