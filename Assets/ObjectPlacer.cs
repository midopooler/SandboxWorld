using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ObjectPlacer : MonoBehaviour/*, IPointerDownHandler, IPointerUpHandler*/
{
    public Camera mainCamera;
    public GameObject[] modelPrefabs;
    [SerializeField] GameObject parentEnvironment;

    private GameObject currentObject;

    private const float YPosition = 2.1f;

    private readonly List<Vector3> spawnPoints = new List<Vector3>
    {
        //new Vector3(55f, YPosition, 30f),
        //new Vector3(55f, YPosition, 7f),
        //new Vector3(19.75f, YPosition, -4.3f),
        //new Vector3(16.9f, YPosition, -29.9f),
        //new Vector3(19.2f, YPosition, -23.1f)
        new Vector3(0f, 2.1f, -68f)
    };

    private int currentSpawnIndex = 0;


    public void InstantiateModel(int modelIndex)
    {
        if (modelIndex < 0 || modelIndex >= modelPrefabs.Length)
        {
            Debug.LogError("Invalid model index");
            return;
        }

        if (currentObject != null)
        {
            //Destroy(currentObject);
        }

        Vector3 spawnPosition = GetNextSpawnPoint();
        currentObject = Instantiate(modelPrefabs[modelIndex], spawnPosition, modelPrefabs[modelIndex].transform.rotation);
        currentObject.transform.parent = parentEnvironment.transform;
        //currentObject = Instantiate(modelPrefabs[modelIndex], spawnPosition, Quaternion.identity);
        Debug.Log("object instantiated");

    }

    private Vector3 GetNextSpawnPoint()
    {
        Vector3 spawnPoint = spawnPoints[currentSpawnIndex];
        currentSpawnIndex = (currentSpawnIndex + 1) % spawnPoints.Count;
        return spawnPoint;
    }
}