using UnityEngine;

/// <summary>
/// Simple camera follow used by NetworkPlayer to track the local player.
/// </summary>
public class CameraFollower : MonoBehaviour
{
    public Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + offset;
    }
}
