using UnityEngine;

namespace Grimhold.Combat.Presentation
{
    public static class CombatVfxPresentationMath
    {
        public static float CalculateRotation(CharacterVisualDirection direction, float rotationOffset)
        {
            float baseAngle = direction switch
            {
                CharacterVisualDirection.North => 90f,
                CharacterVisualDirection.NorthEast => 45f,
                CharacterVisualDirection.SouthEast => -45f,
                CharacterVisualDirection.South => -90f,
                CharacterVisualDirection.SouthWest => -135f,
                CharacterVisualDirection.NorthWest => 135f,
                _ => 0f
            };
            return baseAngle + rotationOffset;
        }
        
        public static bool ShouldMirror(CharacterVisualDirection direction)
        {
            return direction == CharacterVisualDirection.NorthWest || direction == CharacterVisualDirection.SouthWest;
        }

        public static Vector2 GetOrientedOffset(Vector2 canonicalDirection, Vector2 localOffset)
        {
            Vector2 right = canonicalDirection;
            Vector2 up = new Vector2(-right.y, right.x);
            return right * localOffset.x + up * localOffset.y;
        }
    }
}
