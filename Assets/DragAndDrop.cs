using UnityEngine;

public class DragAndDrop : MonoBehaviour
{
    private Vector3 mOffset;
    private float mZCoord;
    private Rigidbody rb;
    private float initialY; // Store the initial Y position

    void Start()
    {
        //rb = GetComponent<Rigidbody>();
        // rb.isKinematic = true; // Initially set to kinematic to avoid unintended physics interactions
    }

    void OnMouseDown()
    {
        // Disable kinematic mode to allow for proper drag and drop
        //rb.isKinematic = true;
        mZCoord = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;
        mOffset = gameObject.transform.position - GetMouseWorldPos();
        initialY = gameObject.transform.position.y; // Store the initial Y position
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = mZCoord;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    void OnMouseDrag()
    {
        Vector3 newPosition = GetMouseWorldPos() + mOffset;
        newPosition.y = 2.1f; // Keep the Y position constant
        transform.position = newPosition;
    }

    void OnMouseUp()
    {
        //rb.isKinematic = false; // Re-enable physics once the object is dropped
    }
}