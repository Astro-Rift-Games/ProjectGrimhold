#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

public enum AttackPreviewEquipmentKind { OneHandMain, OneHandOff, TwoHand, DualWield, ShieldAndWeapon }
public enum AttackPreviewCellOutcome { Unvalidated, Pass, Warning, Fail }

/// <summary>Pure catalog, filter, report and timeline state. Rows preserve first catalog occurrence.</summary>
public sealed class AttackPreviewWindowModel
{
    private readonly IReadOnlyList<AttackPreviewCombination> _catalog;
    private readonly Dictionary<string, AttackPreviewReport> _reports = new Dictionary<string, AttackPreviewReport>();
    public AttackPreviewWindowModel(IReadOnlyList<AttackPreviewCombination> catalog)
    { _catalog = (catalog ?? throw new ArgumentNullException(nameof(catalog))).ToArray(); }
    private IReadOnlyList<AttackPreviewCombination> _visible;
    private IReadOnlyList<IReadOnlyList<AttackPreviewCombination>> _rows;
    private (string, AttackPreviewEquipmentKind?, AttackPreviewAction?, int?, AttackPreviewCellOutcome?) _filterKey;
    private int _reportRevision, _cacheRevision = -1;
    public string Search { get; set; } = "";
    public AttackPreviewEquipmentKind? EquipmentKind { get; set; }
    public AttackPreviewAction? Action { get; set; }
    public int? Facing { get; set; }
    public AttackPreviewCellOutcome? Outcome { get; set; }
    public AttackPreviewCombination Selection { get; set; }
    public float NormalizedTime { get; set; }
    public bool Playing { get; set; }
    public bool Loop { get; set; }
    public float Speed { get; set; } = 1f;
    public IReadOnlyList<AttackPreviewCombination> VisibleCells { get { EnsureFiltered(); return _visible; } }
    public IReadOnlyList<IReadOnlyList<AttackPreviewCombination>> Rows { get { EnsureFiltered(); return _rows; } }
    private void EnsureFiltered()
    {
        var key = (Search, EquipmentKind, Action, Facing, Outcome);
        if (_visible != null && key == _filterKey && _cacheRevision == _reportRevision) return;
        _visible = Array.AsReadOnly(_catalog.Where(Matches).ToArray());
        _rows = Array.AsReadOnly(_visible.GroupBy(c => (c.MainHand, c.OffHand, c.Action))
            .Select(g => (IReadOnlyList<AttackPreviewCombination>)Array.AsReadOnly(g.OrderBy(c => c.FacingIndex).ToArray())).ToArray());
        _filterKey = key;
        _cacheRevision = _reportRevision;
    }

    private bool Matches(AttackPreviewCombination c) =>
        (!EquipmentKind.HasValue || Classify(c) == EquipmentKind) &&
        (!Action.HasValue || c.Action == Action) && (!Facing.HasValue || c.FacingIndex == Facing) &&
        (!Outcome.HasValue || GetOutcome(c) == Outcome) &&
        (string.IsNullOrWhiteSpace(Search) || Contains(c.MainHand, Search.Trim()) || Contains(c.OffHand, Search.Trim()));
    private static bool Contains(LootDefinition item, string query) => item != null &&
        item.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    public static AttackPreviewEquipmentKind Classify(AttackPreviewCombination cell)
    {
        if (cell == null) throw new ArgumentNullException(nameof(cell));
        if (cell.MainHand?.WeaponDefinition?.Handedness == WeaponHandedness.TwoHanded) return AttackPreviewEquipmentKind.TwoHand;
        if (cell.MainHand == null) return AttackPreviewEquipmentKind.OneHandOff;
        if (cell.OffHand == null) return AttackPreviewEquipmentKind.OneHandMain;
        return cell.MainHand.Category == LootCategory.Shield || cell.OffHand.Category == LootCategory.Shield
            ? AttackPreviewEquipmentKind.ShieldAndWeapon : AttackPreviewEquipmentKind.DualWield;
    }

    /// <summary>Compare the attacking weapon in isolated Main/Off cells, without changing weapon or facing.</summary>
    public AttackPreviewCombination Counterpart(AttackPreviewCombination cell)
    {
        if (cell == null || cell.Action == AttackPreviewAction.Defend || Classify(cell) == AttackPreviewEquipmentKind.TwoHand) return null;
        bool off = cell.Action == AttackPreviewAction.OffAttack;
        var weapon = off ? cell.OffHand : cell.MainHand;
        return _catalog.FirstOrDefault(c => c.FacingIndex == cell.FacingIndex &&
            c.Action == (off ? AttackPreviewAction.MainAttack : AttackPreviewAction.OffAttack) &&
            (off ? c.MainHand == weapon && c.OffHand == null : c.OffHand == weapon && c.MainHand == null));
    }
    public void Advance(float deltaSeconds, float timelineDurationSeconds)
    {
        if (!Playing || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0 ||
            !float.IsFinite(timelineDurationSeconds) || timelineDurationSeconds <= 0) return;
        float next = NormalizedTime + deltaSeconds * Speed / timelineDurationSeconds;
        if (next >= 1f)
        {
            if (Loop) next %= 1f;
            else { next = 1f; Playing = false; }
        }
        NormalizedTime = Math.Clamp(next, 0f, 1f);
    }
    public void SetReport(AttackPreviewReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        _reports[report.Combination.Id] = report;
        _reportRevision++;
    }
    public AttackPreviewReport GetReport(string cellId) => _reports.TryGetValue(cellId, out var report) ? report : null;
    public AttackPreviewCellOutcome GetOutcome(AttackPreviewCombination cell)
    {
        var report = GetReport(cell.Id);
        return report == null ? AttackPreviewCellOutcome.Unvalidated : (AttackPreviewCellOutcome)((int)report.Outcome + 1);
    }
    /// <summary>Counts the entire catalog, independent of filters.</summary>
    public int Count(AttackPreviewCellOutcome outcome) => _catalog.Count(c => GetOutcome(c) == outcome);
}
#endif
