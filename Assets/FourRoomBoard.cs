using UnityEngine;
using UnityEngine.InputSystem;

public class FourRoomBoard : MonoBehaviour
{
    [SerializeField] private Transform[] rooms = new Transform[4];
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private float snapDistance = 0.8f;

    // Side order: right, up, left, down.
    private readonly string[] wallNames =
    {
        "RightWall", "Ceiling", "LeftWall", "Ground"
    };

    private readonly Vector3[] sideDirections =
    {
        Vector3.right, Vector3.up, Vector3.left, Vector3.down
    };

    private const float RoomSize = 4f;

    private GameObject[,] walls;
    private bool[,] gates;

    private SpriteRenderer[] areas;
    private SpriteRenderer[][] visuals;
    private Color[][] colors;
    private Collider2D[][] colliders;

    private Camera sceneCamera;
    private int dragging = -1;
    private Vector3 originalPosition;
    private Vector3 dragOffset;
    private bool[] colliderStates;
    private bool completed;
    private Rigidbody2D playerBody;
    private Vector2 startPosition;
    [SerializeField, Min(0.1f)] private float restartDelay = 1.5f;
    private bool isRestarting;
    private float restartTimer;

    [SerializeField, Min(1f)] private float dragThresholdPixels = 8f;
    private int pressedRoom = -1;
    private Vector2 pressScreen;
    private Vector3 pressWorld;
    private bool gestureMoved;
    private string feedbackMessage;
    private float feedbackMessageUntil;

    private Vector3[] initialRoomPositions;
    private Quaternion[] initialRoomRotations;
    private float initialPlayerRotation;

    private float HudScale => Mathf.Max(0.1f,
        Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
    private float HudWidth => Screen.width / HudScale;
    private Rect RestartButtonRect => new Rect(16f, 16f, 170f, 42f);
    private Rect TutorialRect => new Rect(HudWidth - 274f, 16f, 258f, 160f);

    private void Start()
    {
        playerBody = playerCollider.attachedRigidbody;
        startPosition = playerBody.position;
        initialPlayerRotation = playerBody.rotation;
        initialRoomPositions = new Vector3[rooms.Length];
        initialRoomRotations = new Quaternion[rooms.Length];
        for (int i = 0; i < rooms.Length; i++)
        {
            initialRoomPositions[i] = rooms[i].position;
            initialRoomRotations[i] = rooms[i].rotation;
        }
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

        walls = new GameObject[rooms.Length, 4];
        gates = new bool[rooms.Length, 4];

        for (int room = 0; room < rooms.Length; room++)
        {
            for (int side = 0; side < 4; side++)
            {
                GameObject wall = rooms[room].Find(wallNames[side]).gameObject;
                walls[room, side] = wall;

                GateVisual gate = wall.GetComponent<GateVisual>();
                gates[room, side] = gate != null && gate.enabled;
            }
        }

        RefreshConnections();
        Physics2D.SyncTransforms();
    }

    private void Update()
    {
        // Pause gameplay briefly so the death is noticeable.
        if (isRestarting)
        {
            restartTimer -= Time.unscaledDeltaTime;

            if (restartTimer <= 0f)
            {
                Vector3 resetPosition = playerBody.transform.position;
                resetPosition.x = startPosition.x;
                resetPosition.y = startPosition.y;

                playerBody.transform.position = resetPosition;
                playerBody.gameObject.SetActive(true);
                playerBody.position = startPosition;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;

                Physics2D.SyncTransforms();
                isRestarting = false;
            }

            return;
        }

        if (!IsPlayerInsideAnyRoom())
        {
            // Cancel an unfinished drag and keep the last placed layout.
            CancelRoomGesture();

            completed = false;
            isRestarting = true;
            restartTimer = restartDelay;

            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.gameObject.SetActive(false);
            return;
        }

        // Complete the level when the entire player enters Room D.
        Bounds goal = areas[3].bounds;
        Bounds player = playerCollider.bounds;

        if (player.min.x > goal.min.x && player.max.x < goal.max.x &&
            player.min.y > goal.min.y && player.max.y < goal.max.y)
            completed = true;

        HandleRoomMouse();
    }

    private void HandleRoomMouse()
    {
        if (Mouse.current == null)
        {
            CancelRoomGesture();
            return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector2 guiMouse = new Vector2(screen.x, Screen.height - screen.y) / HudScale;
        if (pressedRoom < 0 && dragging < 0 &&
            (RestartButtonRect.Contains(guiMouse) || TutorialRect.Contains(guiMouse)))
            return;

        Vector3 mouse = sceneCamera.ScreenToWorldPoint(
            new Vector3(screen.x, screen.y, -sceneCamera.transform.position.z)
        );
        mouse.z = 0f;

        // Right-click rotates clockwise without starting a drag.
        // Ignore it while a left-button gesture is in progress.
        if (pressedRoom < 0 && dragging < 0 &&
            !Mouse.current.leftButton.isPressed &&
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            for (int i = 1; i <= 2; i++)
            {
                Bounds area = areas[i].bounds;
                if (mouse.x >= area.min.x && mouse.x <= area.max.x &&
                    mouse.y >= area.min.y && mouse.y <= area.max.y)
                {
                    RotateRoom(i, -1);
                    break;
                }
            }

            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            CancelRoomGesture();

            // Capture the room on press; do not rotate or drag yet.
            for (int i = 1; i <= 2; i++)
            {
                Bounds area = areas[i].bounds;
                if (mouse.x >= area.min.x && mouse.x <= area.max.x &&
                    mouse.y >= area.min.y && mouse.y <= area.max.y)
                {
                    pressedRoom = i;
                    pressScreen = screen;
                    pressWorld = mouse;
                    break;
                }
            }
        }

        if (pressedRoom < 0)
            return;

        float threshold = Mathf.Max(1f, dragThresholdPixels);
        if (!gestureMoved && (screen - pressScreen).sqrMagnitude >= threshold * threshold)
        {
            // A blocked drag must never become a rotation on release.
            gestureMoved = true;
            if (CanDrag(pressedRoom))
                BeginDrag(pressedRoom, pressWorld);
            else
                ShowFeedback("Leave this room before dragging it.");
        }

        bool valid = false;
        if (dragging >= 0)
        {
            Vector3 position = SnapPosition(dragging, mouse + dragOffset);
            rooms[dragging].position = position;
            valid = CanPlace(dragging, position);
            SetAppearance(dragging, true, valid);
        }

        if (!Mouse.current.leftButton.isPressed)
        {
            if (dragging >= 0)
                EndDrag(valid);
            else if (!gestureMoved)
                RotateRoom(pressedRoom, 1);

            pressedRoom = -1;
            gestureMoved = false;
        }
    }

    private void CancelRoomGesture()
    {
        if (dragging >= 0)
            EndDrag(false);

        pressedRoom = -1;
        gestureMoved = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelRoomGesture();
    }

    private void RotateRoom(int index, int direction)
    {
        if (index < 1 || index > 2 || dragging >= 0)
            return;

        Physics2D.SyncTransforms();
        bool carryPlayer = Overlaps(playerCollider.bounds, areas[index].bounds);

        // Do not rotate either room while the player straddles a passage.
        if (carryPlayer && !ContainsPlayer(index))
        {
            ShowFeedback("Move fully into one room to rotate.");
            return;
        }

        Quaternion previousRoomRotation = rooms[index].rotation;
        Vector2 previousPlayerPosition = playerBody.position;
        float previousPlayerRotation = playerBody.rotation;
        Vector2 previousVelocity = playerBody.linearVelocity;
        float previousAngularVelocity = playerBody.angularVelocity;

        float angle = Mathf.Round(rooms[index].eulerAngles.z / 90f) * 90f;
        rooms[index].rotation = Quaternion.Euler(0f, 0f, angle + direction * 90f);

        if (carryPlayer)
        {
            // Carry the player in the selected direction while keeping the body upright.
            Vector2 center = rooms[index].position;
            Vector2 offset = previousPlayerPosition - center;
            Vector2 rotated = center + new Vector2(-direction * offset.y, direction * offset.x);
            SetPlayerPose(rotated, 0f);
        }

        RefreshConnections();
        Physics2D.SyncTransforms();

        if (carryPlayer && !ResolveRotationOverlap(index))
        {
            // Restore the complete previous state if no nearby safe pose exists.
            rooms[index].rotation = previousRoomRotation;
            SetPlayerPose(previousPlayerPosition, previousPlayerRotation);
            playerBody.linearVelocity = previousVelocity;
            playerBody.angularVelocity = previousAngularVelocity;
            RefreshConnections();
            Physics2D.SyncTransforms();
            ShowFeedback("Not enough space to rotate here.");
            return;
        }

        if (carryPlayer)
        {
            // Let world gravity start a fresh downward fall after the turn.
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.WakeUp();
        }

        feedbackMessageUntil = 0f;
    }

    private void SetPlayerPose(Vector2 position, float angle)
    {
        Vector3 worldPosition = playerBody.transform.position;
        worldPosition.x = position.x;
        worldPosition.y = position.y;
        playerBody.transform.SetPositionAndRotation(
            worldPosition, Quaternion.Euler(0f, 0f, angle));
        playerBody.position = position;
        playerBody.rotation = angle;
        Physics2D.SyncTransforms();
    }

    private bool ContainsPlayer(int room)
    {
        const float tolerance = 0.001f;
        Bounds area = areas[room].bounds;
        Bounds player = playerCollider.bounds;
        return player.min.x >= area.min.x - tolerance &&
               player.max.x <= area.max.x + tolerance &&
               player.min.y >= area.min.y - tolerance &&
               player.max.y <= area.max.y + tolerance;
    }

    private bool ResolveRotationOverlap(int room)
    {
        const float clearance = 0.002f;
        Vector2 rotatedPosition = playerBody.position;

        // Upright rectangular players may need a small correction after a turn.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Bounds area = areas[room].bounds;
            Bounds player = playerCollider.bounds;
            Vector2 desiredCenter = player.center;
            desiredCenter.x = Mathf.Clamp(desiredCenter.x,
                area.min.x + player.extents.x + clearance,
                area.max.x - player.extents.x - clearance);
            desiredCenter.y = Mathf.Clamp(desiredCenter.y,
                area.min.y + player.extents.y + clearance,
                area.max.y - player.extents.y - clearance);

            Vector2 adjustment = desiredCenter - (Vector2)player.center;
            if (adjustment.sqrMagnitude > 0.00000001f)
                SetPlayerPose(playerBody.position + adjustment, 0f);

            bool overlapping = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                foreach (Collider2D solid in colliders[i])
                {
                    if (!solid.enabled || !solid.gameObject.activeInHierarchy ||
                        solid.isTrigger || solid == playerCollider)
                        continue;

                    ColliderDistance2D separation = playerCollider.Distance(solid);
                    if (!separation.isValid)
                        return false;
                    if (!separation.isOverlapped)
                        continue;

                    overlapping = true;
                    Vector2 correction =
                        separation.normal * (separation.distance - clearance);
                    SetPlayerPose(playerBody.position + correction, 0f);
                }
            }

            // Do not resolve a blocked rotation by teleporting across a room.
            if (Vector2.Distance(playerBody.position, rotatedPosition) > 0.35f)
                return false;

            if (!overlapping && ContainsPlayer(room))
                return true;
        }

        return false;
    }

    private void ShowFeedback(string message)
    {
        feedbackMessage = message;
        feedbackMessageUntil = Time.unscaledTime + 1.5f;
    }

    private bool IsPlayerInsideAnyRoom()
    {
        Vector3 playerCenter = playerCollider.bounds.center;

        for (int i = 0; i < rooms.Length; i++)
        {
            // A dragged preview does not count as a playable room.
            if (i == dragging)
                continue;

            SpriteRenderer area = areas[i];

            if (!area.gameObject.activeInHierarchy)
                continue;

            Vector3 localPoint =
                area.transform.InverseTransformPoint(playerCenter);
            Bounds localBounds = area.sprite.bounds;

            if (localPoint.x >= localBounds.min.x &&
                localPoint.x <= localBounds.max.x &&
                localPoint.y >= localBounds.min.y &&
                localPoint.y <= localBounds.max.y)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GateDirection(int room, int side)
    {
        // Use exact cardinal directions to avoid rotation rounding drift.
        int turns = Mathf.RoundToInt(rooms[room].eulerAngles.z / 90f);
        int worldSide = ((side + turns) % 4 + 4) % 4;
        return sideDirections[worldSide];
    }

    private bool HasFacingGate(int room, Vector3 direction)
    {
        for (int side = 0; side < 4; side++)
        {
            if (gates[room, side] &&
                Vector3.Dot(GateDirection(room, side), direction) > 0.99f)
                return true;
        }

        return false;
    }

    private bool IsConnected(int first, int side, int second)
    {
        if (first == second || first == dragging || second == dragging)
            return false;

        if (!gates[first, side])
            return false;

        Vector3 direction = GateDirection(first, side);
        if (!HasFacingGate(second, -direction))
            return false;

        Vector3 expected = rooms[first].position + direction * RoomSize;
        return Vector3.Distance(rooms[second].position, expected) < 0.01f;
    }

    private void RefreshConnections()
    {
        for (int room = 0; room < rooms.Length; room++)
        {
            for (int side = 0; side < 4; side++)
            {
                if (!gates[room, side])
                    continue;

                bool connected = false;

                for (int other = 0; other < rooms.Length; other++)
                {
                    if (IsConnected(room, side, other))
                    {
                        connected = true;
                        break;
                    }
                }

                // Hide connected gates and show exposed gates.
                walls[room, side].SetActive(!connected);
            }
        }
    }

    private bool CanDrag(int index)
    {
        // Lock the room only while the player's body overlaps its area.
        return !Overlaps(playerCollider.bounds, areas[index].bounds);
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

        for (int other = 0; other < rooms.Length; other++)
        {
            if (other == index)
                continue;

            for (int side = 0; side < 4; side++)
            {
                if (!gates[index, side])
                    continue;

                Vector3 direction = GateDirection(index, side);
                if (!HasFacingGate(other, -direction))
                    continue;

                // Align gates using their current world-space directions.
                Vector3 target = rooms[other].position - direction * RoomSize;

                float distance = Vector3.Distance(desired, target);

                if (distance <= nearest && CanPlace(index, target))
                {
                    nearest = distance;
                    result = target;
                }
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
        const float tolerance = 0.001f;
        return a.min.x < b.max.x - tolerance &&
               a.max.x > b.min.x + tolerance &&
               a.min.y < b.max.y - tolerance &&
               a.max.y > b.min.y + tolerance;
    }

    private void OnDisable()
    {
        CancelRoomGesture();
    }

    private void RestartLevel()
    {
        if (initialRoomPositions == null)
            return;

        // Cancel any preview before restoring the saved starting layout.
        CancelRoomGesture();
        isRestarting = false;
        restartTimer = 0f;
        completed = false;
        feedbackMessage = null;
        feedbackMessageUntil = 0f;

        // Deactivation clears pending movement and jump input.
        playerBody.gameObject.SetActive(false);

        for (int i = 0; i < rooms.Length; i++)
        {
            rooms[i].SetPositionAndRotation(initialRoomPositions[i], initialRoomRotations[i]);
            SetAppearance(i, false, true);
        }

        RefreshConnections();
        playerBody.gameObject.SetActive(true);
        SetPlayerPose(startPosition, initialPlayerRotation);
        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.WakeUp();
        Physics2D.SyncTransforms();
    }

    private void OnGUI()
    {
        // Use the same scaled coordinates for drawing and mouse hit testing.
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(HudScale, HudScale, 1f));
        try
        {
            DrawHud();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawTutorial()
    {
        Rect area = TutorialRect;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.alignment = TextAnchor.UpperLeft;
        style.wordWrap = false;
        style.padding = new RectOffset(0, 0, 0, 0);
        style.normal.textColor = new Color(1f, 0.87f, 0.2f, 1f);

        string[] lines =
        {
            "A / D: Move",
            "Space: Jump",
            "Left Drag: Move Room",
            "Left Click: Rotate Left",
            "Right Click: Rotate Right"
        };

        // Draw only the controls, with no title or background panel.
        for (int i = 0; i < lines.Length; i++)
        {
            GUI.Label(new Rect(area.x, area.y + i * 32f,
                area.width, 32f), lines[i], style);
        }
    }

    private void DrawHud()
    {
        DrawTutorial();
        GUIStyle restartStyle = new GUIStyle(GUI.skin.button);
        restartStyle.fontSize = 20;
        if (GUI.Button(RestartButtonRect, "Restart Level", restartStyle))
            RestartLevel();

        bool showFeedbackMessage = Time.unscaledTime < feedbackMessageUntil;
        if (!completed && !isRestarting && !showFeedbackMessage)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = !completed && !isRestarting ? 22 : 28;

        string message = isRestarting
            ? "You Died — Restarting..."
            : completed ? "Level Complete" : feedbackMessage;

        GUI.Box(
            new Rect(HudWidth * 0.5f - 270f, 20f, 540f, 60f),
            message,
            style
        );
    }
}