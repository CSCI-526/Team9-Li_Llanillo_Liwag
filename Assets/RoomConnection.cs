using UnityEngine;

[RequireComponent(typeof(RoomDrag))]
public class RoomConnection : MonoBehaviour
{
    [SerializeField] private Transform snapTarget;
    [SerializeField] private SpriteRenderer roomArea;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private GameObject roomDoor;
    [SerializeField] private GameObject fixedDoor;

    private Bounds fixedDoorBounds;
    private bool connected;

    private void Awake()
    {
        // Cache the fixed doorway area before hiding its wall.
        fixedDoorBounds = fixedDoor.GetComponent<SpriteRenderer>().bounds;
    }

    private void Start()
    {
        RefreshConnection();
    }

    public bool CanStartDrag()
    {
        Bounds playerBounds = playerCollider.bounds;
        playerBounds.Expand(0.04f);

        // Keep the room locked while any part of the player is inside.
        if (Overlaps2D(playerBounds, roomArea.bounds))
            return false;

        // Also protect a player who is crossing the doorway.
        if (connected && Overlaps2D(playerBounds, fixedDoorBounds))
            return false;

        return true;
    }

    public void Disconnect()
    {
        SetConnected(false);
    }

    public void RefreshConnection()
    {
        bool aligned = Vector3.Distance(
            transform.position,
            snapTarget.position
        ) < 0.01f;

        SetConnected(aligned);
    }

    private void SetConnected(bool value)
    {
        connected = value;

        // Opening the passage hides both walls and their colliders.
        roomDoor.SetActive(!connected);
        fixedDoor.SetActive(!connected);
    }

    private static bool Overlaps2D(Bounds a, Bounds b)
    {
        return a.min.x < b.max.x && a.max.x > b.min.x &&
               a.min.y < b.max.y && a.max.y > b.min.y;
    }
}