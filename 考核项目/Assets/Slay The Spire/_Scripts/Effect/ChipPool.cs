using UnityEngine;
using System.Collections.Generic;

public class ChipPool : MonoBehaviour
{
    public static ChipPool Instance;
    public GameObject chipPrefab;
    private Queue<GameObject> pool = new Queue<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    public GameObject GetChip()
    {
        if (pool.Count == 0)
        {
            return Instantiate(chipPrefab, transform);
        }
        var obj = pool.Dequeue();
        obj.SetActive(true);
        return obj;
    }

    public void ReturnChip(GameObject chip)
    {
        chip.SetActive(false);
        pool.Enqueue(chip);
    }
}