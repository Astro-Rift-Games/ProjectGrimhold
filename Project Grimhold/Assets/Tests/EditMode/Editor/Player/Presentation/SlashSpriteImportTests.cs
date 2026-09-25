using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SlashSpriteImportTests
{
    private const string Path = "Assets/Art/VFX/VFX-Slash.png";

    [Test]
    public void SlashFrames_ImportInCellOrderWithSharedCenteredPivot()
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
        Assert.That(sprites, Has.Length.EqualTo(4));
        for (int index = 0; index < sprites.Length; index++)
        {
            Sprite sprite = sprites[index];
            Assert.That(sprite.name, Is.EqualTo($"VFX-Slash_{index}"));
            Assert.That(sprite.rect, Is.EqualTo(new Rect(index * 96, 0, 96, 96)), sprite.name);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48, 48)), sprite.name);
            Assert.That(sprite.bounds.size.x, Is.EqualTo(6f), sprite.name);
            Assert.That(sprite.bounds.size.y, Is.EqualTo(6f), sprite.name);
            Assert.That(sprite.bounds.center.x, Is.EqualTo(0f), sprite.name);
            Assert.That(sprite.bounds.center.y, Is.EqualTo(0f), sprite.name);
        }
    }
}
