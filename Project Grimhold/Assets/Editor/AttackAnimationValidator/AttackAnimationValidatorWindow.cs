#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AttackAnimationValidatorWindow : EditorWindow
{
    private static readonly string[] Facings = { "N", "NE", "NW", "S", "SE", "SW" };
    private static readonly string[] Kinds = { "All equipment", "One-hand Main", "One-hand Off", "Two-hand", "Dual wield", "Shield + weapon" };
    private static readonly string[] Actions = { "All actions", "Main attack", "Off attack", "Defend" };
    private static readonly string[] Outcomes = { "All outcomes", "Unvalidated", "Pass", "Warning", "Fail" };
    private const float RowHeight = 112, HeaderWidth = 170;
    private readonly Dictionary<string, float> _durations = new Dictionary<string, float>();
    private IReadOnlyList<AttackPreviewCombination> _catalog;
    private AttackPreviewWindowModel _model;
    private AttackPreviewRig _rig;
    private AttackPreviewRenderer _renderer;
    private AttackPreviewFrameCache _cache;
    private AttackPreviewChecks _markerChecks;
    private Vector2 _gridScroll, _detailScroll;
    private double _lastUpdate;
    private float _detailTime;
    private bool _compare, _markers, _followGlobal = true;
    private int _samples = 16, _maxTextures = 2048;
    private string _error;
    private float _tail = 0.1f;
    private bool _needsRebuild;

    [MenuItem("Tools/Animations/Attack Animation Validator")]
    public static void Open() => GetWindow<AttackAnimationValidatorWindow>("Attack Animation Validator");

    private void OnEnable()
    {
        titleContent = new GUIContent("Attack Animation Validator");
        minSize = new Vector2(1120, 620);
        try
        {
            _catalog = AttackPreviewCatalog.Enumerate();
            _model = new AttackPreviewWindowModel(_catalog) { Loop = true };
            _rig = new AttackPreviewRig();
            _renderer = new AttackPreviewRenderer(_rig);
            _markerChecks = new AttackPreviewChecks();
            CreateCache();
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.projectChanged += AssetsChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            AttackPreviewTestRunGuard.Changed += TestRunChanged;
        }
        catch (Exception ex) { Release(); _error = ex.Message; }
    }
    private void OnDisable() => Release();
    private void Release()
    {
        EditorApplication.update -= Tick;
        EditorApplication.projectChanged -= AssetsChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Release;
        AttackPreviewTestRunGuard.Changed -= TestRunChanged;
        ReleasePreview();
    }
    private void ReleasePreview()
    {
        try { _cache?.Dispose(); }
        finally
        {
            _cache = null;
            try { _renderer?.Dispose(); }
            finally
            {
                _renderer = null;
                try { _markerChecks?.Dispose(); }
                finally { _markerChecks = null; _rig?.Dispose(); _rig = null; }
            }
        }
    }
    private void CreateCache()
    {
        _cache?.Dispose();
        _cache = new AttackPreviewFrameCache(RenderThumbnail, () => EditorApplication.timeSinceStartup * 1000,
            _samples, _maxTextures, _tail);
    }
    private Texture2D RenderThumbnail(AttackPreviewCombination cell, float seconds)
    {
        _rig.Configure(cell);
        return _renderer.Render(seconds, 96, 96);
    }
    private float Duration(AttackPreviewCombination cell)
    {
        if (_durations.TryGetValue(cell.Id, out float duration)) return duration;
        _rig.Configure(cell);
        duration = _rig.Duration;
        _durations.Add(cell.Id, duration);
        return duration;
    }
    private void AssetsChanged()
    {
        // Asset notifications may arrive during imports or a test's transient scene setup.
        // Do not inspect assets or initialize presenters from this callback.
        _needsRebuild = true;
        Repaint();
    }
    private void TestRunChanged()
    {
        _needsRebuild = true;
        _lastUpdate = EditorApplication.timeSinceStartup;
        if (AttackPreviewTestRunGuard.IsRunning) ReleasePreview();
        Repaint();
    }
    private void EnsurePreview()
    {
        if (!_needsRebuild && _rig != null && _rig.IsIntact && _cache != null) return;
        ReleasePreview();
        _durations.Clear();
        _catalog = AttackPreviewCatalog.Enumerate();
        _model = new AttackPreviewWindowModel(_catalog) { Loop = true };
        _rig = new AttackPreviewRig();
        _renderer = new AttackPreviewRenderer(_rig);
        _markerChecks = new AttackPreviewChecks();
        CreateCache();
        _needsRebuild = false;
        _error = null;
    }
    public void Tick()
    {
        if (AttackPreviewTestRunGuard.IsRunning) return;
        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - _lastUpdate);
        _lastUpdate = now;
        try
        {
            if (_error != null && !_needsRebuild) return;
            EnsurePreview();
            float duration = _model.Selection != null && !_model.Selection.MissingPresentation ? Duration(_model.Selection) + _tail : 1f;
            _model.Advance(delta, duration);
            bool hadPending = _cache.PendingCount > 0;
            _cache.Tick(8);
            if (_followGlobal) _detailTime = _model.NormalizedTime;
            if (_model.Playing || hadPending) Repaint();
        }
        catch (Exception ex)
        {
            _error = ex.ToString();
            if (_model != null) _model.Playing = false;
            ReleasePreview();
            _needsRebuild = false;
            Repaint();
        }
    }
    private void OnGUI()
    {
        if (AttackPreviewTestRunGuard.IsRunning)
        {
            EditorGUILayout.HelpBox("Preview paused while the Unity Test Runner is executing.", MessageType.Info);
            return;
        }
        if (_rig != null && !_rig.IsIntact) _needsRebuild = true;
        if (_needsRebuild) { Repaint(); return; } // Tick owns the guarded lazy rebuild.
        if (_model == null || _rig == null)
        {
            EditorGUILayout.HelpBox(_error ?? "Preview unavailable.", MessageType.Error);
            if (GUILayout.Button("Rebuild preview")) _needsRebuild = true;
            return;
        }
        DrawToolbar();
        if (_error != null)
        {
            EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (GUILayout.Button("Retry previews")) { _error = null; _cache.Invalidate(); }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawGrid();
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(410))) DrawDetail();
        }
    }
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Weapon", GUILayout.Width(48));
            _model.Search = GUILayout.TextField(_model.Search, EditorStyles.toolbarTextField, GUILayout.MinWidth(120));
            int kind = EditorGUILayout.Popup(_model.EquipmentKind.HasValue ? (int)_model.EquipmentKind.Value + 1 : 0, Kinds, GUILayout.Width(140));
            _model.EquipmentKind = kind == 0 ? (AttackPreviewEquipmentKind?)null : (AttackPreviewEquipmentKind)(kind - 1);
            int action = EditorGUILayout.Popup(_model.Action.HasValue ? (int)_model.Action.Value + 1 : 0, Actions, GUILayout.Width(110));
            _model.Action = action == 0 ? (AttackPreviewAction?)null : (AttackPreviewAction)(action - 1);
            int facing = EditorGUILayout.Popup(_model.Facing.HasValue ? _model.Facing.Value + 1 : 0,
                new[] { "All facings", "N", "NE", "NW", "S", "SE", "SW" }, GUILayout.Width(95));
            _model.Facing = facing == 0 ? (int?)null : facing - 1;
            int outcome = EditorGUILayout.Popup(_model.Outcome.HasValue ? (int)_model.Outcome.Value + 1 : 0, Outcomes, GUILayout.Width(110));
            _model.Outcome = outcome == 0 ? (AttackPreviewCellOutcome?)null : (AttackPreviewCellOutcome)(outcome - 1);
            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton)) Validate(_catalog);
            if (GUILayout.Button("Validate Visible", EditorStyles.toolbarButton)) Validate(_model.VisibleCells);
        }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            _model.Playing = GUILayout.Toggle(_model.Playing, _model.Playing ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(48));
            _model.Loop = GUILayout.Toggle(_model.Loop, "Loop", EditorStyles.toolbarButton, GUILayout.Width(45));
            int speed = _model.Speed == 0.25f ? 0 : _model.Speed == 0.5f ? 1 : 2;
            speed = EditorGUILayout.Popup(speed, new[] { "0.25x", "0.5x", "1x" }, GUILayout.Width(65));
            _model.Speed = speed == 0 ? 0.25f : speed == 1 ? 0.5f : 1f;
            _model.NormalizedTime = GUILayout.HorizontalSlider(_model.NormalizedTime, 0, 1, GUILayout.MinWidth(100));
            GUILayout.Label($"{_model.NormalizedTime:P0}", GUILayout.Width(38));
            GUILayout.Label($"{_model.Count(AttackPreviewCellOutcome.Fail)} fail / {_model.Count(AttackPreviewCellOutcome.Warning)} warn / " +
                $"{_model.Count(AttackPreviewCellOutcome.Pass)} pass / {_model.Count(AttackPreviewCellOutcome.Unvalidated)} unvalidated");
        }
        DrawCacheSettings();
    }
    private void DrawCacheSettings()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            int samples = EditorGUILayout.IntField("Samples/cell", _samples, GUILayout.Width(210));
            int max = EditorGUILayout.IntField("Max textures", _maxTextures, GUILayout.Width(210));
            float tail = EditorGUILayout.FloatField("Tail (seconds)", _tail, GUILayout.Width(210));
            if (EditorGUI.EndChangeCheck())
            {
                _samples = Mathf.Clamp(samples, 2, 64);
                _maxTextures = Mathf.Clamp(max, 16, 8192);
                _tail = float.IsFinite(tail) ? Mathf.Clamp(tail, 0, 5) : 0.1f;
                CreateCache();
                _model = new AttackPreviewWindowModel(_catalog) { Loop = true };
            }
            GUILayout.Label($"Cache {_cache.TextureCount}/{_maxTextures}; pending {_cache.PendingCount}", EditorStyles.miniLabel);
        }
    }
    private void Validate(IReadOnlyList<AttackPreviewCombination> cells)
    {
        if (AttackPreviewTestRunGuard.IsRunning) return;
        _model.Playing = false;
        try
        {
            var reports = new AttackPreviewValidator(new AttackPreviewCheckSettings { TailSeconds = _tail }).ValidateAll(cells,
                (index, total) => !EditorUtility.DisplayCancelableProgressBar("Attack Animation Validator",
                    $"{index + 1}/{total}: {cells[index].DisplayName}", total == 0 ? 0 : (float)index / total));
            foreach (var report in reports) _model.SetReport(report);
        }
        catch (Exception ex) { _error = ex.ToString(); }
        finally { EditorUtility.ClearProgressBar(); _lastUpdate = EditorApplication.timeSinceStartup; Repaint(); }
    }
    private void DrawGrid()
    {
        var rows = _model.Rows;
        Rect area = GUILayoutUtility.GetRect(620, 10000, 200, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        float width = Mathf.Max(620, area.width - 16);
        float cellWidth = (width - HeaderWidth) / 6;
        var header = new Rect(area.x, area.y, area.width, 22);
        GUI.Label(new Rect(header.x, header.y, HeaderWidth, 22), $"{rows.Count} equipment/action rows", EditorStyles.boldLabel);
        for (int f = 0; f < 6; f++) GUI.Label(new Rect(header.x + HeaderWidth + f * cellWidth, header.y, cellWidth, 22), Facings[f], EditorStyles.boldLabel);
        var viewport = new Rect(area.x, area.y + 22, area.width, Mathf.Max(1, area.height - 22));
        _gridScroll = GUI.BeginScrollView(viewport, _gridScroll, new Rect(0, 0, width, rows.Count * RowHeight));
        int first = Mathf.Max(0, Mathf.FloorToInt(_gridScroll.y / RowHeight));
        int end = Mathf.Min(rows.Count, Mathf.CeilToInt((_gridScroll.y + viewport.height) / RowHeight) + 1);
        for (int r = first; r < end; r++) DrawRow(rows[r], r * RowHeight, cellWidth);
        GUI.EndScrollView();
    }
    private void DrawRow(IReadOnlyList<AttackPreviewCombination> row, float y, float cellWidth)
    {
        var first = row[0];
        GUI.Label(new Rect(4, y + 8, HeaderWidth - 8, RowHeight - 8),
            $"{first.MainHand?.name ?? "None"}\n+ {first.OffHand?.name ?? "None"}\n{first.Action}", EditorStyles.wordWrappedMiniLabel);
        foreach (var cell in row)
        {
            var rect = new Rect(HeaderWidth + cell.FacingIndex * cellWidth, y + 2, cellWidth - 4, RowHeight - 4);
            DrawCell(cell, rect);
        }
    }
    private void DrawCell(AttackPreviewCombination cell, Rect rect)
    {
        var outcome = _model.GetOutcome(cell);
        EditorGUI.DrawRect(rect, OutcomeColor(outcome));
        var inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
        EditorGUI.DrawRect(inner, new Color(0.12f, 0.12f, 0.12f));
        if (cell.PresentationError != null)
            GUI.Label(inner, new GUIContent("Missing presentation", cell.PresentationError), EditorStyles.centeredGreyMiniLabel);
        else if (_error == null)
        {
            _cache.Request(cell, Duration(cell));
            var texture = _cache.Get(cell.Id, _model.NormalizedTime);
            if (texture != null) GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit);
            else GUI.Label(inner, "Sampling…", EditorStyles.centeredGreyMiniLabel);
        }
        GUI.Label(new Rect(inner.x + 2, inner.yMax - 18, inner.width - 4, 18),
            (_model.Selection?.Id == cell.Id ? "● " : "") + outcome, EditorStyles.whiteMiniLabel);
        if (GUI.Button(rect, new GUIContent("", cell.DisplayName), GUIStyle.none))
        { _model.Selection = cell; _detailTime = _model.NormalizedTime; Repaint(); }
    }
    private static Color OutcomeColor(AttackPreviewCellOutcome outcome)
    {
        switch (outcome)
        {
            case AttackPreviewCellOutcome.Fail: return new Color(0.9f, 0.22f, 0.2f);
            case AttackPreviewCellOutcome.Warning: return new Color(1, 0.75f, 0.15f);
            case AttackPreviewCellOutcome.Pass: return new Color(0.25f, 0.75f, 0.35f);
            default: return Color.gray;
        }
    }
    private void DrawDetail()
    {
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
        var cell = _model.Selection;
        if (cell == null) EditorGUILayout.HelpBox("Select a facing cell to inspect its live preview and checks.", MessageType.Info);
        else
        {
            EditorGUILayout.LabelField(cell.DisplayName, EditorStyles.wordWrappedLabel);
            _markers = EditorGUILayout.ToggleLeft("Grip markers (cyan hand / magenta grip)", _markers);
            _followGlobal = EditorGUILayout.ToggleLeft("Follow global timeline", _followGlobal);
            EditorGUI.BeginChangeCheck();
            float time = EditorGUILayout.Slider("Detail time", _followGlobal ? _model.NormalizedTime : _detailTime, 0, 1);
            if (EditorGUI.EndChangeCheck()) { _followGlobal = false; _detailTime = time; }
            if (_followGlobal) _detailTime = _model.NormalizedTime;
            var counterpart = _model.Counterpart(cell);
            using (new EditorGUI.DisabledScope(counterpart == null))
                _compare = EditorGUILayout.ToggleLeft("Compare with Main/Off counterpart", _compare);
            if (cell.PresentationError == null)
                DrawLivePreviews(cell, _compare && counterpart?.PresentationError == null ? counterpart : null);
            else EditorGUILayout.HelpBox(cell.PresentationError, MessageType.Error);
            DrawPingButtons(cell);
            var report = _model.GetReport(cell.Id);
            if (report == null) EditorGUILayout.HelpBox("Unvalidated. Validate visible cells or all cells to collect checks.", MessageType.Info);
            else { DrawFailureTimeline(report); DrawChecks(report); }
        }
        EditorGUILayout.EndScrollView();
    }
    private void DrawLivePreviews(AttackPreviewCombination cell, AttackPreviewCombination counterpart)
    {
        float seconds = _detailTime * (Duration(cell) + _tail);
        EditorGUILayout.LabelField($"{seconds:F3}s / {Duration(cell) + _tail:F3}s", EditorStyles.miniLabel);
        var rect = GUILayoutUtility.GetRect(380, counterpart == null ? 290 : 210, GUILayout.ExpandWidth(true));
        if (counterpart == null) DrawLive(cell, seconds, rect);
        else
        {
            float half = (rect.width - 4) / 2;
            DrawLive(cell, seconds, new Rect(rect.x, rect.y, half, rect.height));
            DrawLive(counterpart, seconds, new Rect(rect.x + half + 4, rect.y, half, rect.height));
            EditorGUILayout.LabelField($"Left: {cell.Action}   Right: {counterpart.Action}", EditorStyles.miniLabel);
        }
    }
    private void DrawLive(AttackPreviewCombination cell, float seconds, Rect rect)
    {
        if (Event.current.type != EventType.Repaint || _error != null) return;
        Texture2D texture = null;
        try
        {
            int size = Mathf.Clamp(Mathf.RoundToInt(rect.width * EditorGUIUtility.pixelsPerPoint), 64, 768);
            texture = RenderDetailFrame(cell, seconds, size);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            if (_markers)
            {
                // Project into the square actually occupied by ScaleToFit, not the surrounding layout rect.
                float side = Mathf.Min(rect.width, rect.height);
                var image = new Rect(rect.center.x - side / 2, rect.center.y - side / 2, side, side);
                DrawMarkers(image);
            }
        }
        catch (Exception ex) { _error = ex.ToString(); }
        finally { if (texture != null) Object.DestroyImmediate(texture); }
    }
    // Explicit live-render seam shared by the detail panel and the lifecycle smoke test.
    // The caller owns the returned readback texture.
    internal Texture2D RenderDetailFrame(AttackPreviewCombination cell, float seconds, int size)
    {
        _rig.Configure(cell);
        return _renderer.Render(seconds, size, size);
    }
    private void DrawMarkers(Rect image)
    {
        for (int hand = 0; hand < 2; hand++)
        {
            var sprite = _rig.HandRenderers[hand];
            if (sprite != null && sprite.sprite != null)
                DrawMarker(_renderer.Project(_markerChecks.DrawnHandWorldPoint(sprite), image), Color.cyan, image);
            var grip = hand == 0 ? _rig.MainHandGrip : _rig.OffHandGrip;
            DrawMarker(_renderer.Project(grip.position, image), Color.magenta, image);
        }
    }
    private static void DrawMarker(Vector2 point, Color color, Rect clip)
    {
        if (clip.Contains(point)) EditorGUI.DrawRect(new Rect(point.x - 2, point.y - 2, 5, 5), color);
    }
    private void DrawPingButtons(AttackPreviewCombination cell)
    {
        var loot = cell.Action == AttackPreviewAction.MainAttack ? cell.MainHand : cell.OffHand;
        var weapon = loot != null ? loot.WeaponDefinition : null;
        AnimationClip clip = null;
        if (weapon != null && cell.Action != AttackPreviewAction.Defend)
            clip = cell.Action == AttackPreviewAction.MainAttack ? weapon.Presentation.GetAttackClip(cell.FacingIndex) : weapon.Presentation.GetOffHandAttackClip(cell.FacingIndex);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(loot == null))
                if (GUILayout.Button("Ping weapon")) EditorGUIUtility.PingObject(loot);
            using (new EditorGUI.DisabledScope(clip == null))
                if (GUILayout.Button("Ping clip")) EditorGUIUtility.PingObject(clip);
        }
    }
    private void DrawFailureTimeline(AttackPreviewReport report)
    {
        EditorGUILayout.LabelField($"{report.Outcome} • {report.SampleCount} samples", EditorStyles.boldLabel);
        foreach (AttackPreviewCheckId id in Enum.GetValues(typeof(AttackPreviewCheckId)))
        {
            EditorGUILayout.LabelField(id.ToString(), EditorStyles.miniLabel);
            var bar = GUILayoutUtility.GetRect(200, 12, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, new Color(0.19f, 0.19f, 0.19f));
            float end = Mathf.Max(0.0001f, report.EndSeconds);
            foreach (var range in report.FailureRanges)
            {
                if (range.Id != id) continue;
                float x = bar.x + Mathf.Clamp01(range.StartSeconds / end) * bar.width;
                float width = Mathf.Max(2, (range.EndSeconds - range.StartSeconds) / end * bar.width);
                var rangeRect = new Rect(x, bar.y + (range.Hand == AttackPreviewHand.Main ? 0 : 6), Mathf.Min(width, bar.xMax - x), 6);
                EditorGUI.DrawRect(rangeRect, OutcomeColor(range.Severity == AttackPreviewSeverity.Error ? AttackPreviewCellOutcome.Fail : AttackPreviewCellOutcome.Warning));
                GUI.Label(rangeRect, new GUIContent("", $"{range.Hand}: {range.StartSeconds:F3}–{range.EndSeconds:F3}s ({range.Severity})"));
            }
            EditorGUI.DrawRect(new Rect(bar.x + _detailTime * bar.width, bar.y, 1, bar.height), Color.white);
            if (Event.current.type == EventType.MouseDown && bar.Contains(Event.current.mousePosition))
            { _detailTime = Mathf.Clamp01((Event.current.mousePosition.x - bar.x) / bar.width); _followGlobal = false; Event.current.Use(); Repaint(); }
        }
        EditorGUILayout.LabelField("Ranges: Main upper / Off lower. Click a bar to scrub.", EditorStyles.miniLabel);
    }
    private static void DrawChecks(AttackPreviewReport report)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Worst measured checks", EditorStyles.boldLabel);
        foreach (var check in report.WorstChecks)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{check.Hand} / {check.Id}: {(check.IsViolation ? check.Severity.ToString() : "Pass")}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"Measured {check.MeasuredValue:G4} / threshold {check.Threshold:G4}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(check.Message, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
#endif
