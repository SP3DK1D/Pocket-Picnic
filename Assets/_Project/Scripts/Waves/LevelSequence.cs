using System.Collections.Generic;
using UnityEngine;

namespace CatchTheFruit
{
    [CreateAssetMenu(fileName = "LevelSequence", menuName = "CatchTheFruit/Level Sequence")]
    public class LevelSequence : ScriptableObject
    {
        public List<LevelDef> levels = new();
        public bool loop = true;
    }
}
