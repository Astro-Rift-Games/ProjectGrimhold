using System.Reflection;
using NUnit.Framework;
using ProjectGrimhold.EditorTools.RuleTiles;
using UnityEngine;

namespace Tests.EditMode.RuleTileBuilder
{
    public sealed class RuleTileBuilderTooltipTests
    {
        [TestCase("1  Sprites")]
        [TestCase("2  Template")]
        [TestCase("3  Assign pieces")]
        [TestCase("4  Paint & test")]
        [TestCase("5  Save")]
        [TestCase("Back")]
        [TestCase("Next")]
        [TestCase("Open existing")]
        [TestCase("New")]
        [TestCase("Reload")]
        [TestCase("Add source")]
        [TestCase("Add selected assets")]
        [TestCase("Search")]
        [TestCase("Drop sprites / sliced textures here")]
        [TestCase("Fallback sprite")]
        [TestCase("Default collider")]
        [TestCase("Use selected sprite as fallback")]
        [TestCase("Continuous floor")]
        [TestCase("Orthogonal connections (16)")]
        [TestCase("Full contour & inner corners (47)")]
        [TestCase("Assign selected")]
        [TestCase("Details")]
        [TestCase("Rectangle")]
        [TestCase("L")]
        [TestCase("Hole")]
        [TestCase("Corridor")]
        [TestCase("Isolated")]
        [TestCase("Erase")]
        [TestCase("Paint surface")]
        [TestCase("Other tile")]
        [TestCase("Highlight fallback cells")]
        [TestCase("Asset name")]
        [TestCase("Save changes to original asset")]
        [TestCase("Generate native RuleTile…")]
        [TestCase("Select saved asset in Project")]
        [TestCase("Advanced — edit native rules")]
        [TestCase("Add rule")]
        [TestCase("Duplicate")]
        [TestCase("Delete")]
        [TestCase("Move earlier ↑")]
        [TestCase("Move later ↓")]
        [TestCase("Assign selected sprites")]
        public void NamedControl_HasHelpfulTooltipWithoutChangingItsLabel(string label)
        {
            var content = Content(label, null);
            Assert.That(content.text, Is.EqualTo(label));
            Assert.That(content.tooltip, Is.Not.Null.And.Not.Empty);
            Assert.That(content.tooltip.Length, Is.GreaterThanOrEqualTo(30));
        }

        [TestCase("Clear", "Clear library")]
        [TestCase("Clear", "Clear piece")]
        [TestCase("Clear", "Clear preview")]
        [TestCase("", "Sprite selection")]
        [TestCase("", "Piece shape")]
        [TestCase("", "Preview canvas")]
        [TestCase("Priority 1 · Single", "Rule priority")]
        [TestCase("", "Native neighbor")]
        [TestCase("", "Native transform")]
        [TestCase("", "Native object")]
        [TestCase("", "Native collider")]
        [TestCase("", "Native output")]
        [TestCase("", "Native minimum speed")]
        [TestCase("", "Native maximum speed")]
        [TestCase("", "Native noise")]
        [TestCase("", "Native random transform")]
        [TestCase("", "Native count")]
        [TestCase("", "Native sprite")]
        public void ContextualTooltip_PreservesDisplayText(string label, string key)
        {
            var content = Content(label, key);
            Assert.That(content.text, Is.EqualTo(label));
            Assert.That(content.tooltip.Length, Is.GreaterThanOrEqualTo(30));
        }

        [Test]
        public void ClearControls_ExplainTheirDifferentDestructiveBoundaries()
        {
            var library = Content("Clear", "Clear library").tooltip;
            var piece = Content("Clear", "Clear piece").tooltip;
            var preview = Content("Clear", "Clear preview").tooltip;
            Assert.That(library, Is.Not.EqualTo(piece).And.Not.EqualTo(preview));
            Assert.That(piece, Is.Not.EqualTo(preview));
            Assert.That(library, Does.Contain("library"));
            Assert.That(piece, Does.Contain("rule"));
            Assert.That(preview, Does.Contain("grid"));
        }

        private static GUIContent Content(string label, string key)
        {
            var method = typeof(RuleTileBuilderWindow).GetMethod("Help", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "The window needs contextual help for its existing controls.");
            return (GUIContent)method.Invoke(null, new object[] { label, key });
        }
    }
}
