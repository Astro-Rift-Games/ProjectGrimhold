using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    public sealed class RuleTileBuilderWindow : EditorWindow
    {
        private static readonly string[] Steps = { "1  Sprites", "2  Template", "3  Assign pieces", "4  Paint & test", "5  Save" };
        private static readonly string[] Templates = { "Continuous floor", "Orthogonal connections (16)", "Full contour & inner corners (47)" };
        private static readonly string[] PieceGroups = { "Center", "Edges and junctions", "Outer corners", "Inner corners", "Ends", "Corridors", "Isolated" };
        private static readonly Dictionary<string, string> Tooltips = new()
        {
            ["1  Sprites"] = "Add sprites or already sliced textures and choose a fallback. This does not change tileset import settings.",
            ["2  Template"] = "Choose the connection patterns prepared for you. Switching templates may replace the draft's existing rules.",
            ["3  Assign pieces"] = "Assign artwork to visual roles such as centers, edges, corners and junctions. Missing optional pieces use the fallback.",
            ["4  Paint & test"] = "Paint a temporary room to test native Unity connections without modifying any project scene.",
            ["5  Save"] = "Review errors and warnings before creating an asset or updating the original while preserving its GUID.",
            ["Back"] = "Return to the previous step without losing the draft's sprite assignments.",
            ["Next"] = "Continue to the next wizard step. This does not save or modify the original asset.",
            ["Open existing"] = "Open a temporary copy of an existing RuleTile. The original changes only when saved; derived types and incompatible neighborhoods are not supported.",
            ["New"] = "Start a new RuleTile draft. Pending changes prompt Save, Cancel or Discard; the sprite library is retained.",
            ["Reload"] = "Reload the original asset without merging versions. Pending changes prompt Save, Cancel or Discard before replacing the draft.",
            ["Add source"] = "Add an individual sprite or sprites from an already sliced texture. This does not slice images or change import settings.",
            ["Add selected assets"] = "Add sprites or sliced textures currently selected in Unity's Project window to this sprite library.",
            ["Clear library"] = "Clear only this window's sprite library and selection. This does not delete project sprites or previously assigned rules.",
            ["Drop sprites / sliced textures here"] = "Drop sprites or already sliced textures here to add their pieces to the library without reimporting them.",
            ["Search"] = "Filter library thumbnails by name. This does not remove hidden pieces or change any rules.",
            ["Sprite selection"] = "Click to select this piece; Ctrl/Cmd-click selects multiple variants. Assign the selection to a card or rule afterwards.",
            ["Fallback sprite"] = "The required sprite shown when no visual rule matches. Choose neutral artwork for connections that have not been assigned yet.",
            ["Default collider"] = "The default collider and initial collider for new rules: None, Sprite shape or Grid cell. Existing scene collision is not modified.",
            ["Use selected sprite as fallback"] = "Use the first sprite selected in the library as the draft's fallback. This does not assign it to every rule.",
            ["Continuous floor"] = "Use one continuous surface role with optional sprite variants. No edge or corner artwork is required.",
            ["Orthogonal connections (16)"] = "Prepare 16 shapes checking above, below, left and right. These patterns do not distinguish diagonal inner corners.",
            ["Full contour & inner corners (47)"] = "Prepare 47 shapes including inner and outer corners. No artwork is invented: missing pieces use the fallback after warning confirmation.",
            ["Piece shape"] = "The center is the painted cell: blue neighbors connect to the same surface, dark neighbors are empty or different, and faint neighbors accept either. Assign matching artwork.",
            ["Assign selected"] = "Assign selected sprites to this role. Multiple pieces on a new card create Random variants; an existing rule keeps its output configuration.",
            ["Details"] = "Open this card's native rule in Advanced to edit sprites, variants, animation, collider or connection conditions.",
            ["Clear piece"] = "Remove this card's rule from the draft; its connections return to the fallback. This does not delete the project sprite.",
            ["Rectangle"] = "Replace only the preview grid with a rectangular room to check interior cells and boundaries.",
            ["L"] = "Replace only the preview grid with an L-shaped room to check inner and outer corners.",
            ["Hole"] = "Replace only the preview grid with a room containing a central hole to check inward-facing edges and corners.",
            ["Corridor"] = "Replace only the preview grid with a one-cell-wide corridor to check ends and narrow connections.",
            ["Isolated"] = "Replace only the preview grid with a single isolated cell that has no neighboring cells of the same surface.",
            ["Clear preview"] = "Clear only the temporary preview grid. This does not remove rules, sprites or tiles from any project scene.",
            ["Erase"] = "Left-click or drag to erase preview cells. Right-click also erases regardless of the selected brush.",
            ["Paint surface"] = "Left-click or drag to paint the draft RuleTile in the temporary grid; right-click erases.",
            ["Other tile"] = "Paint a different checkerboard surface to test how the RuleTile responds when its neighbor is not the same tile.",
            ["Highlight fallback cells"] = "Mark cells with no matching visual rule in orange. This diagnostic overlay does not change the saved result.",
            ["Preview canvas"] = "Left-click or drag uses the selected brush; right-click erases. This temporary grid uses Unity's native Tilemap and does not simulate collisions.",
            ["Asset name"] = "Suggested name for the new .asset file. Generation lets you choose a folder inside Assets; occupied paths are not overwritten.",
            ["Save changes to original asset"] = "Apply the draft to the opened asset while preserving its GUID. Errors block saving, and warnings require explicit confirmation.",
            ["Generate native RuleTile…"] = "Create a new native RuleTile at the chosen location. This does not replace existing assets or modify scene Tilemaps.",
            ["Select saved asset in Project"] = "Select the saved asset in Project to inspect it or drag it into Tile Palette. Pending draft edits are not applied.",
            ["Advanced — edit native rules"] = "Show native rules, priority, neighbor conditions and Single, Random or Animation options. This is optional for template authoring.",
            ["Add rule"] = "Append a custom rule to the draft. It initially uses the fallback and accepts any neighbors; refine its conditions in the matrix.",
            ["Duplicate"] = "Copy the selected rule with a new ID and insert it before the original. Refine its connections afterwards to avoid duplicate coverage.",
            ["Delete"] = "Remove the selected rule from the draft. Its sprites are not deleted; other rules or the fallback resolve those connections.",
            ["Rule priority"] = "Select this rule for editing. Visual priority runs from top to bottom; Unity searches animation rules independently.",
            ["Move earlier ↑"] = "Move the selected rule up to evaluate it earlier. It may visually hide more specific rules below it.",
            ["Move later ↓"] = "Move the selected rule down to evaluate it later. Earlier rules have priority for the visual sprite.",
            ["Assign selected sprites"] = "Replace the selected rule's sprites with the library selection. A Single rule switches to Random when multiple pieces are assigned.",
            ["Native neighbor"] = "Left-click or right-click to cycle the condition: green requires the same surface, red requires another surface or empty space, and blank accepts either.",
            ["Native transform"] = "The center changes allowed matching transforms. Fixed applies none; other options allow rotating or mirroring the rule and its result.",
            ["Native object"] = "Optional prefab Unity can place with this rule. This is not required for sprite tiles; the preview grid does not instantiate these objects.",
            ["Native collider"] = "The collider for this particular rule: None, Sprite or Grid. This is independent of the default collider; the preview does not simulate physics.",
            ["Native output"] = "Single uses one sprite, Random selects variants by position, and Animation plays a sequence. Unity searches animation rules independently.",
            ["Native minimum speed"] = "The minimum native animation speed. This must be positive and no greater than the maximum; it does not control player movement speed.",
            ["Native maximum speed"] = "The maximum native animation speed. This must be positive and at least the minimum; equal values select a uniform speed.",
            ["Native noise"] = "The Perlin noise scale used by Unity to distribute Random variants by position. This does not change the rule's connections.",
            ["Native random transform"] = "Optional transform for Random variants. Keep Fixed if the artwork does not support rotation or mirroring.",
            ["Native count"] = "The number of variant or animation sprites. Increasing this creates empty slots that must be filled before saving.",
            ["Native sprite"] = "The rule's output sprite: a variant in Random mode or a frame in Animation mode. Use persistent sprite assets and fill every active slot."
        };
        [SerializeField] private RuleTile _draft;
        [SerializeField] private RuleTile _source;
        [SerializeField] private string _sourceFingerprint;
        [SerializeField] private string _savedDraft;
        [SerializeField] private List<Sprite> _sprites = new();
        [SerializeField] private List<Sprite> _selectedSprites = new();
        [SerializeField] private RuleTileTemplateKind _template;
        [SerializeField] private bool _guided = true;
        [SerializeField] private int _step;
        [SerializeField] private int _selectedRule;
        [SerializeField] private string _assetName = "NewRuleTile";
        [SerializeField] private string _search = "";
        [SerializeField] private bool _advanced;
        private RuleTileBuilderPreview _preview;
        private RuleTileEditor _nativeEditor;
        private RuleTileValidationReport _report;
        private Vector2 _paletteScroll, _contentScroll;
        private string _message;
        private int _brush = 1;
        private bool _showFallback = true;

        public RuleTile Draft => _draft;
        public RuleTile Source => _source;

        [MenuItem("Tools/Grimhold/Rule Tile Builder")]
        public static void Open() => GetWindow<RuleTileBuilderWindow>("Rule Tile Builder");

        private void OnEnable()
        {
            minSize = new Vector2(860, 660);
            saveChangesMessage = "Save the RuleTile draft before closing? Only Save changes the asset on disk.";
            if (!_draft)
            {
                _draft = CreateInstance<RuleTile>();
                _draft.name = "RuleTile draft";
                _draft.hideFlags = HideFlags.HideAndDontSave;
                _draft.m_DefaultColliderType = Tile.ColliderType.None;
                _savedDraft = EditorJsonUtility.ToJson(_draft);
            }
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += UpdateAnimation;
            RebuildEditors();
            Recalculate();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= UpdateAnimation;
            DisposeEditors();
            // Keep the serialized native draft alive across domain reloads. OnDestroy owns final cleanup.
        }

        private void OnDestroy()
        {
            DisposeEditors();
            if (_draft && !EditorUtility.IsPersistent(_draft)) DestroyImmediate(_draft);
        }

        private void DisposeEditors()
        {
            _preview?.Dispose();
            _preview = null;
            if (_nativeEditor) DestroyImmediate(_nativeEditor);
            _nativeEditor = null;
        }

        private void RebuildEditors()
        {
            DisposeEditors();
            if (RuleTileBuilderValidation.CheckCompatibility(_draft) != null) return;
            _nativeEditor = (RuleTileEditor)UnityEditor.Editor.CreateEditor(_draft, typeof(RuleTileEditor));
        }

        private void OnUndoRedo()
        {
            // Native lists are replaced by Undo; rebuild the bound editor as well as the cached neighbors.
            if (RuleTileBuilderValidation.CheckCompatibility(_draft) == null) RebuildEditors();
            Recalculate();
            Repaint();
        }

        private void Recalculate()
        {
            _report = RuleTileBuilderValidation.Analyze(_draft);
            if (_guided)
            {
                int missing = RuleTileBuilderTemplates.GetSlots(_template).Count(slot => RuleTileBuilderTemplates.FindRule(_draft, slot) == null);
                if (missing > 0) _report.Warnings.Add(missing + " optional pieces of the selected template are unassigned. Their connections use the fallback sprite.");
            }
            hasUnsavedChanges = _draft && EditorJsonUtility.ToJson(_draft) != _savedDraft;
            if (_report.HasErrors)
            {
                _preview?.Dispose();
                _preview = null;
            }
            else if (_preview == null)
            {
                _preview = new RuleTileBuilderPreview(_draft);
                _preview.LoadPattern("Rectangle");
            }
            else _preview.Refresh();
        }

        private void Changed()
        {
            EditorUtility.SetDirty(_draft);
            Recalculate();
            Repaint();
        }

        private void UpdateAnimation()
        {
            if (_step != 3 || _preview == null || _report == null || _report.HasErrors) return;
            _preview.AdvanceAnimation(EditorApplication.timeSinceStartup);
            Repaint();
        }

        public void LoadAsset(RuleTile source)
        {
            if (hasUnsavedChanges) throw new InvalidOperationException("Save or discard the current draft before opening another asset.");
            var clone = RuleTileBuilderAssets.CloneDraft(source);
            DisposeEditors();
            if (_draft) DestroyImmediate(_draft);
            _draft = clone;
            _source = source;
            _sourceFingerprint = RuleTileBuilderAssets.Fingerprint(source);
            _assetName = source.name;
            _savedDraft = EditorJsonUtility.ToJson(_draft);
            _guided = RuleTileBuilderTemplates.TryRecognize(_draft, out _template);
            _advanced = !_guided;
            AddSources(new Object[] { _draft.m_DefaultSprite });
            foreach (var rule in _draft.m_TilingRules) AddSources(rule.m_Sprites.Cast<Object>().ToArray());
            RebuildEditors();
            Recalculate();
        }

        public void AddSources(Object[] sources)
        {
            foreach (var sprite in RuleTileBuilderSpriteCatalog.Resolve(sources))
                if (!_sprites.Contains(sprite)) _sprites.Add(sprite);
        }

        public void ApplySlot(RuleTileTemplateSlot slot, Sprite[] sprites)
        {
            Undo.RegisterCompleteObjectUndo(_draft, "Assign RuleTile piece");
            RuleTileBuilderTemplates.SetSprites(_draft, slot, sprites);
            Changed();
        }

        public void SaveTo(string newAssetPath, bool warningsConfirmed = false)
        {
            if (_source) RuleTileBuilderAssets.Update(_source, _draft, _sourceFingerprint, warningsConfirmed);
            else _source = RuleTileBuilderAssets.Create(_draft, newAssetPath, warningsConfirmed);
            _sourceFingerprint = RuleTileBuilderAssets.Fingerprint(_source);
            _savedDraft = EditorJsonUtility.ToJson(_draft);
            hasUnsavedChanges = false;
            _message = "Saved " + AssetDatabase.GetAssetPath(_source) + ". Drag this asset into Tile Palette to paint with it.";
        }

        public override void SaveChanges()
        {
            // A canceled/failed save must not acknowledge the native close prompt.
            if (!TrySave()) throw new OperationCanceledException("The RuleTile draft was not saved; leave this window open or choose Discard.");
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            hasUnsavedChanges = false;
            base.DiscardChanges();
        }

        private bool ConfirmLeave()
        {
            if (!hasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved RuleTile draft", "Save your draft before changing assets?", "Save", "Cancel", "Discard");
            if (choice == 1) return false;
            if (choice == 0) return TrySave();
            hasUnsavedChanges = false;
            return true;
        }

        private bool TrySave()
        {
            Recalculate();
            if (_report.HasErrors)
            {
                _step = 4;
                _message = "Resolve the blocking errors before saving.";
                return false;
            }
            bool confirmed = _report.Warnings.Count == 0 || EditorUtility.DisplayDialog("Save with warnings?",
                string.Join("\n\n", _report.Warnings) + "\n\nMissing optional rules will use the fallback shown in the preview. Continue?", "Save with warnings", "Cancel");
            if (!confirmed) return false;
            string path = null;
            if (!_source)
            {
                path = EditorUtility.SaveFilePanelInProject("Create native RuleTile", _assetName, "asset", "Choose a new asset path. Existing files will not be replaced.");
                if (string.IsNullOrEmpty(path)) return false;
            }
            try { SaveTo(path, confirmed); return true; }
            catch (Exception exception) { _message = exception.Message; _step = 4; return false; }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Rule Tile Builder", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pick pieces, paint a room, then save. Your original asset is untouched until Save.", EditorStyles.wordWrappedLabel);
            DrawAssetToolbar();
            int nextStep = GUILayout.Toolbar(_step, Steps.Select(s => Help(s)).ToArray(), GUILayout.Height(30));
            if (nextStep != _step)
            {
                _step = nextStep;
                _contentScroll = Vector2.zero;
                GUIUtility.ExitGUI();
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(255))) DrawPalette();
                using (new EditorGUILayout.VerticalScope())
                {
                    _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);
                    switch (_step)
                    {
                        case 0: DrawSourceHelp(); break;
                        case 1: DrawTemplates(); break;
                        case 2: DrawAssignments(); break;
                        case 3: DrawPreview(); break;
                        case 4: DrawSave(); break;
                    }
                    _advanced = EditorGUILayout.Foldout(_advanced, Help("Advanced — edit native rules"), true);
                    if (_advanced) DrawAdvanced();
                    EditorGUILayout.EndScrollView();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_step == 0)) if (GUILayout.Button(Help("Back"))) _step--;
                GUILayout.FlexibleSpace();
                GUILayout.Label((_source ? "Editing " + _source.name : "New native RuleTile") + (hasUnsavedChanges ? " · unsaved draft" : ""));
                using (new EditorGUI.DisabledScope(_step == 4)) if (GUILayout.Button(Help("Next"))) _step++;
            }
        }

        private void DrawAssetToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var source = (RuleTile)EditorGUILayout.ObjectField(Help("Open existing"), _source, typeof(RuleTile), false);
                if (source && source != _source && ConfirmLeave())
                {
                    try { LoadAsset(source); _message = null; }
                    catch (Exception exception) { _message = exception.Message; }
                }
                if (GUILayout.Button(Help("New"), GUILayout.Width(55)) && ConfirmLeave())
                {
                    DisposeEditors();
                    if (_draft) DestroyImmediate(_draft);
                    _draft = null;
                    _source = null;
                    _sourceFingerprint = null;
                    _savedDraft = null;
                    _guided = true;
                    _template = RuleTileTemplateKind.Floor;
                    _assetName = "NewRuleTile";
                    _message = null;
                    // Reinitialize without duplicating subscriptions.
                    Undo.undoRedoPerformed -= OnUndoRedo;
                    EditorApplication.update -= UpdateAnimation;
                    OnEnable();
                }
                using (new EditorGUI.DisabledScope(!_source))
                    if (GUILayout.Button(Help("Reload"), GUILayout.Width(60)) && ConfirmLeave()) LoadAsset(_source);
            }
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Sprite library", EditorStyles.boldLabel);
            var input = EditorGUILayout.ObjectField(Help("Add source"), null, typeof(Object), false);
            if (input) AddSourceWithFeedback(new[] { input });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Help("Add selected assets"))) AddSourceWithFeedback(Selection.objects);
                if (GUILayout.Button(Help("Clear", "Clear library"), GUILayout.Width(45))) { _sprites.Clear(); _selectedSprites.Clear(); }
            }
            var drop = GUILayoutUtility.GetRect(240, 30);
            GUI.Box(drop, Help("Drop sprites / sliced textures here"));
            HandleDrop(drop, AddSourceWithFeedback);
            _search = EditorGUILayout.TextField(Help("Search"), _search);
            EditorGUILayout.LabelField(_selectedSprites.Count + " selected · Ctrl/Cmd-click to select variants", EditorStyles.wordWrappedMiniLabel);
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);
            var visible = _sprites.Where(s => s && (string.IsNullOrEmpty(_search) || s.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            for (int i = 0; i < visible.Length; i += 3)
            {
                using (new EditorGUILayout.HorizontalScope())
                    for (int j = i; j < Mathf.Min(i + 3, visible.Length); j++)
                    {
                        var sprite = visible[j];
                        var rect = GUILayoutUtility.GetRect(73, 91, GUILayout.Width(73));
                        if (_selectedSprites.Contains(sprite)) EditorGUI.DrawRect(rect, new Color(0.2f, 0.45f, 0.65f, 0.6f));
                        RuleTileBuilderSpriteCatalog.DrawSprite(new Rect(rect.x + 5, rect.y + 3, 63, 63), sprite);
                        GUI.Label(new Rect(rect.x, rect.y + 66, rect.width, 24), new GUIContent(sprite.name, sprite.name + " · " + sprite.rect), EditorStyles.wordWrappedMiniLabel);
                        Tooltip(rect, "Sprite selection", sprite.name + " · " + sprite.rect);
                        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                        {
                            if (!Event.current.control && !Event.current.command) _selectedSprites.Clear();
                            if (!_selectedSprites.Remove(sprite)) _selectedSprites.Add(sprite);
                            Event.current.Use();
                            Repaint();
                        }
                    }
            }
            EditorGUILayout.EndScrollView();
        }

        private void AddSourceWithFeedback(Object[] sources)
        {
            AddSources(sources);
            foreach (var source in sources)
                if (source is Texture2D && RuleTileBuilderSpriteCatalog.Resolve(new[] { source }).Length == 0)
                    _message = "This texture has no imported sprites. Slice it using Unity's Sprite Editor first; the Builder does not change import settings.";
        }

        private void DrawSourceHelp()
        {
            EditorGUILayout.LabelField("Start with the pieces you already have", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Add a sliced tileset or individual sprites on the left. Nothing is generated or imported automatically. Select a piece to see its name; Ctrl/Cmd-click selects multiple variants.", MessageType.Info);
            EditorGUILayout.HelpBox("For Catacombs, add the BaseTileset and optionally FloorVariants/WallVariants from Assets/Art/Tilesets/Catacombs. Wall illustrations contain several pieces: one RuleTile paints one sprite per cell, not an entire multi-row wall.", MessageType.None);
            DrawDefaults();
        }

        private void DrawDefaults()
        {
            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("When no connection rule matches, this sprite fills the cell. Choose a valid neutral piece. Collider controls the generated tile, not any existing scene collision.", MessageType.None);
            var serialized = new SerializedObject(_draft);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serialized.FindProperty("m_DefaultSprite"), Help("Fallback sprite"));
            EditorGUILayout.PropertyField(serialized.FindProperty("m_DefaultColliderType"), Help("Default collider"));
            if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
            using (new EditorGUI.DisabledScope(_selectedSprites.Count == 0))
                if (GUILayout.Button(Help("Use selected sprite as fallback")))
                {
                    Undo.RecordObject(_draft, "Set RuleTile fallback");
                    _draft.m_DefaultSprite = _selectedSprites[0];
                    Changed();
                }
        }

        private void DrawTemplates()
        {
            EditorGUILayout.LabelField("Choose how the surface connects", EditorStyles.boldLabel);
            var descriptions = new[]
            {
                "One surface everywhere. Add several sprites to its card for random variation. Does not need edge or corner art.",
                "Looks above, below, left and right. Supports edges, ends and junctions, but cannot distinguish interior diagonal corners.",
                "Also looks diagonally when both adjacent sides connect. Supports outer corners, inner corners and combinations. Missing pieces are optional and fall back visibly."
            };
            for (int i = 0; i < Templates.Length; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool current = _guided && (int)_template == i;
                    if (GUILayout.Button(Help((current ? "✓  " : "") + Templates[i], Templates[i]), GUILayout.Height(28)) && !current)
                    {
                        if (_draft.m_TilingRules.Count == 0 || EditorUtility.DisplayDialog("Replace connection rules?", "The new template replaces the draft's rules, including custom rules. Your saved asset remains unchanged until Save.", "Replace draft rules", "Cancel"))
                        {
                            Undo.RegisterCompleteObjectUndo(_draft, "Change RuleTile template");
                            _draft.m_TilingRules.Clear();
                            _template = (RuleTileTemplateKind)i;
                            _guided = true;
                            Changed();
                        }
                    }
                    EditorGUILayout.LabelField(descriptions[i], EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                        foreach (var slot in RuleTileBuilderTemplates.GetSlots((RuleTileTemplateKind)i).Take(5))
                        { var r = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48)); DrawShape(r, slot); }
                }
            }
        }

        private void DrawAssignments()
        {
            DrawDefaults();
            if (!_guided)
            {
                EditorGUILayout.HelpBox("This asset has custom rules. They are preserved in Advanced. To rebuild connections from a template, choose one in step 2 and confirm replacing the draft rules.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(Templates[(int)_template], EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each card shows the shape it handles: filled neighbors connect, outlined neighbors are empty/other, faint cells can be either. Assign the sprite that depicts that exact shape. Multiple selected sprites become variants for a new card. No artwork is rotated automatically.", MessageType.Info);
            string group = null;
            foreach (var slot in RuleTileBuilderTemplates.GetSlots(_template).OrderBy(s => Array.IndexOf(PieceGroups, s.Group)))
            {
                if (slot.Group != group) { group = slot.Group; EditorGUILayout.LabelField(group, EditorStyles.boldLabel); }
                var rule = RuleTileBuilderTemplates.FindRule(_draft, slot);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var shape = GUILayoutUtility.GetRect(65, 65, GUILayout.Width(65));
                    DrawShape(shape, slot);
                    var image = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64));
                    if (rule != null && rule.m_Sprites != null && rule.m_Sprites.Length > 0) RuleTileBuilderSpriteCatalog.DrawSprite(image, rule.m_Sprites[0]);
                    else GUI.Label(image, "Fallback", EditorStyles.centeredGreyMiniLabel);
                    Tooltip(image, "Assign selected", slot.Label);
                    HandleDrop(image, objects => { var sprites = RuleTileBuilderSpriteCatalog.Resolve(objects); if (sprites.Length > 0) ApplySlot(slot, sprites); });
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(slot.Label, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.LabelField(rule == null ? "Optional · not assigned" : rule.m_Sprites.Length + " sprite(s) · " + rule.m_Output, EditorStyles.miniLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(_selectedSprites.Count == 0))
                                if (GUILayout.Button(Help("Assign selected"))) ApplySlot(slot, _selectedSprites.Where(s => s).ToArray());
                            using (new EditorGUI.DisabledScope(rule == null))
                            {
                                if (GUILayout.Button(Help("Details"))) { _selectedRule = _draft.m_TilingRules.IndexOf(rule); _advanced = true; }
                                if (GUILayout.Button(Help("Clear", "Clear piece"))) ApplySlot(slot, Array.Empty<Sprite>());
                            }
                        }
                    }
                }
            }
        }

        private static void DrawShape(Rect rect, RuleTileTemplateSlot slot)
        {
            float cell = Mathf.Min(rect.width, rect.height) / 3f;
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    var pos = new Vector3Int(x - 1, 1 - y, 0);
                    int bit = -1;
                    for (int b = 0; b < 8; b++) if (RuleTileBuilderTemplates.GetPosition(b) == pos) bit = b;
                    bool center = pos == Vector3Int.zero;
                    bool tested = center || (slot.ConditionMask & (1 << bit)) != 0;
                    bool filled = center || (slot.Mask & (1 << bit)) != 0;
                    var r = new Rect(rect.x + x * cell, rect.y + y * cell, cell - 2, cell - 2);
                    EditorGUI.DrawRect(r, !tested ? new Color(0.35f, 0.35f, 0.35f, 0.25f) : filled ? new Color(0.28f, 0.65f, 0.75f) : new Color(0.18f, 0.18f, 0.18f));
                    if (tested && !filled) GUI.Label(r, "·", EditorStyles.centeredGreyMiniLabel);
                }
            Tooltip(rect, "Piece shape", slot.Label);
        }

        private void DrawPreview()
        {
            EditorGUILayout.HelpBox("Paint a small room with your draft. Orange overlays mark fallback; checkerboard tiles are a different surface. This is a real isolated Unity Tilemap, not a mock-up. Collider physics are not simulated.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
                foreach (string pattern in new[] { "Rectangle", "L", "Hole", "Corridor", "Isolated", "Clear" })
                    if (GUILayout.Button(Help(pattern, pattern == "Clear" ? "Clear preview" : null))) _preview?.LoadPattern(pattern);
            _brush = GUILayout.Toolbar(_brush, new[] { Help("Erase"), Help("Paint surface"), Help("Other tile") });
            _showFallback = EditorGUILayout.ToggleLeft(Help("Highlight fallback cells"), _showFallback);
            if (_preview == null || _report.HasErrors)
            { EditorGUILayout.HelpBox("Assign a valid fallback and resolve the blocking errors in step 5 before previewing. Missing optional template pieces are allowed.", MessageType.Error); return; }
            var rect = GUILayoutUtility.GetRect(320, 450, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var texture = _preview.Render(rect);
                if (texture) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
                DrawPreviewOverlay(rect);
            }
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && (e.button == 0 || e.button == 1) && _preview.TryGetCell(rect, e.mousePosition, out var cell))
            {
                _preview.SetCell(cell, e.button == 1 ? 0 : _brush);
                e.Use();
                Repaint();
            }
            string hoverInfo = "Hover over a cell to inspect its visual and animation rules.";
            if (_preview.TryGetCell(rect, e.mousePosition, out var hover))
            {
                int mask = _preview.GetMask(hover);
                int index = _report.FirstRuleByMask[mask];
                int animation = _report.AnimationRuleByMask[mask];
                hoverInfo = "Cell " + hover + " · " + (index < 0 ? "fallback" : "visual rule " + (index + 1)) +
                    (animation >= 0 ? " · animation rule " + (animation + 1) : "");
            }
            // Reserve the same layout control even when Layout and Repaint see different mouse positions.
            EditorGUILayout.LabelField(hoverInfo, EditorStyles.miniLabel);
            Tooltip(rect, "Preview canvas");
        }

        private void DrawPreviewOverlay(Rect rect)
        {
            float side = Mathf.Min(rect.width, rect.height), cell = side / RuleTileBuilderPreview.GridSize;
            var origin = rect.center - Vector2.one * side * 0.5f;
            for (int y = 0; y < RuleTileBuilderPreview.GridSize; y++)
                for (int x = 0; x < RuleTileBuilderPreview.GridSize; x++)
                {
                    var pos = new Vector3Int(x - 6, 5 - y, 0);
                    var r = new Rect(origin.x + x * cell, origin.y + y * cell, cell, cell);
                    var tile = _preview.Tilemap.GetTile(pos);
                    if (_showFallback && tile == _draft && _report.FirstRuleByMask[_preview.GetMask(pos)] < 0)
                        EditorGUI.DrawRect(r, new Color(1f, 0.55f, 0.05f, 0.28f));
                    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), new Color(1, 1, 1, 0.14f));
                    EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), new Color(1, 1, 1, 0.14f));
                }
        }

        private void DrawSave()
        {
            EditorGUILayout.LabelField("Review before saving", EditorStyles.boldLabel);
            if (!_source) _assetName = EditorGUILayout.TextField(Help("Asset name"), _assetName);
            else EditorGUILayout.LabelField("Update in place", AssetDatabase.GetAssetPath(_source));
            EditorGUILayout.LabelField(_draft.m_TilingRules.Count + " rules · " + _report.FirstRuleByMask.Count(i => i >= 0) + "/256 neighborhoods have explicit visual rules");
            foreach (var error in _report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var warning in _report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            using (new EditorGUI.DisabledScope(_report.HasErrors))
                if (GUILayout.Button(Help(_source ? "Save changes to original asset" : "Generate native RuleTile…"), GUILayout.Height(34))) TrySave();
            if (_source && GUILayout.Button(Help("Select saved asset in Project"))) { Selection.activeObject = _source; EditorGUIUtility.PingObject(_source); }
            EditorGUILayout.HelpBox("Drag the saved RuleTile into Unity's Tile Palette. The standard Inspector remains available. To complete missing pieces later, reopen this asset, assign more cards, and Save: its GUID stays the same.", MessageType.None);
        }

        private void DrawAdvanced()
        {
            if (!_nativeEditor) return;
            EditorGUILayout.HelpBox("Rules are evaluated top to bottom for the visual sprite. Animation rules are searched independently by Unity. Matrix: green arrow = same surface, red cross = other/empty, blank = either. Left/right click cycles; the center changes allowed transforms. Rotations/reflections are opt-in here.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Help("Add rule")))
                {
                    Undo.RegisterCompleteObjectUndo(_draft, "Add RuleTile rule");
                    _draft.m_TilingRules.Add(new RuleTile.TilingRule { m_Id = NextId(), m_Neighbors = new List<int>(), m_NeighborPositions = new List<Vector3Int>(), m_Sprites = new[] { _draft.m_DefaultSprite }, m_ColliderType = _draft.m_DefaultColliderType });
                    _selectedRule = _draft.m_TilingRules.Count - 1;
                    _guided = false;
                    Changed();
                }
                using (new EditorGUI.DisabledScope(_draft.m_TilingRules.Count == 0))
                {
                    if (GUILayout.Button(Help("Duplicate")))
                    {
                        Undo.RegisterCompleteObjectUndo(_draft, "Duplicate RuleTile rule");
                        var copy = _draft.m_TilingRules[Mathf.Clamp(_selectedRule, 0, _draft.m_TilingRules.Count - 1)].Clone();
                        copy.m_Id = NextId();
                        _draft.m_TilingRules.Insert(_selectedRule, copy);
                        _guided = false;
                        Changed();
                    }
                    if (GUILayout.Button(Help("Delete")))
                    {
                        Undo.RegisterCompleteObjectUndo(_draft, "Delete RuleTile rule");
                        _draft.m_TilingRules.RemoveAt(Mathf.Clamp(_selectedRule, 0, _draft.m_TilingRules.Count - 1));
                        Changed();
                    }
                }
            }
            if (_draft.m_TilingRules.Count == 0) return;
            _selectedRule = Mathf.Clamp(_selectedRule, 0, _draft.m_TilingRules.Count - 1);
            for (int i = 0; i < _draft.m_TilingRules.Count; i++)
            {
                var rule = _draft.m_TilingRules[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var thumb = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28));
                    if (rule.m_Sprites != null && rule.m_Sprites.Length > 0) RuleTileBuilderSpriteCatalog.DrawSprite(thumb, rule.m_Sprites[0]);
                    if (GUILayout.Toggle(_selectedRule == i, Help("Priority " + (i + 1) + " · " + rule.m_Output, "Rule priority"), "Button")) _selectedRule = i;
                    var shape = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
                    DrawRuleShape(shape, rule);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_selectedRule == 0)) if (GUILayout.Button(Help("Move earlier ↑"))) MoveRule(-1);
                using (new EditorGUI.DisabledScope(_selectedRule == _draft.m_TilingRules.Count - 1)) if (GUILayout.Button(Help("Move later ↓"))) MoveRule(1);
                using (new EditorGUI.DisabledScope(_selectedSprites.Count == 0))
                    if (GUILayout.Button(Help("Assign selected sprites")))
                    {
                        Undo.RecordObject(_draft, "Set rule sprites");
                        var selected = _draft.m_TilingRules[_selectedRule];
                        selected.m_Sprites = _selectedSprites.Where(s => s).ToArray();
                        if (selected.m_Output == RuleTile.TilingRuleOutput.OutputSprite.Single && selected.m_Sprites.Length > 1) selected.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random;
                        Changed();
                    }
            }
            var active = _draft.m_TilingRules[_selectedRule];
            if (active.m_Sprites == null || active.m_Sprites.Length == 0)
            {
                EditorGUILayout.HelpBox("This rule has no output sprite array. Assign selected sprites to repair it.", MessageType.Error);
                return;
            }
            var area = GUILayoutUtility.GetRect(340, Mathf.Max(180, 170 + active.m_Sprites.Length * 20));
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(_draft, "Edit native RuleTile rule");
            _nativeEditor.RuleMatrixOnGUI(_draft, new Rect(area.x, area.y, 110, 110), new BoundsInt(-1, -1, 0, 3, 3, 1), active);
            _nativeEditor.RuleInspectorOnGUI(new Rect(area.x + 118, area.y, Mathf.Max(220, area.width - 118), area.height), active);
            _nativeEditor.SpriteOnGUI(new Rect(area.x, area.y + 116, 110, 64), active);
            DrawNativeTooltips(area, active);
            if (EditorGUI.EndChangeCheck())
            {
                _guided = RuleTileBuilderTemplates.TryRecognize(_draft, out var recognized);
                if (_guided) _template = recognized;
                Changed();
            }
        }

        private static GUIContent Help(string text, string key = null)
        {
            return new GUIContent(text, Tooltips.TryGetValue(key ?? text, out var tooltip) ? tooltip : string.Empty);
        }

        // Passive labels supply hover text without intercepting the native controls' input or changing layout.
        private static void Tooltip(Rect rect, string key, string context = null)
        {
            var content = Help(string.Empty, key);
            if (!string.IsNullOrEmpty(context)) content.tooltip = context + "\n" + content.tooltip;
            GUI.Label(rect, content, GUIStyle.none);
        }

        private static void DrawNativeTooltips(Rect area, RuleTile.TilingRule rule)
        {
            var neighbors = rule.GetNeighbors();
            float cell = 110f / 3f;
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    var position = new Vector3Int(x - 1, 1 - y, 0);
                    bool center = position == Vector3Int.zero;
                    neighbors.TryGetValue(position, out int state);
                    string current = center ? "Current transform: " + rule.m_RuleTransform :
                        "Current condition: " + (state == 1 ? "same surface" : state == 2 ? "other surface or empty" : "any");
                    Tooltip(new Rect(area.x + x * cell, area.y + y * cell, cell, cell), center ? "Native transform" : "Native neighbor", current);
                }

            // Use the installed editor's public row-height constant instead of assuming IMGUI line metrics.
            float rowHeight = RuleTileEditor.k_SingleLineHeight;
            int row = 0;
            void Row(string key)
            {
                Tooltip(new Rect(area.x + 118, area.y + row++ * rowHeight, Mathf.Max(220, area.width - 118), rowHeight), key);
            }
            Row("Native object");
            Row("Native collider");
            Row("Native output");
            if (rule.m_Output == RuleTile.TilingRuleOutput.OutputSprite.Animation)
            {
                Row("Native minimum speed");
                Row("Native maximum speed");
            }
            if (rule.m_Output == RuleTile.TilingRuleOutput.OutputSprite.Random)
            {
                Row("Native noise");
                Row("Native random transform");
            }
            if (rule.m_Output != RuleTile.TilingRuleOutput.OutputSprite.Single)
            {
                Row("Native count");
                for (int i = 0; i < rule.m_Sprites.Length; i++) Row("Native sprite");
            }
            Tooltip(new Rect(area.x, area.y + 116, 110, 64), "Native sprite");
        }

        private static void DrawRuleShape(Rect rect, RuleTile.TilingRule rule)
        {
            int mask = 0, conditions = 0;
            for (int i = 0; i < rule.m_Neighbors.Count && i < rule.m_NeighborPositions.Count; i++)
                for (int b = 0; b < 8; b++)
                    if (rule.m_NeighborPositions[i] == RuleTileBuilderTemplates.GetPosition(b) && rule.m_Neighbors[i] != 0)
                    { conditions |= 1 << b; if (rule.m_Neighbors[i] == 1) mask |= 1 << b; }
            DrawShape(rect, new RuleTileTemplateSlot("preview", "", "", mask, conditions));
        }

        private int NextId()
        {
            var ids = new HashSet<int>(_draft.m_TilingRules.Select(r => r.m_Id));
            int id = 0;
            while (ids.Contains(id)) id++;
            return id;
        }

        private void MoveRule(int delta)
        {
            Undo.RegisterCompleteObjectUndo(_draft, "Reorder RuleTile rules");
            var rule = _draft.m_TilingRules[_selectedRule];
            _draft.m_TilingRules.RemoveAt(_selectedRule);
            _selectedRule += delta;
            _draft.m_TilingRules.Insert(_selectedRule, rule);
            Changed();
        }

        private static void HandleDrop(Rect rect, Action<Object[]> accept)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform) { DragAndDrop.AcceptDrag(); accept(DragAndDrop.objectReferences); }
            e.Use();
        }
    }
}
