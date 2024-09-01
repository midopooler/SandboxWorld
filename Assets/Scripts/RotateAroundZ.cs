using System.Collections;
using UnityEngine;

public class RotateAroundZ : MonoBehaviour
{
    public float rotationTime = 5.0f; // Time to complete one full revolution

    public void RotateEnvironment()
    {
        // Start the rotation coroutine
        StartCoroutine(RotateZCoroutine());
    }

    private IEnumerator RotateZCoroutine()
    {
        float elapsedTime = 0f;
        float initialRotation = transform.rotation.eulerAngles.z;

        while (elapsedTime < rotationTime)
        {
            // Calculate the current rotation based on elapsed time
            float currentRotation = Mathf.Lerp(initialRotation, initialRotation - 360f, elapsedTime / rotationTime);
            transform.rotation = Quaternion.Euler(0, 0, currentRotation);

            elapsedTime += Time.deltaTime;
            yield return null; // Wait until the next frame
        }

        // Ensure the rotation completes exactly one full revolution
        transform.rotation = Quaternion.Euler(0, 0, initialRotation + 360f);
    }
}
