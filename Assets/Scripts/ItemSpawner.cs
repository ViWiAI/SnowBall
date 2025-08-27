using UnityEngine;
using System.Collections.Generic;
using Game.Network;

public class ItemSpawner : MonoBehaviour
{
    public static ItemSpawner Instance { get; private set; }

    [Header("生成设置")]
    public List<GameObject> itemPrefabs; // 道具预制体列表（宝石、药水、雪球等）
    public Vector2 mapSize = new Vector2(1000f, 1000f); // 地图尺寸（1到1000）
    public int initialPoolSize = 3500; // 每种道具的初始池大小

    private Dictionary<GameObject, Queue<GameObject>> itemPools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<int, GameObject> activeItems = new Dictionary<int, GameObject>(); // 跟踪激活道具，键为spawn_id
    private Dictionary<int, Vector3> itemPositions = new Dictionary<int, Vector3>(); // spawn_id到位置的映射

    private void Awake()
    {
        // Singleton 实现
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Debug.Log($"ItemSpawner 位置: {transform.position}, 缩放: {transform.localScale}");
        Debug.Log($"地图尺寸 (Inspector): X={mapSize.x}, Z={mapSize.y}");
        InitializePools();
        // 移除本地生成逻辑，等待服务器的MSG_ITEM_SPAWNED消息
    }

    // 初始化对象池
    private void InitializePools()
    {
        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogError("itemPrefabs 列表为空，请在 Inspector 中分配道具预制体！");
            return;
        }

        foreach (var prefab in itemPrefabs)
        {
            Queue<GameObject> pool = new Queue<GameObject>();
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject item = Instantiate(prefab);
                item.SetActive(false);
                pool.Enqueue(item);
            }
            itemPools.Add(prefab, pool);
          //  Debug.Log($"为 {prefab.name} 创建对象池，初始大小: {initialPoolSize}");
        }
    }

    // 从对象池获取道具
    private GameObject GetPooledItem(GameObject prefab)
    {
        if (itemPools.ContainsKey(prefab) && itemPools[prefab].Count > 0)
        {
            GameObject item = itemPools[prefab].Dequeue();
            item.SetActive(true);
            return item;
        }
        else
        {
            Debug.LogWarning($"对象池 {prefab.name} 已空，动态实例化新道具");
            return Instantiate(prefab);
        }
    }

    // 归还道具到对象池
    public void ReturnItemToPool(GameObject item)
    {
        if (item == null) return;

        item.SetActive(false);
        foreach (var prefab in itemPrefabs)
        {
            if (item.name.Contains(prefab.name))
            {
                itemPools[prefab].Enqueue(item);
                int spawnId = GetSpawnId(item);
                if (activeItems.ContainsKey(spawnId))
                {
                    activeItems.Remove(spawnId);
                    itemPositions.Remove(spawnId);
                }
                Debug.Log($"归还道具 {item.name} 到 {prefab.name} 对象池");
                break;
            }
        }
    }

    // 禁用道具（由服务器指令调用）
    public void DisableItem(int spawnId)
    {
        if (activeItems.TryGetValue(spawnId, out GameObject item))
        {
            ReturnItemToPool(item);
            Debug.Log($"禁用道具 spawnId: {spawnId}");
        }
        else
        {
            Debug.LogWarning($"未找到道具 spawnId: {spawnId}");
        }
    }

    // 客户端根据服务器指令生成道具
    public void SpawnItemFromServer(int spawnId, string itemType, Vector3 position)
    {
        GameObject prefab = itemPrefabs.Find(p => p.name == itemType);
        if (prefab == null)
        {
            Debug.LogError($"未找到道具预制体: {itemType}");
            return;
        }

        GameObject item = GetPooledItem(prefab);
        item.transform.SetPositionAndRotation(position, Quaternion.identity);
        item.transform.SetParent(null);
        activeItems.Add(spawnId, item);
        itemPositions.Add(spawnId, position);
        Debug.Log($"客户端生成道具: {itemType} (spawnId: {spawnId}) 在位置: {position}");

        MeshRenderer renderer = item.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    // 获取道具的spawnId
    public int GetSpawnId(GameObject item)
    {
        foreach (var kvp in activeItems)
        {
            if (kvp.Value == item)
            {
                return kvp.Key;
            }
        }
        return -1;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Vector3 center = new Vector3(mapSize.x / 2f, 0f, mapSize.y / 2f);
        Gizmos.DrawWireCube(center, new Vector3(mapSize.x, 1f, mapSize.y));
    }
}