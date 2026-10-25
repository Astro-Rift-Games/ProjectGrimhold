#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;


    /// <summary>Owns an isolated preview scene and real raid avatar. Scene is exposed for a later renderer;
    /// rendering and cameras are deliberately not owned here. All seeks are absolute, including backward scrubs.</summary>
    public sealed class AttackPreviewRig : IDisposable
    {
        private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private readonly PreviewPlayerPresentationState _state = new PreviewPlayerPresentationState();
        private PlayerAnimatorView _view;
        private PlayerWeaponPresenter _weapon;
        private PlayerAttackVfxPresenter _vfx;
        private PlayerArmorPresenter _armor;
        private AttackPreviewCombination _combination;
        private bool _disposed;
        private bool _sequential, _initialized;
        private float _lastSeconds;
        private readonly List<(Component owner, string path, Object value)> _references = new List<(Component, string, Object)>();
        private SpriteRenderer _mainWeapon;
        private SpriteRenderer _offWeapon;
        private SpriteRenderer _mainVfx;
        private Scene _scene;
        public Scene Scene => _scene;
        public GameObject Root { get; private set; }
        public Animator Animator { get; private set; }
        public Transform VisualRoot => _view.transform;
        public Transform MainHandGrip { get; private set; }
        public Transform OffHandGrip { get; private set; }
        public IReadOnlyList<SpriteRenderer> HandRenderers { get; private set; }
        public IReadOnlyList<SpriteRenderer> WeaponRenderers { get; private set; }
        // Off-hand VFX is lazily owned by the real weapon presenter, not a prefab field.
        public IReadOnlyList<SpriteRenderer> AttackVfxRenderers
        {
            get
            {
                if (Root == null) return Array.Empty<SpriteRenderer>();
                var offVfx = VisualRoot.Find("OffHandAttackVfx")?.GetComponent<SpriteRenderer>();
                return Array.AsReadOnly(offVfx != null ? new[] { _mainVfx, offVfx } : new[] { _mainVfx });
            }
        }
        public IReadOnlyList<SpriteRenderer> BodyRenderers { get; private set; }
        public float Duration { get; private set; }

        public AttackPreviewRig()
        {
            try { BuildInstance(); }
            catch { Dispose(); throw; }
        }

        private void BuildInstance()
        {
            if (!_scene.IsValid() || !_scene.isLoaded) _scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException($"Missing preview prefab: {PrefabPath}");
                // A connected Editor prefab instance forbids reparenting its children. Runtime
                // clones must move the weapon between MainHandGrip and WeaponPose on equip.
                Root = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(Root, _scene);
                _view = Require<PlayerAnimatorView>();
                _weapon = Require<PlayerWeaponPresenter>();
                _vfx = Require<PlayerAttackVfxPresenter>();
                _armor = Require<PlayerArmorPresenter>();
                Animator = _view.GetComponent<Animator>();
                if (Animator == null) throw new InvalidOperationException("Preview visual root has no Animator.");
                MainHandGrip = VisualRoot.Find("RightHandPivot/RightHand/MainHandGrip");
                OffHandGrip = VisualRoot.Find("LeftHandPivot/LeftHand/OffHandGrip");
                if (MainHandGrip == null || OffHandGrip == null) throw new InvalidOperationException("Preview prefab is missing a hand grip.");
                HandRenderers = Array.AsReadOnly(new[] { MainHandGrip.parent.GetComponent<SpriteRenderer>(), OffHandGrip.parent.GetComponent<SpriteRenderer>() });
                _mainWeapon = RendererReference(_weapon, "_mainHandRenderer");
                _offWeapon = RendererReference(_weapon, "_offHandRenderer");
                _mainVfx = RendererReference(_vfx, "_vfxRenderer");
                WeaponRenderers = Array.AsReadOnly(new[] { _mainWeapon, _offWeapon });
                BodyRenderers = Array.AsReadOnly(Root.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r => !HandRenderers.Contains(r) && !WeaponRenderers.Contains(r) && r != _mainVfx).ToArray());
                CaptureReferences();
                if (!IsIntact) throw new InvalidOperationException("Preview prefab has incomplete presenter references or Animator layers.");
            }
            catch { DestroyInstance(); throw; }
        }

        // Snapshot serialized dependencies from the known-good prefab, including nested references.
        // A removed component or cleared reference must be detected before any presenter logs validation errors.
        private void CaptureReferences()
        {
            _references.Clear();
            foreach (Component owner in new Component[] { _view, _weapon, _vfx, _armor })
            {
                using var serialized = new SerializedObject(owner);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                        _references.Add((owner, property.propertyPath, property.objectReferenceValue));
            }
        }

        public bool IsIntact
        {
            get
            {
                if (_disposed || !_scene.IsValid() || !_scene.isLoaded || Root == null || _view == null ||
                    _weapon == null || _vfx == null || _armor == null || Animator == null ||
                    Animator.runtimeAnimatorController == null || MainHandGrip == null || OffHandGrip == null) return false;
                foreach (var reference in _references)
                {
                    if (reference.owner == null || reference.value == null) return false;
                    using var serialized = new SerializedObject(reference.owner);
                    if (serialized.FindProperty(reference.path)?.objectReferenceValue != reference.value) return false;
                }
                return RequiredReferencesPresent(_vfx, "_animator", "_vfxTransform", "_vfxRenderer", "_tintPalette") &&
                    RequiredReferencesPresent(_weapon, "_animatorView", "_mainHandGrip", "_weaponPose", "_weaponPoseHandRenderer",
                        "_mainHandWeaponPivot", "_mainHandWeaponVisual", "_mainHandRenderer", "_offHandGrip", "_offHandVisual", "_offHandRenderer") &&
                    Animator.GetLayerIndex("RightHand") >= 0 && Animator.GetLayerIndex("LeftHand") >= 0;
            }
        }

        private static bool RequiredReferencesPresent(Component owner, params string[] fields)
        {
            using var serialized = new SerializedObject(owner);
            foreach (string field in fields)
                if (serialized.FindProperty(field)?.objectReferenceValue == null) return false;
            return true;
        }

        private void EnsureInstance()
        {
            if (IsIntact) return;
            DestroyInstance();
            BuildInstance();
        }

        private void DestroyInstance()
        {
            try { ReleasePresenters(); }
            finally
            {
                if (Root != null) Object.DestroyImmediate(Root);
                Root = null;
                _references.Clear();
            }
        }

        private T Require<T>() where T : Component => Root.GetComponentInChildren<T>(true)
            ?? throw new InvalidOperationException($"Preview prefab is missing {typeof(T).Name}.");

        private static SpriteRenderer RendererReference(Component presenter, string field)
        {
            using var serialized = new SerializedObject(presenter);
            var renderer = serialized.FindProperty(field)?.objectReferenceValue as SpriteRenderer;
            return renderer != null ? renderer : throw new InvalidOperationException($"{presenter.GetType().Name}.{field} is not assigned.");
        }

        public void Configure(AttackPreviewCombination combination, bool sequential = false)
        {
            ThrowIfDisposed();
            if (combination == null) throw new ArgumentNullException(nameof(combination));
            EnsureInstance();
            if (!AttackPreviewCatalog.IsLegalPair(combination.MainHand, combination.OffHand))
                throw new ArgumentException("Preview requires a legal offensive weapon set.", nameof(combination));
            if (combination.PresentationError != null) throw new ArgumentException(combination.PresentationError, nameof(combination));
            _sequential = sequential;
            _initialized = false;
            _lastSeconds = 0;
            _combination = combination;
            _state.Configure(combination);
            Duration = ResolveDuration(combination);
            Evaluate(0f);
        }

        private float ResolveDuration(AttackPreviewCombination combination)
        {
            if (combination.Action == AttackPreviewAction.Defend)
            {
                // Sustained defense has no gameplay end. Preview one complete authored pose cycle.
                return Animator.runtimeAnimatorController.animationClips
                    .Where(c => c.name.StartsWith("Shield_Defend_", StringComparison.Ordinal))
                    .Select(c => c.length).DefaultIfEmpty(1f).Max();
            }
            var loot = combination.Action == AttackPreviewAction.MainAttack ? combination.MainHand : combination.OffHand;
            var weapon = loot != null ? loot.WeaponDefinition : null;
            if (weapon == null) throw new ArgumentException("Attack action requires a weapon in its source hand.");
            var presentation = weapon.Presentation;
            var clip = combination.Action == AttackPreviewAction.MainAttack
                ? presentation.GetAttackClip(combination.FacingIndex) : presentation.GetOffHandAttackClip(combination.FacingIndex);
            float end = clip != null ? clip.length : weapon.AttackReleaseSeconds;
            var vfx = presentation.AttackVfx;
            if (vfx != null && vfx.Clip != null)
                end = Mathf.Max(end, weapon.AttackReleaseSeconds - vfx.ReleaseLeadSeconds + vfx.Clip.length);
            return end;
        }

        /// <summary>Absolute scrubs reset presentation. Sequential validation initializes once,
        /// publishes one attack and advances the real frame pipeline through the cleanup tail.</summary>
        public void Evaluate(float seconds)
        {
            ThrowIfDisposed();
            if (_combination == null) throw new InvalidOperationException("Configure preview rig first.");
            if (!float.IsFinite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (_sequential && _initialized && (!IsIntact || seconds < _lastSeconds))
                throw new InvalidOperationException("Sequential validation requires an intact rig and monotonically increasing time.");
            EnsureInstance();
            bool reset = !_sequential || !_initialized;
            if (reset)
            {
                ReleasePresenters();
                Animator.Rebind();
                _view.BindPresentationState(_state);
                _view.BindMovementState(_state);
                _weapon.BindPresentationState(_state);
                _vfx.BindPresentationState(_state);
                _armor.BindEquipmentVisualSource(_state);
                _view.InitializePresentation();
                _vfx.InitializePresentation();
                _weapon.InitializePresentation();
                _armor.InitializePresentation();
            }
            _state.Evaluate(seconds, publishAttack: reset);
            _weapon.BeginFrame();
            _view.BeginFrame();
            Animator.Update(_sequential && !reset ? seconds - _lastSeconds : 0f);
            _view.PresentFrame();
            // A completed timed attack queues Idle with Play; Edit Mode has no automatic
            // animation pass after LateUpdate, so commit that queued state before consumers.
            Animator.Update(0f);
            if (_combination.Action == AttackPreviewAction.Defend)
            {
                int layer = Animator.GetLayerIndex("LeftHand");
                if (layer >= 0)
                {
                    Animator.Play("LeftHand.Defend", layer, Duration > 0f ? Mathf.Clamp01(seconds / Duration) : 0f);
                    Animator.Update(0f);
                }
            }
            _vfx.PresentFrame();
            _weapon.PresentFrame();
            _armor.PresentFrame();
            _lastSeconds = seconds;
            _initialized = true;
        }

        private void ReleasePresenters()
        {
            if (_weapon != null) _weapon.ReleasePresentation();
            if (_vfx != null) _vfx.ReleasePresentation();
            // Armor has no subscriptions or transient resources; its initialization resets cached visuals.
            if (_view != null) _view.ReleasePresentation();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AttackPreviewRig));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { DestroyInstance(); }
            finally
            {
                if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            }
        }
    }
#endif
