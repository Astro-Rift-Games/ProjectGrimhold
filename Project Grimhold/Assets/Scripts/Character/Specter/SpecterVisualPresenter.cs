using UnityEngine;
using Fusion;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Visual and client-side presentation for the Catacombs Specter (MVP US-78 / TASK-285).
    /// Dynamically modulates sprite transparency for alternating incorporeity
    /// and renders the static 1.5m ground telegraph circle during attack channeling,
    /// progressively intensifying visual warning without altering its 1.5m radius.
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
                // Progressively intensify alpha and line width over the 1.25s channel without altering the 1.5m radius (§9 / TASK-285)
                float progress = GetStateProgress();
                float alpha = Mathf.Lerp(0.2f, 0.95f, progress);
                Color currentColor = new Color(_telegraphColor.r, _telegraphColor.g, _telegraphColor.b, alpha);
                _telegraphLine.startColor = currentColor;
                _telegraphLine.endColor = currentColor;
                float currentWidth = Mathf.Lerp(0.03f, 0.07f, progress);
                _telegraphLine.startWidth = currentWidth;
                _telegraphLine.endWidth = currentWidth;
                DrawCircle(center, SpecterRules.DetonationRadius);
            }
            else if (state == SpecterStateType.Detonacion)
            {
                _telegraphLine.enabled = true;
                _telegraphLine.startColor = _detonationColor;
                _telegraphLine.endColor = _detonationColor;
                _telegraphLine.startWidth = 0.08f;
                _telegraphLine.endWidth = 0.08f;
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
            if (_telegraphLine == null)
            {
                Transform child = transform.Find("TelegraphLine");
                if (child != null && child.TryGetComponent(out LineRenderer existingLine))
                {
                    _telegraphLine = existingLine;
                }
                else
                {
                    var lineObj = new GameObject("TelegraphLine");
                    lineObj.transform.SetParent(transform, false);
                    _telegraphLine = lineObj.AddComponent<LineRenderer>();
                    _telegraphLine.useWorldSpace = true;
                    _telegraphLine.loop = true;
                    _telegraphLine.startWidth = 0.05f;
                    _telegraphLine.endWidth = 0.05f;
                    _telegraphLine.sortingOrder = 5;
                    _telegraphLine.enabled = false;
                }
            }

            if (_telegraphLine != null && (_telegraphLine.sharedMaterial == null || _telegraphLine.sharedMaterial.shader == null || _telegraphLine.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
            {
#if UNITY_EDITOR
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SpecterTelegraphLine.mat");
                if (mat != null)
                {
                    _telegraphLine.sharedMaterial = mat;
                }
                else
#endif
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        _telegraphLine.material = new Material(shader);
                    }
                }
            }
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
