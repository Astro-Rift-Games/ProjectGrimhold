#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class ScreenDamagePeripheralTests
    {
        private ScreenDamageConfig _config;
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<ScreenDamageConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);

        [TestCase(1f)] [TestCase(.4f)] [TestCase(.2f)]
        public void State_PeripheralSamplingIsInactiveOutsideAgony(float health)
        {
            var state = new ScreenDamageFeedbackState(_config);
            state.Step(health * 100, 100, true, 1, 1);
            Assert.That(state.PeripheralWeight, Is.Zero);
        }
        [Test]
        public void State_AgonySmoothsInAndOutAndAccessibilityZeroClearsImmediately()
        {
            var state = new ScreenDamageFeedbackState(_config);
            state.Step(10, 100, true, .016f, 1);
            Assert.That(state.PeripheralWeight, Is.InRange(.001f, .1f));
            state.Step(10, 100, true, 1, 1);
            Assert.That(state.PeripheralWeight, Is.EqualTo(.5f).Within(.005f));
            state.Step(50, 100, true, .016f, 1);
            Assert.That(state.PeripheralWeight, Is.InRange(.1f, .49f));
            state.Step(10, 100, true, .016f, 0);
            Assert.That(state.PeripheralWeight, Is.Zero);
            state.Reset();
            Assert.That(state.PeripheralWeight, Is.Zero);
        }

        [Test]
        public void CameraMaterial_IsOwnedIsolatedAndReleasedOnlyByItsOwner()
        {
            var go = new GameObject("Camera effect test", typeof(Camera), typeof(ScreenDamageCameraEffect));
            var avatar = new GameObject("Avatar");
            var other = new GameObject("Other owner", typeof(Camera));
            try
            {
                var effect = go.GetComponent<ScreenDamageCameraEffect>(); Configure(effect);
                effect.Apply(go, avatar.transform, _config, 1);
                Assert.That(effect.TryGetMaterial(other.GetComponent<Camera>(), out _), Is.False);
                Assert.That(effect.TryGetMaterial(go.GetComponent<Camera>(), out Material material), Is.True);
                var template = (Material)new SerializedObject(effect).FindProperty("_materialTemplate").objectReferenceValue;
                Assert.That(material, Is.Not.SameAs(template));
                effect.Clear(other);
                Assert.That(effect.RuntimeMaterial, Is.SameAs(material));
                effect.Clear(go);
                Assert.That(material == null, Is.True);
                Assert.That(effect.TryGetMaterial(go.GetComponent<Camera>(), out _), Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(avatar); Object.DestroyImmediate(other); }
        }

        [Test]
        public void CameraMaterial_DisableAndLostAvatarCannotLeaveAnEffectBehind()
        {
            var go = new GameObject("Camera effect teardown", typeof(Camera), typeof(ScreenDamageCameraEffect));
            var avatar = new GameObject("Avatar");
            try
            {
                var effect = go.GetComponent<ScreenDamageCameraEffect>(); Configure(effect);
                effect.Apply(go, avatar.transform, _config, 1);
                effect.TryGetMaterial(go.GetComponent<Camera>(), out Material material);
                Object.DestroyImmediate(avatar);
                Assert.That(effect.TryGetMaterial(go.GetComponent<Camera>(), out _), Is.False);
                Assert.That(material == null, Is.True);
                typeof(ScreenDamageCameraEffect).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(effect, null);
                Assert.That(effect.RuntimeMaterial, Is.Null);
            }
            finally { Object.DestroyImmediate(go); if (avatar != null) Object.DestroyImmediate(avatar); }
        }

        [Test]
        public void Shader_ChangesOnlyPeripheryAndProtectsAvatarAndCursor()
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shaders/ScreenDamagePeripheral.mat");
            Assert.That(template, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(template.shader), Is.False);
            var material = new Material(template);
            var texture = Pattern();
            try
            {
                material.SetFloat("_ViewportAspect", 1);
                material.SetVector("_AvatarClear", new Vector4(.1f, .8f, .12f, .04f));
                material.SetVector("_CursorClear", new Vector4(.9f, .2f, .08f, .025f));
                var baseline = Render(material, texture, 0);
                var active = Render(material, texture, 1);
                int changed = 0;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    int i = y * 64 + x;
                    if (!active[i].Equals(baseline[i])) changed++;
                    Vector2 uv = new Vector2((x + .5f) / 64, (y + .5f) / 64);
                    bool center = x >= 20 && x < 44 && y >= 20 && y < 44;
                    bool avatar = Vector2.Distance(uv, new Vector2(.1f, .8f)) < .1f;
                    bool cursor = Vector2.Distance(uv, new Vector2(.9f, .2f)) < .06f;
                    if (center || avatar || cursor) Assert.That(active[i], Is.EqualTo(baseline[i]), $"Protected pixel {x},{y}");
                }
                Assert.That(changed, Is.GreaterThan(100), "Shader must actually sample the scene, not just tint the spray.");
            }
            finally { Object.DestroyImmediate(texture); Object.DestroyImmediate(material); }
        }

        private static Texture2D Pattern()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                pixels[y * 64 + x] = (x / 2 + y / 2) % 2 == 0 ? new Color(.1f, .6f, .9f) : new Color(.9f, .2f, .1f);
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }
        [Test]
        public void Renderer2D_ChangesOnlyMarkedCameraAndRestoresExactBaselineOnClear()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var host = new GameObject("Screen damage render pipeline test", typeof(Camera));
            var art = new GameObject("Pattern", typeof(SpriteRenderer));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(art, scene);
            var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 4; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var source = Pattern(); source.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(source, new Rect(0, 0, 64, 64), Vector2.one * .5f, 8);
            art.GetComponent<SpriteRenderer>().sprite = sprite;
            var rt = new RenderTexture(64, 64, 24); rt.Create(); camera.targetTexture = rt;
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Color32[] Capture()
            {
                camera.Render(); RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply();
                return readback.GetPixels32();
            }
            try
            {
                var baseline = Capture(); // No marker: shared renderer must leave this camera alone.
                var effect = host.AddComponent<ScreenDamageCameraEffect>(); Configure(effect);
                effect.Apply(host, art.transform, _config, 1);
                var active = Capture();
                int changed = 0;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    int i = y * 64 + x;
                    if (!active[i].Equals(baseline[i])) changed++;
                    if (x >= 20 && x < 44 && y >= 20 && y < 44)
                        Assert.That(active[i], Is.EqualTo(baseline[i]));
                }
                Assert.That(changed, Is.GreaterThan(100), "The authored Renderer2D must execute the effect.");
                effect.Clear(host);
                Assert.That(Capture(), Is.EqualTo(baseline), "Clearing the lease restores exact pre-effect rendering.");
                var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
                foreach (var feature in renderer.rendererFeatures)
                    if (feature is ScreenDamageRendererFeature damage) Assert.That(damage.passMaterial, Is.Null);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(host); Object.DestroyImmediate(art); Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(source); Object.DestroyImmediate(readback); rt.Release(); Object.DestroyImmediate(rt);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static Color32[] Render(Material material, Texture2D source, float weight)
        {
            var rt = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            using var commands = new CommandBuffer();
            try
            {
                material.SetTexture("_BlitTexture", source);
                material.SetVector("_BlitTexture_TexelSize", new Vector4(1f / 64, 1f / 64, 64, 64));
                material.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                material.SetFloat("_EffectWeight", weight);
                commands.SetRenderTarget(rt);
                commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply();
                return readback.GetPixels32();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(readback); }
        }
        private static void Configure(ScreenDamageCameraEffect effect)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("_materialTemplate").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shaders/ScreenDamagePeripheral.mat");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void GameplayCamera_HasCameraScopedPeripheralEffect()
        {
            var type = typeof(PlayerCharacter).Assembly.GetType("ScreenDamageCameraEffect");
            Assert.That(type, Is.Not.Null, "TASK-253 peripheral scene effect is missing");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay Camera.prefab");
            Assert.That(prefab.GetComponent(type), Is.Not.Null);
        }
    }
}
#endif
