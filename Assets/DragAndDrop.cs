using UnityEngine;

public class DragAndDrop : MonoBehaviour
{
    private Vector3 mOffset;
    private float mZCoord;


    void OnMouseDown()
    {
        mZCoord = Camera.main.WorldToScreenPoint(gameObject.transform.position).z;
        mOffset = gameObject.transform.position - GetMouseWorldPos();
        gameObject.GetComponent<Outline>().enabled = true;
        gameObject.GetComponent<Outline>().OutlineColor = Color.cyan;
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
        gameObject.GetComponent<Outline>().OutlineColor = Color.magenta;
    }

    void OnMouseUp()
    {
        gameObject.GetComponent<Outline>().enabled = false;
    }
}