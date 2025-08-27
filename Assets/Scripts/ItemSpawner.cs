using UnityEngine;
using System.Collections.Generic;
using Game.Network;

public class ItemSpawner : MonoBehaviour
{
    public static ItemSpawner Instance { get; private set; }

    [Header("��������")]
    public List<GameObject> itemPrefabs; // ����Ԥ�����б���ʯ��ҩˮ��ѩ��ȣ�
    public Vector2 mapSize = new Vector2(1000f, 1000f); // ��ͼ�ߴ磨1��1000��
    public int initialPoolSize = 3500; // ÿ�ֵ��ߵĳ�ʼ�ش�С

    private Dictionary<GameObject, Queue<GameObject>> itemPools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<int, GameObject> activeItems = new Dictionary<int, GameObject>(); // ���ټ�����ߣ���Ϊspawn_id
    private Dictionary<int, Vector3> itemPositions = new Dictionary<int, Vector3>(); // spawn_id��λ�õ�ӳ��

    private void Awake()
    {
        // Singleton ʵ��
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
        Debug.Log($"ItemSpawner λ��: {transform.position}, ����: {transform.localScale}");
        Debug.Log($"��ͼ�ߴ� (Inspector): X={mapSize.x}, Z={mapSize.y}");
        InitializePools();
        // �Ƴ����������߼����ȴ���������MSG_ITEM_SPAWNED��Ϣ
    }

    // ��ʼ�������
    private void InitializePools()
    {
        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogError("itemPrefabs �б�Ϊ�գ����� Inspector �з������Ԥ���壡");
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
          //  Debug.Log($"Ϊ {prefab.name} ��������أ���ʼ��С: {initialPoolSize}");
        }
    }

    // �Ӷ���ػ�ȡ����
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
            Debug.LogWarning($"����� {prefab.name} �ѿգ���̬ʵ�����µ���");
            return Instantiate(prefab);
        }
    }

    // �黹���ߵ������
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
                Debug.Log($"�黹���� {item.name} �� {prefab.name} �����");
                break;
            }
        }
    }

    // ���õ��ߣ��ɷ�����ָ����ã�
    public void DisableItem(int spawnId)
    {
        if (activeItems.TryGetValue(spawnId, out GameObject item))
        {
            ReturnItemToPool(item);
            Debug.Log($"���õ��� spawnId: {spawnId}");
        }
        else
        {
            Debug.LogWarning($"δ�ҵ����� spawnId: {spawnId}");
        }
    }

    // �ͻ��˸��ݷ�����ָ�����ɵ���
    public void SpawnItemFromServer(int spawnId, string itemType, Vector3 position)
    {
        GameObject prefab = itemPrefabs.Find(p => p.name == itemType);
        if (prefab == null)
        {
            Debug.LogError($"δ�ҵ�����Ԥ����: {itemType}");
            return;
        }

        GameObject item = GetPooledItem(prefab);
        item.transform.SetPositionAndRotation(position, Quaternion.identity);
        item.transform.SetParent(null);
        activeItems.Add(spawnId, item);
        itemPositions.Add(spawnId, position);
        Debug.Log($"�ͻ������ɵ���: {itemType} (spawnId: {spawnId}) ��λ��: {position}");

        MeshRenderer renderer = item.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    // ��ȡ���ߵ�spawnId
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