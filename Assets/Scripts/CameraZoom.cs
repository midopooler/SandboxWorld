using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    public float zoomSpeed = 0.5f;
    public float minZoom = 2.0f;
    public float maxZoom = 10.0f;

    void OnGUI()
    {
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel)
        {
            float scrollData = e.delta.y;

            // Get the mouse position in world coordinates before the zoom
            Vector3 mouseWorldPosBeforeZoom = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Adjust the orthographic size of the camera
            Camera.main.orthographicSize += scrollData * zoomSpeed;
            Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);

            // Get the mouse position in world coordinates after the zoom
            Vector3 mouseWorldPosAfterZoom = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Calculate the difference and adjust the camera position to maintain focus on the mouse position
            Vector3 difference = mouseWorldPosBeforeZoom - mouseWorldPosAfterZoom;
            Camera.main.transform.position += difference;
        }
    }
}
