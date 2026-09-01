using System.Collections;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject customerPrefab;
    public Transform entrancePoint;   // left door
    public Transform exitPoint;       // right door
    public Transform[] shelfPoints;   // pool of browsing spots around the store
    public Transform cashierPoint;    // first cashier desk

    [Header("Spawning")]
    public Vector2 spawnIntervalRange = new Vector2(5f, 12f);
    public int maxActiveCustomers = 6;

    [Tooltip("Driven by ShiftManager — customers only arrive while a shift is running.")]
    public bool spawningEnabled;

    int activeCount;

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnIntervalRange.x, spawnIntervalRange.y));

            if (spawningEnabled && activeCount < maxActiveCustomers)
                SpawnCustomer();
        }
    }

    void SpawnCustomer()
    {
        if (customerPrefab == null || entrancePoint == null)
        {
            Debug.LogWarning("CustomerSpawner is missing customerPrefab or entrancePoint.");
            return;
        }

        GameObject instance = Instantiate(customerPrefab, entrancePoint.position, entrancePoint.rotation);
        CustomerNPC npc = instance.GetComponent<CustomerNPC>();

        if (npc == null)
        {
            Debug.LogWarning("customerPrefab has no CustomerNPC component.");
            Destroy(instance);
            return;
        }

        npc.Init(shelfPoints, cashierPoint, exitPoint, OnCustomerDespawned);
        activeCount++;
    }

    void OnCustomerDespawned()
    {
        activeCount = Mathf.Max(0, activeCount - 1);
    }
}
