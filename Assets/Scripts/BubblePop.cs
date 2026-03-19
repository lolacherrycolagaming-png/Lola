using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BubblePop : MonoBehaviour
{
    [Tooltip("Optional. If not set, Camera.main is used.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("If set, only colliders on these layers can be popped.")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    private Collider2D bubbleCollider;

    private void Awake()
    {
        bubbleCollider = GetComponent<Collider2D>();
        if (bubbleCollider == null)
        {
            Debug.LogError($"{nameof(BubblePop)} requires a 2D Collider on the same GameObject.", this);
            enabled = false;
            return;
        }

        BubblePopInputRouter.EnsureExists();
    }

    private void OnEnable()
    {
        BubblePopInputRouter.Register(this);
    }

    private void OnDisable()
    {
        BubblePopInputRouter.Unregister(this);
    }

    public void Pop()
    {
        Destroy(gameObject);
    }

    private Camera GetCamera()
    {
        if (targetCamera != null) return targetCamera;
        return Camera.main;
    }

    private bool TryHit(Vector2 worldPoint)
    {
        // Using OverlapPoint is generally more reliable than a zero-length Raycast for 2D point clicks.
        var hit = Physics2D.OverlapPoint(worldPoint, clickableLayers);
        if (hit == null) return false;

        // If the collider belongs to this bubble (or one of its children), pop this bubble.
        if (hit == bubbleCollider) return true;
        if (hit.transform.IsChildOf(transform)) return true;

        // If we hit another bubble, let that bubble handle it.
        return false;
    }

    private void TryPopAtScreenPoint(Vector2 screenPoint)
    {
        var cam = GetCamera();
        if (cam == null) return;

        var worldPoint = (Vector2)cam.ScreenToWorldPoint(screenPoint);
        if (TryHit(worldPoint))
        {
            Pop();
        }
    }

    private static class BubblePopInputRouter
    {
        private static GameObject host;
        private static RouterBehaviour behaviour;

        public static void EnsureExists()
        {
            if (behaviour != null) return;

            host = new GameObject("[BubblePopInputRouter]");
            Object.DontDestroyOnLoad(host);
            behaviour = host.AddComponent<RouterBehaviour>();
        }

        public static void Register(BubblePop bubble)
        {
            EnsureExists();
            behaviour.Register(bubble);
        }

        public static void Unregister(BubblePop bubble)
        {
            if (behaviour == null) return;
            behaviour.Unregister(bubble);
        }

        private sealed class RouterBehaviour : MonoBehaviour
        {
            private readonly System.Collections.Generic.List<BubblePop> bubbles = new();
            private readonly Collider2D[] overlapResults = new Collider2D[16];

            public void Register(BubblePop bubble)
            {
                if (bubble == null) return;
                if (!bubbles.Contains(bubble)) bubbles.Add(bubble);
            }

            public void Unregister(BubblePop bubble)
            {
                if (bubble == null) return;
                bubbles.Remove(bubble);
            }

            private void Update()
            {
                // One global input poll + physics query per tap/click.
                // This avoids every bubble doing its own raycast every frame.

#if ENABLE_INPUT_SYSTEM
                if (TryGetPointerDownScreenPoint_InputSystem(out var screenPoint))
                {
                    TryPopAt(screenPoint);
                }
#else
                if (TryGetPointerDownScreenPoint_Legacy(out var screenPoint))
                {
                    TryPopAt(screenPoint);
                }
#endif
            }

            private void TryPopAt(Vector2 screenPoint)
            {
                var cam = Camera.main;
                if (cam == null) return;

                var worldPoint = (Vector2)cam.ScreenToWorldPoint(screenPoint);

                // Find the top-most bubble hit at that point.
                // OverlapPointNonAlloc avoids allocations.
                var count = Physics2D.OverlapPointNonAlloc(worldPoint, overlapResults);
                if (count <= 0) return;

                BubblePop best = null;
                var bestSort = int.MinValue;

                for (int i = 0; i < count; i++)
                {
                    var col = overlapResults[i];
                    if (col == null) continue;

                    var bubble = col.GetComponentInParent<BubblePop>();
                    if (bubble == null || !bubble.isActiveAndEnabled) continue;

                    var sr = bubble.GetComponent<SpriteRenderer>();
                    var sort = sr != null ? (sr.sortingLayerID ^ sr.sortingOrder) : 0;

                    if (best == null || sort > bestSort)
                    {
                        best = bubble;
                        bestSort = sort;
                    }
                }

                best?.TryPopAtScreenPoint(screenPoint);
            }

#if ENABLE_INPUT_SYSTEM
            private static bool TryGetPointerDownScreenPoint_InputSystem(out Vector2 screenPoint)
            {
                // Touch takes priority on mobile. Mouse also works in-editor.
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
            private static bool TryGetPointerDownScreenPoint_Legacy(out Vector2 screenPoint)
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