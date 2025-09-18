using UnityEngine;
using System.Collections;

public class EyeBlink : MonoBehaviour
{
    [SerializeField] private SpriteRenderer openEyes;
    [SerializeField] private SpriteRenderer closedEyes;
    [SerializeField] private float blinkDuration = 0.15f;
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 5f;

    void OnEnable()
    {
        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
            // close
            openEyes.enabled = false;
            closedEyes.enabled = true;
            yield return new WaitForSeconds(blinkDuration);
            // open
            openEyes.enabled = true;
            closedEyes.enabled = false;
        }
    }
}
