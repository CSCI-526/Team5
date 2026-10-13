
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableCommand : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RouteCommand command;
    [SerializeField] private ForkCommandSlot targetSlot;

    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector3 originalPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("Begin dragging");
        originalPosition = rectTransform.position;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("Dragging...");
        Camera uiCamera = canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
            ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            (RectTransform)canvas.transform,
            eventData.position,
            uiCamera,
            out Vector3 position
        );

        rectTransform.position = position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        rectTransform.position = originalPosition;

        if (targetSlot == null || Camera.main == null)
            return;

        Collider2D collider =
            targetSlot.GetComponent<Collider2D>();

        if (collider == null)
            return;

        Camera cam = Camera.main;

        Vector3 screenPoint = new Vector3(
            eventData.position.x,
            eventData.position.y,
            Mathf.Abs(cam.transform.position.z -
                      targetSlot.transform.position.z)
        );

        Vector3 worldPoint = cam.ScreenToWorldPoint(screenPoint);

        if (collider.OverlapPoint(worldPoint))
        {
            targetSlot.SetCommand(command);
            Debug.Log("Command placed: " + command);
        }
    }
}
