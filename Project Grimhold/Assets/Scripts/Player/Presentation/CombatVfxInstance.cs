using UnityEngine;
using System;

namespace Grimhold.Combat.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatVfxInstance : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private Action<CombatVfxInstance> _onComplete;
        private bool _isPlaying;
        private float _playStartTime;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Play(Action<CombatVfxInstance> onComplete, Vector3 position, Quaternion rotation, bool mirror, Color tint, int sortingOrder, Transform parent = null)
        {
            _onComplete = onComplete;
            
            if (parent != null)
            {
                transform.SetParent(parent);
                transform.localPosition = position; // interpreted as local
                transform.localRotation = rotation; // interpreted as local
            }
            else
            {
                transform.position = position;
                transform.rotation = rotation;
            }
            
            Vector3 scale = transform.localScale;
            scale.y = Mathf.Abs(scale.y) * (mirror ? -1f : 1f);
            transform.localScale = scale;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = tint;
                _spriteRenderer.sortingOrder = sortingOrder;
            }

            gameObject.SetActive(true);

            if (_animator != null)
            {
                _animator.Play("Play", 0, 0f);
                _animator.Update(0f); // Force animator state update to prevent immediate finishing
            }

            _isPlaying = true;
            _playStartTime = Time.time;
        }

        private void Update()
        {
            if (!_isPlaying || _animator == null) return;
            if (Time.time - _playStartTime < 0.1f) return; // Prevent instant finishing
            
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Play") && state.normalizedTime >= 1f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            _isPlaying = false;
            gameObject.SetActive(false);
            _onComplete?.Invoke(this);
        }
    }
}
