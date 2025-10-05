// Assets/_Project/Scripts/Quests/QuestDef.cs
using UnityEngine;

namespace CatchTheFruit
{
    public enum QuestType
    {
        CatchFruits,      // count non-bomb fruits caught
        CatchBombs,       // count bombs caught (usually via shield)
        SurviveSeconds,   // count survival time (seconds)
        ScorePoints       // optional: total score gained
    }

    /// <summary>
    /// Scriptable definition for a quest or an achievement.
    /// Use a unique ID so progress can be saved.
    /// </summary>
    [CreateAssetMenu(menuName = "CatchTheFruit/Quest")]
    public class QuestDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique stable id used as a save key (no spaces).")]
        public string id = "daily_catch_50";

        [Tooltip("Title shown in the UI, e.g. 'Catch 50 fruits'.")]
        public string title = "Catch 50 fruits";

        [Header("Goal")]
        public QuestType type = QuestType.CatchFruits;
        [Min(1)] public int target = 50;

        [Header("Rewards")]
        [Min(0)] public int rewardCoins = 10;

        [Header("Category")]
        [Tooltip("Check this if it’s a long-term achievement instead of a daily quest.")]
        public bool isAchievement = false;
    }
}
