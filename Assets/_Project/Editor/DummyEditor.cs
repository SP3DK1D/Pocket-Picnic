#if UNITY_EDITOR
using UnityEditor;
static class DummyEditor
{
    [InitializeOnLoadMethod]
    static void Touch() { /* ensures Assembly-CSharp-Editor exists */ }
}
#endif
