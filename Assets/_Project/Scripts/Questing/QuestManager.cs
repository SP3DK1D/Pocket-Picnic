// Assets/_Project/Scripts/Quests/QuestManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Central quest + coins manager (singleton, persisted).
    /// Features:
    ///  - 3 daily quests active at a time.
    ///  - When you complete a daily, a NEW daily is auto-picked for the SAME day
    ///    (no duplicates until we exhaust the pool).
    ///  - Auto-reset at local midnight.
    ///  - Persistent achievements with coin rewards.
    ///  - Coin wallet (Add/Spend).
    /// Hooks: GameEvents.OnFruitCaught, OnTimerTick, OnGameStart/Over
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("Catalogs")]
        [SerializeField] private List<QuestDef> dailyQuestPool = new(); // dailies only
        [SerializeField] private List<QuestDef> achievementDefs = new(); // achievements only

        [Header("Daily Settings")]
        [Min(1)] public int dailyCount = 3;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = false;

        // Runtime
        public readonly List<Quest> DailyQuests = new();
        public readonly List<Quest> Achievements = new();

        // Save keys
        const string KEY_COINS           = "ctf_coins";
        const string KEY_DAILY_DATE      = "ctf_daily_date";         // yyyyMMdd
        const string KEY_DAILY_ACTIVE    = "ctf_daily_ids_csv";      // id,id,id  (active)
        const string KEY_DAILY_USED      = "ctf_daily_used_csv";     // ids we've used today (for no-repeat)
        const string KEY_ACH_PROGRESS    = "ctf_ach_progress_";      // +id => int
        const string KEY_ACH_REWARDED    = "ctf_ach_rewarded_";      // +id => int(0/1)

        // Coins
        public int Coins
        {
            get => PlayerPrefs.GetInt(KEY_COINS, 0);
            private set { PlayerPrefs.SetInt(KEY_COINS, Mathf.Max(0, value)); PlayerPrefs.Save(); RaiseCoinsChanged(); }
        }

        // Session survival timer (unscaled)
        float _sessionSecondsUnscaled;

        // Rolling daily state
        readonly HashSet<string> _usedToday = new(); // quest ids already surfaced today

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BootstrapDailies();       // builds DailyQuests and _usedToday
            BootstrapAchievements();  // builds Achievements (progress loaded)
        }

        void OnEnable()
        {
            GameEvents.OnGameStart   += HandleGameStart;
            GameEvents.OnGameOver    += HandleGameOver;
            GameEvents.OnTimerTick   += HandleTimerTick;
            GameEvents.OnFruitCaught += HandleFruitCaught;
        }

        void OnDisable()
        {
            GameEvents.OnGameStart   -= HandleGameStart;
            GameEvents.OnGameOver    -= HandleGameOver;
            GameEvents.OnTimerTick   -= HandleTimerTick;
            GameEvents.OnFruitCaught -= HandleFruitCaught;
        }

        // ------------ Public API ------------
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            if (verboseLogs) Debug.Log($"[Coins] +{amount} -> {Coins}");
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (Coins < amount) return false;
            Coins -= amount;
            if (verboseLogs) Debug.Log($"[Coins] -{amount} -> {Coins}");
            return true;
        }

        public int GetCoins() => Coins;

        // ------------ Event hooks ------------
        void HandleGameStart() => _sessionSecondsUnscaled = 0f;

        void HandleGameOver()
        {
            // dump survival time
            int secs = Mathf.RoundToInt(_sessionSecondsUnscaled);
            if (secs > 0) AddProgress(QuestType.SurviveSeconds, secs);
            CheckCompletionsAndGrant(); // in case this finishes anything
        }

        void HandleTimerTick(float unscaledDelta)
        {
            _sessionSecondsUnscaled += Mathf.Max(0f, unscaledDelta);
        }

        void HandleFruitCaught(string id, int score, bool isBomb)
        {
            if (!isBomb) AddProgress(QuestType.CatchFruits, 1);
            if (isBomb)  AddProgress(QuestType.CatchBombs, 1);
            if (score > 0) AddProgress(QuestType.ScorePoints, score);
            CheckCompletionsAndGrant();
        }

        // ------------ Progress + Rewards ------------
        public void AddProgress(QuestType type, int amount)
        {
            if (amount == 0) return;

            // Dailies
            for (int i = 0; i < DailyQuests.Count; i++)
                if (DailyQuests[i].def && DailyQuests[i].def.type == type)
                    DailyQuests[i].AddProgress(amount);

            // Achievements
            for (int i = 0; i < Achievements.Count; i++)
            {
                var a = Achievements[i];
                if (a.def && a.def.type == type)
                {
                    a.AddProgress(amount);
                    SaveAchievementProgress(a);
                }
            }
        }

        void CheckCompletionsAndGrant()
        {
            // Daily: grant & roll new ones as they finish
            for (int i = 0; i < DailyQuests.Count; i++)
            {
                var q = DailyQuests[i];
                if (q.Completed && !q.rewardGranted)
                {
                    GrantReward(q);
                    RollNewDailyAtIndex(i); // auto-advance
                }
            }

            // Achievements: grant once
            for (int i = 0; i < Achievements.Count; i++)
            {
                var a = Achievements[i];
                if (a.Completed && !a.rewardGranted)
                {
                    GrantReward(a);
                    SaveAchievementRewarded(a);
                }
            }
        }

        void GrantReward(Quest q)
        {
            q.rewardGranted = true;
            if (q.def && q.def.rewardCoins > 0) AddCoins(q.def.rewardCoins);
            if (verboseLogs) Debug.Log($"[Quest] Completed: {q.def?.title} (+{q.def?.rewardCoins} coins)");
        }

        // ------------ Daily bootstrap/reset ------------
        void BootstrapDailies()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            string savedDay = PlayerPrefs.GetString(KEY_DAILY_DATE, "");

            DailyQuests.Clear();
            _usedToday.Clear();

            // filter catalog to dailies only (safety)
            dailyQuestPool.RemoveAll(q => !q || q.isAchievement);

            if (savedDay != today)
            {
                // new day → pick fresh set
                var active = PickDistinctDailies(dailyCount, exclude: null, out var used);
                SaveDailyState(today, active, used);
                BuildDailyInstances(active);
            }
            else
            {
                // same day → load active/used
                var active = LoadCsvIds(KEY_DAILY_ACTIVE);
                var used   = LoadCsvIds(KEY_DAILY_USED);
                foreach (var u in used) _usedToday.Add(u);

                // if something went wrong, repick
                if (active.Count == 0)
                {
                    active = PickDistinctDailies(dailyCount, exclude: null, out var used2);
                    SaveDailyState(today, active, used2);
                }
                BuildDailyInstances(active);
            }
        }

        void BuildDailyInstances(List<string> ids)
        {
            foreach (var id in ids)
            {
                var def = dailyQuestPool.Find(d => d && d.id == id);
                if (def) DailyQuests.Add(new Quest(def));
            }
        }

        void RollNewDailyAtIndex(int i)
        {
            // mark this quest id as "used"
            if (i < 0 || i >= DailyQuests.Count) return;
            var finishedId = DailyQuests[i].def?.id;
            if (!string.IsNullOrEmpty(finishedId)) _usedToday.Add(finishedId);

            // pick a replacement not currently active and not used (if possible)
            var activeIds = new HashSet<string>();
            for (int k = 0; k < DailyQuests.Count; k++)
            {
                if (k == i) continue; // we are replacing this one
                var id = DailyQuests[k].def?.id;
                if (!string.IsNullOrEmpty(id)) activeIds.Add(id);
            }

            var nextId = PickOneDaily(exclude: activeIds, preferNotUsed: true);
            if (string.IsNullOrEmpty(nextId))
            {
                // exhausted pool without repeats → allow repeats excluding currently active
                nextId = PickOneDaily(exclude: activeIds, preferNotUsed: false);
            }

            if (!string.IsNullOrEmpty(nextId))
            {
                var newDef = dailyQuestPool.Find(d => d && d.id == nextId);
                DailyQuests[i] = new Quest(newDef);
                _usedToday.Add(nextId);
                PersistActiveAndUsed(); // update disk
                if (verboseLogs) Debug.Log($"[Daily] Rolled new daily: {newDef.title}");
            }
        }

        // ------------ Picking helpers ------------
        List<string> PickDistinctDailies(int count, HashSet<string> exclude, out List<string> usedOut)
        {
            usedOut = new List<string>();
            var result = new List<string>();
            var pool = new List<QuestDef>(dailyQuestPool);

            // Remove excluded
            if (exclude != null)
                pool.RemoveAll(d => d && exclude.Contains(d.id));

            // Shuffle
            for (int n = 0; n < pool.Count; n++)
            {
                int j = UnityEngine.Random.Range(n, pool.Count);
                (pool[n], pool[j]) = (pool[j], pool[n]);
            }

            for (int i = 0; i < pool.Count && result.Count < count; i++)
            {
                if (pool[i] && !result.Contains(pool[i].id))
                    result.Add(pool[i].id);
            }

            // track used (these initial picks are used too)
            usedOut.AddRange(result);
            return result;
        }

        string PickOneDaily(HashSet<string> exclude, bool preferNotUsed)
        {
            // build candidate list
            var candidates = new List<QuestDef>();
            foreach (var d in dailyQuestPool)
            {
                if (!d) continue;
                if (exclude != null && exclude.Contains(d.id)) continue;
                if (preferNotUsed && _usedToday.Contains(d.id)) continue;
                candidates.Add(d);
            }

            // if none and preferNotUsed, relax constraint
            if (candidates.Count == 0 && preferNotUsed)
            {
                foreach (var d in dailyQuestPool)
                {
                    if (!d) continue;
                    if (exclude != null && exclude.Contains(d.id)) continue;
                    candidates.Add(d);
                }
            }

            if (candidates.Count == 0) return null;
            int at = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[at].id;
        }

        void SaveDailyState(string today, List<string> active, List<string> used)
        {
            PlayerPrefs.SetString(KEY_DAILY_DATE, today);
            PlayerPrefs.SetString(KEY_DAILY_ACTIVE, string.Join(",", active));
            PlayerPrefs.SetString(KEY_DAILY_USED, string.Join(",", used));
            PlayerPrefs.Save();

            _usedToday.Clear();
            for (int i = 0; i < used.Count; i++) _usedToday.Add(used[i]);
        }

        void PersistActiveAndUsed()
        {
            // serialize current active + usedToday
            var active = new List<string>(DailyQuests.Count);
            for (int i = 0; i < DailyQuests.Count; i++)
                if (DailyQuests[i].def) active.Add(DailyQuests[i].def.id);

            PlayerPrefs.SetString(KEY_DAILY_ACTIVE, string.Join(",", active));
            PlayerPrefs.SetString(KEY_DAILY_USED, string.Join(",", _usedToday));
            PlayerPrefs.Save();
        }

        List<string> LoadCsvIds(string key)
        {
            var csv = PlayerPrefs.GetString(key, "");
            var ids = new List<string>();
            if (string.IsNullOrEmpty(csv)) return ids;
            var split = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < split.Length; i++) ids.Add(split[i]);
            return ids;
        }

        // ------------ Achievements ------------
        void BootstrapAchievements()
        {
            Achievements.Clear();
            // safety: ensure these are actually achievements
            achievementDefs.RemoveAll(d => !d || !d.isAchievement);

            foreach (var def in achievementDefs)
            {
                var q = new Quest(def);
                q.progress = PlayerPrefs.GetInt(KEY_ACH_PROGRESS + def.id, 0);
                q.rewardGranted = PlayerPrefs.GetInt(KEY_ACH_REWARDED + def.id, 0) == 1;
                Achievements.Add(q);
            }
        }

        void SaveAchievementProgress(Quest a)
        {
            if (a.def == null) return;
            PlayerPrefs.SetInt(KEY_ACH_PROGRESS + a.def.id, Mathf.Max(0, a.progress));
            PlayerPrefs.Save();
        }

        void SaveAchievementRewarded(Quest a)
        {
            if (a.def == null) return;
            PlayerPrefs.SetInt(KEY_ACH_REWARDED + a.def.id, a.rewardGranted ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ------------ UI broadcast ------------
        public static event Action<int> OnCoinsChanged;
        void RaiseCoinsChanged() => OnCoinsChanged?.Invoke(Coins);
    }
}
