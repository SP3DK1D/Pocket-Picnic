using UnityEngine;
using TMPro;
using CatchTheFruit; // for GameEvents

/// <summary>
/// Auto-pops the attached TMP text whenever the score changes.
/// Requires a ScalePunch on the same GameObject.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class ScorePopper : MonoBehaviour
{
    private ScalePunch _punch;

    private void Reset() { _punch = GetComponent<ScalePunch>(); }
    private void Awake() { _punch = GetComponent<ScalePunch>(); }

    private void OnEnable()
    {
        if (_punch == null) _punch = GetComponent<ScalePunch>();
        GameEvents.OnScoreChanged += HandleScoreChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int newScore)
    {
        if (_punch != null) _punch.Play();
    }
}
