using System.Collections;
using UnityEngine;
using static CatchTheFruit.GameEvents;

namespace CatchTheFruit
{
    /// <summary>
    /// Rotates short challenges. No SpawnTable dependency.
    /// - BananaBlitz: bananas grant extra points
    /// - BombStorm: (message only; no spawn bias without table helpers)
    /// - GoldenTime: all catches can earn a flat bonus + optional tint (Fruit.cs reads it)
    /// </summary>
    public class ChallengeDirector : MonoBehaviour
    {
        [Header("Timing")]
        public float intervalBetween = 30f; // wait between challenges
        public float duration = 12f; // challenge duration

        [Header("Banana Blitz")]
        public string bananaId = "banana"; // match your FruitData.id
        public int bananaBonus = 50;

        [Header("Golden Time")]
        public int goldenBonus = 100;
        public Color goldenTint = new Color(1f, 0.95f, 0.2f, 1f);

        public static ChallengeKind Active { get; private set; } = ChallengeKind.None;

        void OnEnable()
        {
            GameEvents.OnGameStart += StartRoutine;
            GameEvents.OnGameOver += StopRoutine;
            GameEvents.OnFruitCaught += OnFruitCaught;
        }
        void OnDisable()
        {
            GameEvents.OnGameStart -= StartRoutine;
            GameEvents.OnGameOver -= StopRoutine;
            GameEvents.OnFruitCaught -= OnFruitCaught;
            StopAllCoroutines();
            Active = ChallengeKind.None;
        }

        void StartRoutine()
        {
            StopAllCoroutines();
            StartCoroutine(RunLoop());
        }
        void StopRoutine()
        {
            StopAllCoroutines();
            if (Active != ChallengeKind.None)
                GameEvents.RaiseChallengeEnded(Active);
            Active = ChallengeKind.None;
        }

        IEnumerator RunLoop()
        {
            yield return new WaitForSecondsRealtime(10f); // settle period

            while (true)
            {
                yield return new WaitForSecondsRealtime(intervalBetween);

                var pick = (ChallengeKind)Random.Range(1, 4); // 1..3
                Active = pick;
                GameEvents.RaiseChallengeStarted(pick);

                switch (pick)
                {
                    case ChallengeKind.BananaBlitz:
                        GameEvents.RaiseWaveMessage("🍌 Banana Blitz!", 1.6f);
                        break;
                    case ChallengeKind.BombStorm:
                        GameEvents.RaiseWaveMessage("💣 Bomb Storm!", 1.6f);
                        break;
                    case ChallengeKind.GoldenTime:
                        GameEvents.RaiseWaveMessage("⭐ Golden Time!", 1.6f);
                        break;
                }

                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                GameEvents.RaiseChallengeEnded(Active);
                Active = ChallengeKind.None;
            }
        }

        // ----- Hooks used by Fruit.cs -----

        // Adds extra points while Active == GoldenTime, also provides a tint
        public int GoldenCatchBonus(FruitData fd, out Color? tint)
        {
            tint = null;
            if (Active != ChallengeKind.GoldenTime) return 0;
            tint = goldenTint;
            return goldenBonus;
        }

        // Grant banana bonus during BananaBlitz (no spawn bias)
        void OnFruitCaught(string id, int baseScore, bool isBomb)
        {
            if (Active == ChallengeKind.BananaBlitz && !isBomb && id == bananaId)
            {
                ScoreManager.Instance?.AddBulkPoints(bananaBonus);
            }
        }

        // Kept for compatibility with FruitSpawner; returns unchanged pick (no table ops)
        public FruitData BiasPick(FruitData picked, object _ignored) => picked;
    }
}
