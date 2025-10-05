#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatchTheFruit.EditorTools
{
    /// <summary>
    /// One-click setup for MANY dailies + achievements + coin fruit + wiring.
    /// Menu: Tools/Pocket Picnic/Generate Big Quest Catalog
    /// </summary>
    public static class QuestSetupWizard
    {
        private const string RootFolder   = "Assets/_Game";
        private const string QuestsFolder = RootFolder + "/Quests";
        private const string DailyFolder  = QuestsFolder + "/Daily";
        private const string AchFolder    = QuestsFolder + "/Achievements";
        private const string FruitFolder  = RootFolder + "/Fruit";

        [MenuItem("Tools/Pocket Picnic/Generate Big Quest Catalog")]
        public static void Generate()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                EnsureFolders();

                // 1) Build catalogs
                var dailies = CreateDailyQuestAssets();
                var achs    = CreateAchievementAssets();

                // 2) Make coin FruitData
                var coinFd = CreateCoinFruitData();

                // 3) Wire QuestManager
                var qm = FindOrCreateQuestManager();
                WireQuestManager(qm, dailies, achs);

                // 4) Add coin to a SpawnTable (optional)
                TryAddCoinToSpawnTable(coinFd);

                Debug.Log("<b>[QuestSetup]</b> Big catalog generated. Open the Quest screen to see them.");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (EditorSceneManager.GetActiveScene().isLoaded)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        // ---------- Folders ----------
        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets", "_Game");
            CreateFolderIfMissing(RootFolder, "Quests");
            CreateFolderIfMissing(QuestsFolder, "Daily");
            CreateFolderIfMissing(QuestsFolder, "Achievements");
            CreateFolderIfMissing(RootFolder, "Fruit");
        }

        private static void CreateFolderIfMissing(string parent, string child)
        {
            var full = Path.Combine(parent, child).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }

        // ---------- Quest creators ----------
        private static List<QuestDef> CreateDailyQuestAssets()
        {
            var list = new List<QuestDef>();

            // Catch fruits
            list.Add(MakeDaily("daily_catch_30",   "Catch 30 fruits",   QuestType.CatchFruits, 30,   12));
            list.Add(MakeDaily("daily_catch_50",   "Catch 50 fruits",   QuestType.CatchFruits, 50,   20));
            list.Add(MakeDaily("daily_catch_80",   "Catch 80 fruits",   QuestType.CatchFruits, 80,   35));
            list.Add(MakeDaily("daily_catch_120",  "Catch 120 fruits",  QuestType.CatchFruits, 120,  55));
            list.Add(MakeDaily("daily_catch_150",  "Catch 150 fruits",  QuestType.CatchFruits, 150,  70));
            list.Add(MakeDaily("daily_catch_200",  "Catch 200 fruits",  QuestType.CatchFruits, 200,  95));

            // Score
            list.Add(MakeDaily("daily_score_300",  "Score 300 points",  QuestType.ScorePoints, 300,  12));
            list.Add(MakeDaily("daily_score_600",  "Score 600 points",  QuestType.ScorePoints, 600,  24));
            list.Add(MakeDaily("daily_score_900",  "Score 900 points",  QuestType.ScorePoints, 900,  36));
            list.Add(MakeDaily("daily_score_1500", "Score 1,500 points",QuestType.ScorePoints, 1500, 60));
            list.Add(MakeDaily("daily_score_2000", "Score 2,000 points",QuestType.ScorePoints, 2000, 80));

            // Survive
            list.Add(MakeDaily("daily_survive_60", "Survive 60 seconds", QuestType.SurviveSeconds,  60, 10));
            list.Add(MakeDaily("daily_survive_90", "Survive 90 seconds", QuestType.SurviveSeconds,  90, 16));
            list.Add(MakeDaily("daily_survive_120","Survive 120 seconds",QuestType.SurviveSeconds, 120, 24));
            list.Add(MakeDaily("daily_survive_180","Survive 180 seconds",QuestType.SurviveSeconds, 180, 36));

            // Bombs (if your design allows catching bombs)
            list.Add(MakeDaily("daily_bombs_3",    "Catch 3 bombs",      QuestType.CatchBombs, 3,   18));
            list.Add(MakeDaily("daily_bombs_5",    "Catch 5 bombs",      QuestType.CatchBombs, 5,   30));

            return list;
        }

        private static List<QuestDef> CreateAchievementAssets()
        {
            var list = new List<QuestDef>();

            // Catch fruits milestones
            list.Add(MakeAch("ach_catch_500",     "Catch 500 fruits",     QuestType.CatchFruits,  500,   60));
            list.Add(MakeAch("ach_catch_1000",    "Catch 1,000 fruits",    QuestType.CatchFruits,  1000,  120));
            list.Add(MakeAch("ach_catch_2500",    "Catch 2,500 fruits",    QuestType.CatchFruits,  2500,  300));
            list.Add(MakeAch("ach_catch_5000",    "Catch 5,000 fruits",    QuestType.CatchFruits,  5000,  600));
            list.Add(MakeAch("ach_catch_10000",   "Catch 10,000 fruits",   QuestType.CatchFruits, 10000, 1200));
            list.Add(MakeAch("ach_catch_20000",   "Catch 20,000 fruits",   QuestType.CatchFruits, 20000, 2600));

            // Score totals
            list.Add(MakeAch("ach_score_5000",    "Score 5,000 points",    QuestType.ScorePoints,  5000,   80));
            list.Add(MakeAch("ach_score_15000",   "Score 15,000 points",   QuestType.ScorePoints,  15000,  220));
            list.Add(MakeAch("ach_score_50000",   "Score 50,000 points",   QuestType.ScorePoints,  50000,  800));
            list.Add(MakeAch("ach_score_100000",  "Score 100,000 points",  QuestType.ScorePoints, 100000, 1500));
            list.Add(MakeAch("ach_score_250000",  "Score 250,000 points",  QuestType.ScorePoints, 250000, 4200));

            // Survival totals (aggregate)
            list.Add(MakeAch("ach_survive_600",   "Survive 10 minutes (total)",  QuestType.SurviveSeconds,  600,  120));
            list.Add(MakeAch("ach_survive_1800",  "Survive 30 minutes (total)",  QuestType.SurviveSeconds, 1800,  360));
            list.Add(MakeAch("ach_survive_3600",  "Survive 60 minutes (total)",  QuestType.SurviveSeconds, 3600,  720));
            list.Add(MakeAch("ach_survive_7200",  "Survive 120 minutes (total)", QuestType.SurviveSeconds, 7200, 1400));

            // Bombs
            list.Add(MakeAch("ach_bombs_20",      "Catch 20 bombs",        QuestType.CatchBombs,  20,  120));
            list.Add(MakeAch("ach_bombs_50",      "Catch 50 bombs",        QuestType.CatchBombs,  50,  300));
            list.Add(MakeAch("ach_bombs_100",     "Catch 100 bombs",       QuestType.CatchBombs, 100,  900));

            return list;
        }

        private static QuestDef MakeDaily(string id, string title, QuestType type, int target, int reward)
            => GetOrCreateQuestDef(DailyFolder, $"Q_{id}.asset", id, title, type, target, reward, false);

        private static QuestDef MakeAch(string id, string title, QuestType type, int target, int reward)
            => GetOrCreateQuestDef(AchFolder, $"Q_{id}.asset", id, title, type, target, reward, true);

        private static QuestDef GetOrCreateQuestDef(
            string folder, string fileName, string id, string title,
            QuestType type, int target, int reward, bool isAchievement)
        {
            string path = $"{folder}/{fileName}";
            var def = AssetDatabase.LoadAssetAtPath<QuestDef>(path);
            if (!def)
            {
                def = ScriptableObject.CreateInstance<QuestDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.id = id;
            def.title = title;
            def.type = type;
            def.target = Mathf.Max(1, target);
            def.rewardCoins = Mathf.Max(0, reward);
            def.isAchievement = isAchievement;

            EditorUtility.SetDirty(def);
            return def;
        }

        // ---------- Coin FruitData ----------
        private static FruitData CreateCoinFruitData()
        {
            string path = $"{FruitFolder}/FD_Coin.asset";
            var fd = AssetDatabase.LoadAssetAtPath<FruitData>(path);
            if (!fd)
            {
                fd = ScriptableObject.CreateInstance<FruitData>();
                AssetDatabase.CreateAsset(fd, path);
            }

            fd.id = "coin";
            fd.isBomb = false;
            fd.scoreValue = 0;
            fd.tint = Color.white;
            fd.minFallSpeed = 3.5f;
            fd.maxFallSpeed = 6.5f;
            fd.weight = 2f; // rare-ish; tweak in Inspector
            EditorUtility.SetDirty(fd);
            return fd;
        }

        // ---------- QuestManager wiring ----------
        private static QuestManager FindOrCreateQuestManager()
        {
            QuestManager qm = null;

            // Unity 2023+: use FindFirstObjectByType with enum
#if UNITY_2023_1_OR_NEWER
            qm = Object.FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
            qm = Object.FindObjectOfType<QuestManager>();
#pragma warning restore 618
#endif
            if (!qm)
            {
                var go = new GameObject("~QuestManager");
                qm = go.AddComponent<QuestManager>();
                Undo.RegisterCreatedObjectUndo(go, "Create QuestManager");
            }
            return qm;
        }

        private static void WireQuestManager(QuestManager qm, List<QuestDef> dailies, List<QuestDef> achs)
        {
            if (!qm) return;
            var so = new SerializedObject(qm);
            so.FindProperty("dailyCount").intValue = 3;

            var dailyProp = so.FindProperty("dailyQuestPool");
            dailyProp.ClearArray();
            for (int i = 0; i < dailies.Count; i++)
            {
                dailyProp.InsertArrayElementAtIndex(i);
                dailyProp.GetArrayElementAtIndex(i).objectReferenceValue = dailies[i];
            }

            var achProp = so.FindProperty("achievementDefs");
            achProp.ClearArray();
            for (int i = 0; i < achs.Count; i++)
            {
                achProp.InsertArrayElementAtIndex(i);
                achProp.GetArrayElementAtIndex(i).objectReferenceValue = achs[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qm);
        }

        // ---------- SpawnTable update (optional) ----------
        private static void TryAddCoinToSpawnTable(FruitData coinFd)
        {
            if (!coinFd) return;

            string[] guids = AssetDatabase.FindAssets("t:SpawnTable");
            if (guids == null || guids.Length == 0) return;

            var st = AssetDatabase.LoadAssetAtPath<SpawnTable>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (!st) return;

            var so = new SerializedObject(st);
            var entries = so.FindProperty("entries");

            // If coin already there, skip
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("fruit").objectReferenceValue == coinFd) return;
            }

            // Add coin entry
            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var el = entries.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("fruit").objectReferenceValue = coinFd;
            el.FindPropertyRelative("weight").floatValue = 2f;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(st);
        }
    }
}
#endif
