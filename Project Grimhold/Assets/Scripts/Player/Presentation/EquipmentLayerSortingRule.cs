/// <summary>
/// Pure sorting rule shared by the modular character presentation.
/// Each equipment layer renders a fixed relative offset above the base body part
/// it follows, so it tracks the base part's sorting order dynamically (direction /
/// animation changes) while always staying deterministically above that base part.
///
/// The rule is intentionally per-layer and content-agnostic: it never depends on a
/// specific armor set, only on the base part's current sorting order. For it to work
/// without collisions the base parts must reserve a free slot above each of them
/// (see the NetworkPlayer prefab sorting layout and PlayerArmorSortingLayoutTests).
/// </summary>
public static class EquipmentLayerSortingRule
{
    /// <summary>
    /// Relative offset an equipment layer keeps above its base part. A single slot
    /// is enough because every base part carries exactly one equipment layer.
    /// </summary>
    public const int RelativeSortingOffset = 1;

    /// <summary>
    /// Resolves the sorting order an equipment layer must use to stay one slot above
    /// the given base part order. Preserved even when the base order changes at runtime.
    /// </summary>
    public static int ResolveEquipmentSortingOrder(int baseSortingOrder)
    {
        return baseSortingOrder + RelativeSortingOffset;
    }
}
