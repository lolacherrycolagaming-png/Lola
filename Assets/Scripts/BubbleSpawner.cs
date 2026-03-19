using UnityEngine;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Bubble Settings")]
    public GameObject bubblePrefab;
    public int columns = 6;
    public int rows = 8;
    public float spacing = 1.1f;

    [Header("Poplo Color Palette")]
    public Color[] bubbleColors = new Color[]
    {
        new Color(1.0f, 0.4f, 0.4f),   // Soft Red
        new Color(0.4f, 0.7f, 1.0f),   // Sky Blue
        new Color(0.5f, 0.9f, 0.5f),   // Mint Green
        new Color(1.0f, 0.8f, 0.3f),   // Warm Yellow
        new Color(0.8f, 0.5f, 1.0f),   // Soft Purple
    };

    void Start()
    {
        SpawnBubbles();
    }

    void SpawnBubbles()
    {
        // Calculate where to start so grid is centered on screen
        float startX = -(columns - 1) * spacing / 2f;
        float startY = -(rows - 1) * spacing / 2f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                // Calculate position for this bubble
                Vector3 position = new Vector3(
                    startX + col * spacing,
                    startY + row * spacing,
                    0f
                );

                // Spawn the bubble
                GameObject bubble = Instantiate(bubblePrefab, position, Quaternion.identity);

                // Assign a random color
                Color randomColor = bubbleColors[Random.Range(0, bubbleColors.Length)];
                bubble.GetComponent<SpriteRenderer>().color = randomColor;
            }
        }
    }
}