using UnityEngine;

public class SoftSnapIntoBasket : MonoBehaviour
{
    [SerializeField] float pullSpeed = 12f;      // tweak 8�16
    [SerializeField] LayerMask fruitMask;

    void OnTriggerStay2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & fruitMask.value) == 0) return;
        if (!other.attachedRigidbody) return;

        Vector2 center = transform.position;
        Vector2 toCenter = center - other.attachedRigidbody.position;
        float t = Mathf.InverseLerp(1.2f, 0f, toCenter.magnitude); // stronger near center
        other.attachedRigidbody.linearVelocity += toCenter.normalized * (pullSpeed * t) * Time.deltaTime;
    }
}
