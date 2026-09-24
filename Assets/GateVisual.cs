using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GateVisual : MonoBehaviour
{
    [SerializeField] private bool vertical;
    [SerializeField, Range(1, 12)] private int waveCount = 6;
    [SerializeField, Range(0.05f, 0.35f)] private float waveHeight = 0.25f;
    [SerializeField, Range(0.05f, 0.25f)] private float lineThickness = 0.15f;

    private Sprite waveSprite;
    private Vector2[] vertices;
    private ushort[] triangles;

    private void Awake()
    {
        SpriteRenderer original = GetComponent<SpriteRenderer>();

        if (original.sprite == null)
            return;

        Sprite source = original.sprite;

        // Create a separate sprite so the original wall asset stays unchanged.
        waveSprite = Sprite.Create(
            source.texture,
            source.rect,
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect
        );

        int count = Mathf.Clamp(waveCount, 1, 12);
        int segments = count * 24;
        Vector2 size = waveSprite.bounds.size;

        this.vertices = new Vector2[(segments + 1) * 2];
        this.triangles = new ushort[segments * 6];

        // Build a narrow ribbon along a smooth sine wave.
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float wave = Mathf.Sin(t * count * Mathf.PI * 2f);
            float height = wave * Mathf.Clamp(waveHeight, 0.05f, 0.35f);
            float halfWidth = Mathf.Clamp(lineThickness, 0.05f, 0.25f) * 0.5f;

            if (vertical)
            {
                float y = (t - 0.5f) * size.y;

                vertices[i * 2] =
                    new Vector2((height - halfWidth) * size.x, y);
                vertices[i * 2 + 1] =
                    new Vector2((height + halfWidth) * size.x, y);
            }
            else
            {
                float x = (t - 0.5f) * size.x;

                vertices[i * 2] =
                    new Vector2(x, (height - halfWidth) * size.y);
                vertices[i * 2 + 1] =
                    new Vector2(x, (height + halfWidth) * size.y);
            }

            if (i == segments)
                continue;

            int index = i * 6;
            int vertex = i * 2;

            triangles[index] = (ushort)vertex;
            triangles[index + 1] = (ushort)(vertex + 1);
            triangles[index + 2] = (ushort)(vertex + 2);
            triangles[index + 3] = (ushort)(vertex + 1);
            triangles[index + 4] = (ushort)(vertex + 3);
            triangles[index + 5] = (ushort)(vertex + 2);
        }

        GameObject waveObject = new GameObject("WaveVisual");
        waveObject.transform.SetParent(transform, false);

        SpriteRenderer waveRenderer =
            waveObject.AddComponent<SpriteRenderer>();

        waveRenderer.sprite = waveSprite;
        waveRenderer.sharedMaterial = original.sharedMaterial;
        waveRenderer.color = original.color;
        waveRenderer.sortingLayerID = original.sortingLayerID;
        waveRenderer.sortingOrder = original.sortingOrder;

        original.enabled = false;

        // Gates remain passable without a connected neighboring room.
        foreach (Collider2D gateCollider in GetComponents<Collider2D>())
        {
            gateCollider.enabled = false;
        }
    }

    private void Start()
    {
        if (waveSprite == null)
            return;

        Vector2[] pixelVertices = new Vector2[vertices.Length];
        Vector2 pivot = waveSprite.pivot;
        Vector2 rectSize = waveSprite.rect.size;
        float pixelsPerUnit = waveSprite.pixelsPerUnit;

        // Convert centered local coordinates to sprite-rect pixel coordinates.
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 pixel = vertices[i] * pixelsPerUnit + pivot;

            pixel.x = Mathf.Clamp(pixel.x, 0f, rectSize.x);
            pixel.y = Mathf.Clamp(pixel.y, 0f, rectSize.y);

            pixelVertices[i] = pixel;
        }

        waveSprite.OverrideGeometry(pixelVertices, triangles);
    }

    private void OnDestroy()
    {
        // Release the sprite created for this runtime instance.
        if (waveSprite != null)
            Destroy(waveSprite);
    }
}