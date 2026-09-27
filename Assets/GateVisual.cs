using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GateVisual : MonoBehaviour
{
    [SerializeField] private bool vertical;
    [SerializeField, Range(2, 32)] private int dashCount = 10;
    [SerializeField, Range(0.1f, 0.9f)] private float fillRatio = 0.5f;
    [Tooltip("Visible line thickness in world units. Does not change collision bounds.")]
    [SerializeField, Range(0.02f, 0.15f)] private float dashThickness = 0.05f;

    private Sprite dashSprite;
    private bool started;
    private bool previousVertical;
    private int previousCount;
    private float previousFill;
    private float previousThickness;
    private Vector3 previousScale;

    private void Awake()
    {
        SpriteRenderer original = GetComponent<SpriteRenderer>();
        if (original.sprite == null)
            return;

        Sprite source = original.sprite;
        dashSprite = Sprite.Create(
            source.texture, source.rect, new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect);

        // Create the renderer before the board caches room visuals in Start.
        GameObject dashObject = new GameObject("DashVisual");
        dashObject.transform.SetParent(transform, false);
        SpriteRenderer renderer = dashObject.AddComponent<SpriteRenderer>();
        renderer.sprite = dashSprite;
        renderer.sharedMaterial = original.sharedMaterial;
        renderer.color = original.color;
        renderer.sortingLayerID = original.sortingLayerID;
        renderer.sortingOrder = original.sortingOrder;
        original.enabled = false;

        // A gate remains passable even when it has no neighboring room.
        foreach (Collider2D gateCollider in GetComponents<Collider2D>())
            gateCollider.enabled = false;
    }

    private void Start()
    {
        started = true;
        RebuildDashes();
    }

    private void Update()
    {
        // Rebuild the existing sprite so board preview colors stay intact.
        if (started && dashSprite != null &&
            (vertical != previousVertical || dashCount != previousCount ||
             fillRatio != previousFill || dashThickness != previousThickness ||
             transform.lossyScale != previousScale))
        {
            RebuildDashes();
        }
    }

    private void RebuildDashes()
    {
        if (dashSprite == null)
            return;

        int count = Mathf.Clamp(dashCount, 2, 32);
        float fill = Mathf.Clamp(fillRatio, 0.1f, 0.9f);
        Vector2 rectSize = dashSprite.rect.size;
        Vector3 scale = transform.lossyScale;
        float shortPixels = vertical ? rectSize.x : rectSize.y;
        float shortScale = Mathf.Abs(vertical ? scale.x : scale.y);
        float worldThickness = shortPixels / dashSprite.pixelsPerUnit * shortScale;
        float fraction = Mathf.Clamp(
            Mathf.Clamp(dashThickness, 0.02f, 0.15f) /
            Mathf.Max(worldThickness, 0.0001f), 0.001f, 1f);
        float halfWidth = fraction * 0.5f;

        Vector2[] vertices = new Vector2[count * 4];
        ushort[] triangles = new ushort[count * 6];

        for (int i = 0; i < count; i++)
        {
            // Center each dash in its cell, leaving equal gaps at both ends.
            float start = (i + (1f - fill) * 0.5f) / count;
            float end = (i + (1f + fill) * 0.5f) / count;
            int v = i * 4;

            // OverrideGeometry takes coordinates inside the sprite pixel rect.
            if (vertical)
            {
                float left = (0.5f - halfWidth) * rectSize.x;
                float right = (0.5f + halfWidth) * rectSize.x;
                vertices[v] = new Vector2(left, start * rectSize.y);
                vertices[v + 1] = new Vector2(right, start * rectSize.y);
                vertices[v + 2] = new Vector2(left, end * rectSize.y);
                vertices[v + 3] = new Vector2(right, end * rectSize.y);
            }
            else
            {
                float bottom = (0.5f - halfWidth) * rectSize.y;
                float top = (0.5f + halfWidth) * rectSize.y;
                vertices[v] = new Vector2(start * rectSize.x, bottom);
                vertices[v + 1] = new Vector2(end * rectSize.x, bottom);
                vertices[v + 2] = new Vector2(start * rectSize.x, top);
                vertices[v + 3] = new Vector2(end * rectSize.x, top);
            }

            int t = i * 6;
            triangles[t] = (ushort)v;
            triangles[t + 1] = (ushort)(v + 1);
            triangles[t + 2] = (ushort)(v + 2);
            triangles[t + 3] = (ushort)(v + 1);
            triangles[t + 4] = (ushort)(v + 3);
            triangles[t + 5] = (ushort)(v + 2);
        }

        dashSprite.OverrideGeometry(vertices, triangles);
        previousVertical = vertical;
        previousCount = dashCount;
        previousFill = fillRatio;
        previousThickness = dashThickness;
        previousScale = scale;
    }

    private void OnDestroy()
    {
        if (dashSprite != null)
            Destroy(dashSprite);
    }
}
