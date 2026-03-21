using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tap/click a bubble to destroy all connected bubbles of the same color.
/// Connection is determined via an overlap query around each bubble (good for grid layouts).
/// Attach this to the bubble prefab (same GameObject as the CircleCollider2D + SpriteRenderer).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BubbleColorClusterPop : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Only colliders on these layers are considered bubbles.")]
    [SerializeField] private LayerMask bubbleLayerMask = ~0;

    [Tooltip("Neighbor search radius in world units. For a spaced grid, set close to your spacing (e.g. spacing * 0.55 to 0.75).")]
    [SerializeField] private float neighborRadius = 0.75f;

    [Tooltip("How close colors must be to match. 0 = exact, ~0.02–0.06 works well for minor float drift.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float colorTolerance = 0.02f;

    [Header("Rules")]
    [Tooltip("If true, only pop groups of size >= Min Group Size.")]
    [SerializeField] private bool requireMinGroupSize = false;

    [SerializeField] private int minGroupSize = 2;

    [Header("Optional")]
    [Tooltip("Optional. If not set, uses Camera.main.")]
    [SerializeField] private Camera targetCamera;

    private SpriteRenderer sr;
    private Collider2D col;

    // Shared, reusable buffers to avoid allocations.
    private static readonly Collider2D[] OverlapBuffer = new Collider2D[64];

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        BubbleInputRouter.EnsureExists();
    }

    private void OnEnable() => BubbleInputRouter.Register(this);
    private void OnDisable() => BubbleInputRouter.Unregister(this);

    private Camera GetCamera() => targetCamera != null ? targetCamera : Camera.main;

    private bool MatchesColor(Color a, Color b)
    {
        // Compare in RGBA space; for palette colors this is stable and fast.
        // Using squared distance to avoid sqrt.
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        float da = a.a - b.a;
        float distSq = (dr * dr) + (dg * dg) + (db * db) + (da * da);
        return distSq <= (colorTolerance * colorTolerance);
    }

    private bool ContainsPoint(Vector2 worldPoint)
    {
        // Fast and reliable point-in-collider test for 2D clicks.
        return col != null && col.OverlapPoint(worldPoint);
    }

    private void HandleTap(Vector2 screenPoint)
    {
        var cam = GetCamera();
        if (cam == null) return;

        var worldPoint = (Vector2)cam.ScreenToWorldPoint(screenPoint);
        if (!ContainsPoint(worldPoint)) return;

        PopConnectedSameColor();
    }

    private void PopConnectedSameColor()
    {
        Color targetColor = sr.color;

        // BFS flood fill through neighbor overlap queries.
        var visited = new HashSet<BubbleColorClusterPop>();
        var queue = new Queue<BubbleColorClusterPop>();
        var group = new List<BubbleColorClusterPop>(32);

        visited.Add(this);
        queue.Enqueue(this);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == null || !current.isActiveAndEnabled) continue;

            group.Add(current);

            int hitCount = Physics2D.OverlapCircleNonAlloc(
                current.transform.position,
                neighborRadius,
                OverlapBuffer,
                bubbleLayerMask
            );

            for (int i = 0; i < hitCount; i++)
            {
                var hit = OverlapBuffer[i];
                if (hit == null) continue;

                var other = hit.GetComponentInParent<BubbleColorClusterPop>();
                if (other == null || !other.isActiveAndEnabled) continue;
                if (visited.Contains(other)) continue;

                if (MatchesColor(targetColor, other.sr.color))
                {
                    visited.Add(other);
                    queue.Enqueue(other);
                }
            }
        }

        if (requireMinGroupSize && group.Count < Mathf.Max(1, minGroupSize))
            return;

        // LOL-11: award points for this cluster before destroying bubbles
        if (PoploScoreManager.Instance != null)
            PoploScoreManager.Instance.AddScoreForCluster(group.Count);

        for (int i = 0; i < group.Count; i++)
        {
            if (group[i] != null)
                Destroy(group[i].gameObject);
        }
    }

    private static class BubbleInputRouter
    {
        private static GameObject host;
        private static RouterBehaviour behaviour;

        public static void EnsureExists()
        {
            if (behaviour != null) return;
            host = new GameObject("[BubbleInputRouter]");
            Object.DontDestroyOnLoad(host);
            behaviour = host.AddComponent<RouterBehaviour>();
        }

        public static void Register(BubbleColorClusterPop bubble)
        {
            EnsureExists();
            behaviour.Register(bubble);
        }

        public static void Unregister(BubbleColorClusterPop bubble)
        {
            if (behaviour == null) return;
            behaviour.Unregister(bubble);
        }

        private sealed class RouterBehaviour : MonoBehaviour
        {
            private readonly List<BubbleColorClusterPop> bubbles = new();

            public void Register(BubbleColorClusterPop bubble)
            {
                if (bubble == null) return;
                if (!bubbles.Contains(bubble)) bubbles.Add(bubble);
            }

            public void Unregister(BubbleColorClusterPop bubble)
            {
                if (bubble == null) return;
                bubbles.Remove(bubble);
            }

            private void Update()
            {
#if ENABLE_INPUT_SYSTEM
                if (TryGetPointerDown_InputSystem(out var screenPoint))
                {
                    Dispatch(screenPoint);
                }
#else
                if (TryGetPointerDown_Legacy(out var screenPoint))
                {
                    Dispatch(screenPoint);
                }
#endif
            }

            private void Dispatch(Vector2 screenPoint)
            {
                // Dispatch to all bubbles; each bubble does a very cheap OverlapPoint.
                // (This avoids complex sorting logic and works well for modest bubble counts.)
                for (int i = bubbles.Count - 1; i >= 0; i--)
                {
                    var b = bubbles[i];
                    if (b == null || !b.isActiveAndEnabled)
                    {
                        bubbles.RemoveAt(i);
                        continue;
                    }

                    b.HandleTap(screenPoint);
                }
            }

#if ENABLE_INPUT_SYSTEM
            private static bool TryGetPointerDown_InputSystem(out Vector2 screenPoint)
            {
                var ts = Touchscreen.current;
                if (ts != null)
                {
                    for (int i = 0; i < ts.touches.Count; i++)
                    {
                        var t = ts.touches[i];
                        if (t.press.wasPressedThisFrame)
                        {
                            screenPoint = t.position.ReadValue();
                            return true;
                        }
                    }
                }

                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    screenPoint = mouse.position.ReadValue();
                    return true;
                }

                screenPoint = default;
                return false;
            }
#else
            private static bool TryGetPointerDown_Legacy(out Vector2 screenPoint)
            {
                if (Input.touchCount > 0)
                {
                    var t = Input.GetTouch(0);
                    if (t.phase == TouchPhase.Began)
                    {
                        screenPoint = t.position;
                        return true;
                    }
                }

                if (Input.GetMouseButtonDown(0))
                {
                    screenPoint = Input.mousePosition;
                    return true;
                }

                screenPoint = default;
                return false;
            }
#endif
        }
    }
}

