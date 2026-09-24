using UnityEngine;
using UnityEngine.InputSystem;

public class FourRoomBoard : MonoBehaviour
{
    [SerializeField] private Transform[] rooms = new Transform[4];
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private float snapDistance = 0.8f;

    private readonly int[] from = { 0, 1, 2 };
    private readonly int[] to = { 1, 2, 3 };
    private readonly Vector3[] offsets =
    {
        new Vector3(4f, 0f, 0f),
        new Vector3(0f, 4f, 0f),
        new Vector3(4f, 0f, 0f)
    };

    private SpriteRenderer[] areas;
    private SpriteRenderer[][] visuals;
    private Color[][] colors;
    private Collider2D[][] colliders;
    private GameObject[,] doors;

    private Camera sceneCamera;
    private int dragging = -1;
    private Vector3 originalPosition;
    private Vector3 dragOffset;
    private bool[] colliderStates;
    private bool completed;

    private void Start()
    {
        sceneCamera = Camera.main;
        areas = new SpriteRenderer[4];
        visuals = new SpriteRenderer[4][];
        colors = new Color[4][];
        colliders = new Collider2D[4][];

        for (int i = 0; i < 4; i++)
        {
            areas[i] = rooms[i].Find("Background")
                .GetComponent<SpriteRenderer>();

            visuals[i] = rooms[i].GetComponentsInChildren<SpriteRenderer>(true);
            colliders[i] = rooms[i].GetComponentsInChildren<Collider2D>(true);
            colors[i] = new Color[visuals[i].Length];

            for (int j = 0; j < visuals[i].Length; j++)
                colors[i][j] = visuals[i][j].color;
        }

        doors = new GameObject[3, 2];
        doors[0, 0] = rooms[0].Find("RightWall").gameObject;
        doors[0, 1] = rooms[1].Find("LeftWall").gameObject;
        doors[1, 0] = rooms[1].Find("Ceiling").gameObject;
        doors[1, 1] = rooms[2].Find("Ground").gameObject;
        doors[2, 0] = rooms[2].Find("RightWall").gameObject;
        doors[2, 1] = rooms[3].Find("LeftWall").gameObject;

        RefreshConnections();
        Physics2D.SyncTransforms();
    }

    private void Update()
    {
        // Complete the level when the entire player enters Room D.
        Bounds goal = areas[3].bounds;
        Bounds player = playerCollider.bounds;

        if (player.min.x > goal.min.x && player.max.x < goal.max.x &&
            player.min.y > goal.min.y && player.max.y < goal.max.y)
            completed = true;

        if (Mouse.current == null)
            return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 mouse = sceneCamera.ScreenToWorldPoint(
            new Vector3(screen.x, screen.y, -sceneCamera.transform.position.z)
        );
        mouse.z = 0f;

        if (dragging < 0 && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Only the two middle rooms can move.
            for (int i = 1; i <= 2; i++)
            {
                Bounds area = areas[i].bounds;

                if (mouse.x >= area.min.x && mouse.x <= area.max.x &&
                    mouse.y >= area.min.y && mouse.y <= area.max.y &&
                    CanDrag(i))
                {
                    BeginDrag(i, mouse);
                    break;
                }
            }
        }

        if (dragging < 0)
            return;

        Vector3 position = SnapPosition(dragging, mouse + dragOffset);
        rooms[dragging].position = position;

        bool valid = CanPlace(dragging, position);
        SetAppearance(dragging, true, valid);

        if (!Mouse.current.leftButton.isPressed)
            EndDrag(valid);
    }

    private bool IsConnected(int link)
    {
        if (dragging == from[link] || dragging == to[link])
            return false;

        Vector3 expected = rooms[from[link]].position + offsets[link];
        return Vector3.Distance(rooms[to[link]].position, expected) < 0.01f;
    }

    private void RefreshConnections()
    {
        for (int link = 0; link < 3; link++)
        {
            bool closed = !IsConnected(link);
            doors[link, 0].SetActive(closed);
            doors[link, 1].SetActive(closed);
        }
    }

    private bool CanDrag(int index)
    {
        Bounds player = playerCollider.bounds;
        player.Expand(0.04f);

        if (Overlaps(player, areas[index].bounds))
            return false;

        for (int link = 0; link < 3; link++)
        {
            if (from[link] != index && to[link] != index)
                continue;

            if (!IsConnected(link))
                continue;

            // Protect both sides of an occupied passage.
            for (int side = 0; side < 2; side++)
            {
                if (Overlaps(player, WallBounds(doors[link, side])))
                    return false;
            }
        }

        return true;
    }

    private Bounds WallBounds(GameObject wall)
    {
        // Compute bounds even when the wall is inactive.
        SpriteRenderer sprite = wall.GetComponent<SpriteRenderer>();
        Bounds local = sprite.sprite.bounds;

        return new Bounds(
            wall.transform.TransformPoint(local.center),
            Vector3.Scale(local.size, wall.transform.lossyScale)
        );
    }

    private void BeginDrag(int index, Vector3 mouse)
    {
        dragging = index;
        originalPosition = rooms[index].position;
        dragOffset = originalPosition - mouse;

        RefreshConnections();

        colliderStates = new bool[colliders[index].Length];

        for (int i = 0; i < colliders[index].Length; i++)
        {
            colliderStates[i] = colliders[index][i].enabled;
            colliders[index][i].enabled = false;
        }

        Physics2D.SyncTransforms();
    }

    private Vector3 SnapPosition(int index, Vector3 desired)
    {
        Vector3 result = desired;
        float nearest = snapDistance;

        for (int link = 0; link < 3; link++)
        {
            Vector3 target;

            if (index == to[link])
                target = rooms[from[link]].position + offsets[link];
            else if (index == from[link])
                target = rooms[to[link]].position - offsets[link];
            else
                continue;

            float distance = Vector3.Distance(desired, target);

            if (distance <= nearest && CanPlace(index, target))
            {
                nearest = distance;
                result = target;
            }
        }

        return result;
    }

    private bool CanPlace(int index, Vector3 position)
    {
        Bounds candidate = areas[index].bounds;
        candidate.center += position - rooms[index].position;

        for (int i = 0; i < 4; i++)
        {
            if (i != index && Overlaps(candidate, areas[i].bounds))
                return false;
        }

        Bounds player = playerCollider.bounds;
        player.Expand(0.04f);

        if (Overlaps(candidate, player))
            return false;

        // Keep the entire room within the current orthographic view.
        float halfHeight = sceneCamera.orthographicSize;
        float halfWidth = halfHeight * sceneCamera.aspect;
        Vector3 center = sceneCamera.transform.position;

        return candidate.min.x >= center.x - halfWidth &&
               candidate.max.x <= center.x + halfWidth &&
               candidate.min.y >= center.y - halfHeight &&
               candidate.max.y <= center.y + halfHeight;
    }

    private void SetAppearance(int index, bool preview, bool valid)
    {
        for (int i = 0; i < visuals[index].Length; i++)
        {
            Color color = preview && !valid ? Color.red : colors[index][i];

            if (preview)
                color.a = 0.45f;

            visuals[index][i].color = color;
        }
    }

    private void EndDrag(bool valid)
    {
        int index = dragging;

        if (!valid)
            rooms[index].position = originalPosition;

        for (int i = 0; i < colliders[index].Length; i++)
            colliders[index][i].enabled = colliderStates[i];

        SetAppearance(index, false, true);
        dragging = -1;

        RefreshConnections();
        Physics2D.SyncTransforms();
    }

    private static bool Overlaps(Bounds a, Bounds b)
    {
        return a.min.x < b.max.x && a.max.x > b.min.x &&
               a.min.y < b.max.y && a.max.y > b.min.y;
    }

    private void OnDisable()
    {
        if (dragging >= 0)
            EndDrag(false);
    }

    private void OnGUI()
    {
        if (!completed)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 28;

        GUI.Box(
            new Rect(Screen.width * 0.5f - 150f, 20f, 300f, 55f),
            "Level Complete",
            style
        );
    }
}