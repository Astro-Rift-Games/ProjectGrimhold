// src/config/progressionBalance.js
//
// Balance constants mirrored from ProgressionBalanceDefaults.cs (Unity).
// These values MUST remain in sync with the C# counterparts.
// If the curve or per-level grant changes in Unity, update this file too.

'use strict';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const INITIAL_LEVEL = 1;
const ATTRIBUTE_POINTS_PER_LEVEL = 1;

// XP required to advance from level N to level N+1.
// Index 0 = requirement to go from level 1 → 2, etc.
// Mirrors ProgressionBalanceDefaults.CreateInitialExperienceCurve() in C#.
const XP_REQUIREMENTS = [
  100, 105, 110, 115, 120, 126, 132, 138, 144, 151,
  158, 165, 173, 181, 190, 199, 208, 218, 228, 239,
  250, 262, 275, 288, 302, 317, 332, 348, 365
];

const MAX_LEVEL = INITIAL_LEVEL + XP_REQUIREMENTS.length; // 30

// ---------------------------------------------------------------------------
// Pure computation functions
// ---------------------------------------------------------------------------

/**
 * Given a character's current progression state and the consolidated XP
 * earned in a raid, computes the resulting level and leftover XP.
 *
 * Mirrors ConsolidatedExperienceApplicationRules.TryApply() in Unity.
 *
 * @param {number} currentLevel   - Character's level before the raid (>= 1).
 * @param {number} currentXp      - Character's current XP before the raid (>= 0).
 * @param {number} consolidatedXp - XP amount to apply (>= 0).
 * @returns {{ resultingLevel: number, resultingExperience: number }}
 */
function computeLevelAndExperience(currentLevel, currentXp, consolidatedXp) {
  let level = currentLevel;
  let xp    = currentXp + consolidatedXp;

  // Level up as many times as the XP allows, stopping at max level.
  while (level < MAX_LEVEL) {
    const requiredXp = XP_REQUIREMENTS[level - INITIAL_LEVEL];
    if (xp < requiredXp) break;
    xp -= requiredXp;
    level += 1;
  }

  // At max level, excess XP is discarded.
  if (level >= MAX_LEVEL) {
    xp = 0;
  }

  return { resultingLevel: level, resultingExperience: xp };
}

/**
 * Computes how many attribute points to grant based on levels gained.
 *
 * Mirrors CharacterAttributePointGrantRules.TryApply() in Unity:
 * 1 point per level gained.
 *
 * @param {number} previousLevel  - Level before the raid.
 * @param {number} resultingLevel - Level after the raid.
 * @returns {number} Points to add to availablePoints.
 */
function computeAttributePointsGranted(previousLevel, resultingLevel) {
  const levelsGained = Math.max(0, resultingLevel - previousLevel);
  return levelsGained * ATTRIBUTE_POINTS_PER_LEVEL;
}

// ---------------------------------------------------------------------------
// Exports
// ---------------------------------------------------------------------------

module.exports = {
  INITIAL_LEVEL,
  MAX_LEVEL,
  ATTRIBUTE_POINTS_PER_LEVEL,
  XP_REQUIREMENTS,
  computeLevelAndExperience,
  computeAttributePointsGranted,
};
