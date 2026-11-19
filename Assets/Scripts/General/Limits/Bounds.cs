using UnityEngine;

public class Bounds : MonoBehaviour
{
    public static Bounds Instance { get; private set; }

    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private float height = 50f;
    [SerializeField] private float width = 50f;
    [SerializeField] private float fixedY = 1f;
    [SerializeField] private bool drawGizmos;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Vector3 OutOfBounds(Vector3 position)
    {
        Vector3 newPosition = position;

        float minX = center.x - width / 2f;
        float maxX = center.x + width / 2f;
        float minZ = center.z - height / 2f;
        float maxZ = center.z + height / 2f;

        if (position.x > maxX) newPosition.x = minX;
        if (position.x < minX) newPosition.x = maxX;
        if (position.z > maxZ) newPosition.z = minZ;
        if (position.z < minZ) newPosition.z = maxZ;

        newPosition.y = fixedY;
        return newPosition;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.green;
        Vector3 gizmoCenter = new Vector3(center.x, fixedY, center.z);
        Gizmos.DrawWireCube(gizmoCenter, new Vector3(width, 0f, height));
    }
}