using UnityEngine;

namespace CatchTheFruit
{
    [System.Serializable]
    public struct DifficultySettings
    {
        [Range(0.5f, 1.5f)] public float spawnIntervalMul;   // >1 = slower spawns
        [Range(0.5f, 2.0f)] public float fallSpeedMul;       // scales spawner speed mul
        [Min(0f)] public float gravityScale;       // per-fruit gravity
        [Min(0f)] public float maxFallSpeed;       // terminal velocity
        [Min(0f)] public float initialDownBoost;   // extra downward kick at spawn
    }

    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultySettings Current { get; private set; }
        public static bool HasCurrent { get; private set; }

        [Header("Presets (tweak to taste)")]
        public DifficultySettings easy = new DifficultySettings
        {
            spawnIntervalMul = 1.25f,
            fallSpeedMul = 0.85f,
            gravityScale = 1.6f,
            maxFallSpeed = 7.5f,
            initialDownBoost = 0f
        };
        public DifficultySettings medium = new DifficultySettings
        {
            spawnIntervalMul = 1.0f,
            fallSpeedMul = 1.0f,
            gravityScale = 1.9f,
            maxFallSpeed = 9.0f,
            initialDownBoost = 0.5f
        };
        public DifficultySettings hard = new DifficultySettings
        {
            spawnIntervalMul = 0.85f,
            fallSpeedMul = 1.15f,
            gravityScale = 2.2f,
            maxFallSpeed = 11f,
            initialDownBoost = 0.8f
        };

        [Header("Scene refs (optional but handy)")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject difficultyPanel;
        [SerializeField] private GameObject player; // activates on start, hides on game over

        FruitSpawner _spawner;

        void Awake()
        {
            _spawner = FindObjectOfType<FruitSpawner>();
            HasCurrent = false;
            if (player) player.SetActive(false); // hidden at boot
        }

        void OnEnable() { GameEvents.OnGameOver += OnGameOver; }
        void OnDisable() { GameEvents.OnGameOver -= OnGameOver; }

        public void PickEasy() => ApplyAndStart(easy);
        public void PickMedium() => ApplyAndStart(medium);
        public void PickHard() => ApplyAndStart(hard);

        void ApplyAndStart(DifficultySettings s)
        {
            Current = s;
            HasCurrent = true;

            if (_spawner)
            {
                _spawner.SetIntervalMultiplier(s.spawnIntervalMul);
                _spawner.SetSpeedMultiplier(s.fallSpeedMul);
            }

            if (difficultyPanel) difficultyPanel.SetActive(false);
            if (startPanel) startPanel.SetActive(false);
            if (player) player.SetActive(true);

            GameEvents.RaiseGameStart();
        }

        void OnGameOver()
        {
            if (player) player.SetActive(false);
            HasCurrent = false;
        }

        public static void ClearCurrent() { HasCurrent = false; }
    }
}
