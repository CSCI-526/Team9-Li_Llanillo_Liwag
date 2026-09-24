using UnityEngine;
using UnityEngine.InputSystem;

public class RoomDrag : MonoBehaviour
{
    [SerializeField] private Collider2D dragArea;
    [SerializeField] private SpriteRenderer placementArea;
    [SerializeField] private SpriteRenderer[] blockedAreas;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Transform snapTarget;
    [SerializeField] private float snapDistance = 0.8f;

    private Camera sceneCamera;
    private RoomConnection roomConnection;
    private Vector3 dragOffset;
    private Vector3 originalPosition;

    private Collider2D[] roomColliders;
    private bool[] colliderStates;
    private SpriteRenderer[] visuals;
    private Color[] originalColors;

    private static RoomDrag activeRoom;

    private void Awake()
    {
        roomConnection = GetComponent<RoomConnection>();
        sceneCamera = Camera.main;

        roomColliders = GetComponentsInChildren<Collider2D>();
        colliderStates = new bool[roomColliders.Length];

        visuals = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[visuals.Length];

        for (int i = 0; i < visuals.Length; i++)
            originalColors[i] = visuals[i].color;
    }

    private void Update()
    {
        if (Mouse.current == null || sceneCamera == null ||
            dragArea == null || placementArea == null ||
            playerCollider == null)
            return;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        float depth = transform.position.z - sceneCamera.transform.position.z;

        Vector3 mouseWorld = sceneCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth)
        );
        mouseWorld.z = transform.position.z;

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            activeRoom == null &&
            dragArea.OverlapPoint(mouseWorld))
        {
            BeginDrag(mouseWorld);
        }

        if (activeRoom != this)
            return;

        transform.position = GetPlacementPosition(mouseWorld + dragOffset);
        bool valid = CanPlace();

        ShowPreview(valid);

        if (!Mouse.current.leftButton.isPressed)
            EndDrag(valid);
    }

    private void BeginDrag(Vector3 mouseWorld)
    {
        if (roomConnection != null)
        {
            if (!roomConnection.CanStartDrag())
                return;

            // Close the passage before turning the room into a preview.
            roomConnection.Disconnect();
        }

        activeRoom = this;
        originalPosition = transform.position;
        dragOffset = transform.position - mouseWorld;

        // Disable physical collisions while previewing the placement.
        for (int i = 0; i < roomColliders.Length; i++)
        {
            colliderStates[i] = roomColliders[i].enabled;
            roomColliders[i].enabled = false;
        }
    }

    private bool CanPlace()
    {
        Bounds candidate = placementArea.bounds;

        if (blockedAreas != null)
        {
            foreach (SpriteRenderer area in blockedAreas)
            {
                if (area != null && Overlaps2D(candidate, area.bounds))
                    return false;
            }
        }

        // Leave a small clearance around the player.
        if (playerCollider.enabled &&
            playerCollider.gameObject.activeInHierarchy)
        {
            Bounds playerBounds = playerCollider.bounds;
            playerBounds.Expand(0.04f);

            if (Overlaps2D(candidate, playerBounds))
                return false;
        }

        return true;
    }

    private static bool Overlaps2D(Bounds a, Bounds b)
    {
        return a.min.x < b.max.x && a.max.x > b.min.x &&
               a.min.y < b.max.y && a.max.y > b.min.y;
    }

    private void ShowPreview(bool valid)
    {
        for (int i = 0; i < visuals.Length; i++)
        {
            Color color = valid ? originalColors[i] : Color.red;
            color.a = 0.45f;
            visuals[i].color = color;
        }
    }

    private void EndDrag(bool valid)
    {
        if (!valid)
            transform.position = originalPosition;

        for (int i = 0; i < visuals.Length; i++)
            visuals[i].color = originalColors[i];

        for (int i = 0; i < roomColliders.Length; i++)
            roomColliders[i].enabled = colliderStates[i];

        if (roomConnection != null)
            roomConnection.RefreshConnection();

        Physics2D.SyncTransforms();
        activeRoom = null;
    }

    private Vector3 GetPlacementPosition(Vector3 desiredPosition)
    {
        // Snap the room's center to the connection target when nearby.
        if (snapTarget != null &&
            Vector3.Distance(desiredPosition, snapTarget.position) <= snapDistance)
        {
            return snapTarget.position;
        }

        return desiredPosition;
    }

    private void OnDisable()
    {
        if (activeRoom == this)
            EndDrag(false);
    }
}