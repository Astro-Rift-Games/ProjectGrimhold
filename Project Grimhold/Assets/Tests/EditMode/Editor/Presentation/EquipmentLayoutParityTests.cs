#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode.Presentation
{
    /// <summary>
    /// The Inventory tab's equipment panel must look exactly like the Attributes tab's: same grid, same
    /// slot distribution (including the empty cells) and same slot art. The Attributes sheet is the reference.
    /// </summary>
    public sealed class EquipmentLayoutParityTests
    {
        private const string AttributesPath = "Assets/Prefabs/TownAttributeAssignmentView.prefab";
        private const string InventoryPath = "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab";

        private static GridLayoutGroup AttributesGrid(GameObject prefab) =>
            prefab.transform.Find("Content/EquipmentColumn/EquipmentGrid").GetComponent<GridLayoutGroup>();

        private static GridLayoutGroup InventoryGrid(GameObject prefab) =>
            prefab.transform.Find("RaidInventoryScreen/PanelsRow/EquipmentPanel/EquipmentGrid").GetComponent<GridLayoutGroup>();

        private static string CellName(Transform cell) =>
            cell.name.StartsWith("Slot_") ? cell.name.Substring("Slot_".Length) : cell.name.StartsWith("Spacer") ? "Spacer" : cell.name;

        [Test]
        public void Grids_UseTheSameCellSizeSpacingAndColumns()
        {
            GridLayoutGroup reference = AttributesGrid(AssetDatabase.LoadAssetAtPath<GameObject>(AttributesPath));
            GridLayoutGroup inventory = InventoryGrid(AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath));

            Assert.That(inventory.cellSize, Is.EqualTo(reference.cellSize));
            Assert.That(inventory.spacing, Is.EqualTo(reference.spacing));
            Assert.That(inventory.constraint, Is.EqualTo(reference.constraint));
            Assert.That(inventory.constraintCount, Is.EqualTo(reference.constraintCount));
            Assert.That(inventory.startCorner, Is.EqualTo(reference.startCorner));
            Assert.That(inventory.startAxis, Is.EqualTo(reference.startAxis));
            Assert.That(inventory.childAlignment, Is.EqualTo(reference.childAlignment));
            Assert.That(inventory.padding.left, Is.EqualTo(reference.padding.left));
            Assert.That(inventory.padding.top, Is.EqualTo(reference.padding.top));
        }

        [Test]
        public void Grids_PlaceEverySlotAndEmptyCellInTheSameOrder()
        {
            GridLayoutGroup reference = AttributesGrid(AssetDatabase.LoadAssetAtPath<GameObject>(AttributesPath));
            GridLayoutGroup inventory = InventoryGrid(AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath));

            List<string> expected = reference.transform.Cast<Transform>().Select(CellName).ToList();
            List<string> actual = inventory.transform.Cast<Transform>().Select(CellName).ToList();

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.Count(name => name != "Spacer"), Is.EqualTo(8));
        }

        [Test]
        public void SlotCells_UseTheSameFrameArtAndIconPlacement()
        {
            GameObject attributes = AssetDatabase.LoadAssetAtPath<GameObject>(AttributesPath);
            GameObject inventory = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath);
            Transform referenceCell = AttributesGrid(attributes).transform.Find("Slot_Helmet");
            Image referenceFrame = referenceCell.GetComponent<Image>();
            var referenceIcon = (RectTransform)referenceCell.Find("Icon");

            foreach (Transform cell in InventoryGrid(inventory).transform)
            {
                if (CellName(cell) == "Spacer")
                {
                    continue;
                }

                Image frame = cell.GetComponent<Image>();
                Assert.That(frame.sprite, Is.SameAs(referenceFrame.sprite), $"{cell.name} frame art");
                Assert.That(frame.type, Is.EqualTo(referenceFrame.type), $"{cell.name} frame type");

                var icon = (RectTransform)cell.Find("Icon");
                Assert.That(icon.anchorMin, Is.EqualTo(referenceIcon.anchorMin), $"{cell.name} icon anchorMin");
                Assert.That(icon.anchorMax, Is.EqualTo(referenceIcon.anchorMax), $"{cell.name} icon anchorMax");
                Assert.That(icon.offsetMin, Is.EqualTo(referenceIcon.offsetMin), $"{cell.name} icon offsetMin");
                Assert.That(icon.offsetMax, Is.EqualTo(referenceIcon.offsetMax), $"{cell.name} icon offsetMax");
                Assert.That(
                    icon.GetComponent<Image>().preserveAspect,
                    Is.EqualTo(referenceIcon.GetComponent<Image>().preserveAspect),
                    $"{cell.name} icon aspect");
            }
        }

        [Test]
        public void EquipmentPanel_IsWideEnoughForTheGrid()
        {
            GameObject inventory = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath);
            GridLayoutGroup grid = InventoryGrid(inventory);
            var panel = (RectTransform)grid.transform.parent;
            int columns = grid.constraintCount;
            float gridWidth = columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;

            Assert.That(panel.sizeDelta.x, Is.GreaterThanOrEqualTo(gridWidth));
        }
    }
}
#endif
