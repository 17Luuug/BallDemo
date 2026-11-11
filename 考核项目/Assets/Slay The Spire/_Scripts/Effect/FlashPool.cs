using UnityEngine;
using System.Collections.Generic;

public class FlashPool : MonoBehaviour
{
    public static FlashPool Instance;
    public GameObject flashPrefab;
    private Queue<GameObject> pool = new Queue<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    public GameObject GetFlash()
    {
        if (pool.Count == 0)
        {
            return Instantiate(flashPrefab, transform);
        }
        var obj = pool.Dequeue();
        obj.SetActive(true);
        return obj;
    }

    public void ReturnFlash(GameObject flash)
    {
        flash.SetActive(false);
        pool.Enqueue(flash);
    }
}