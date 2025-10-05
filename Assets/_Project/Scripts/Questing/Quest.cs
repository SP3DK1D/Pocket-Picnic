// Assets/_Project/Scripts/Quests/Quest.cs
using System;

namespace CatchTheFruit
{
    /// <summary>
    /// Runtime instance of a quest/achievement with progress tracking.
    /// </summary>
    [Serializable]
    public class Quest
    {
        public QuestDef def;
        public int progress;
        public bool rewardGranted;

        public bool Completed => progress >= (def ? def.target : int.MaxValue);

        public Quest(QuestDef def)
        {
            this.def = def;
            progress = 0;
            rewardGranted = false;
        }

        public void AddProgress(int amount)
        {
            if (Completed) return;
            progress += amount;
            if (progress < 0) progress = 0;
        }

        public float Progress01 => (def && def.target > 0) ? (float)progress / def.target : 0f;
    }
}
