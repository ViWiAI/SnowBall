using Game.Network;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();
    private ObjectPool<GameObject> playerPool; // �����
    private GameObject playerPrefab; // ���Ԥ���壬ͨ�� Inspector ���������
    private Vector3 startPoint = new Vector3(500f, 0.5f, 500f);
    private bool loginStatus = false;
    private bool online = false;
    private string loginAccount;
    private int stage;
    private int playerId;
    private const int MAX_PLAYERS = 200; // ����������


    public delegate void PlayerSpawnedHandler(GameObject player);
    public event PlayerSpawnedHandler OnPlayerSpawned;


    // MapId (int) ���������Ƶ�ӳ��
    private static readonly Dictionary<int, string> MapIdToSceneName = new Dictionary<int, string>
    {
        { 1, "Main" },
        { 2, "Stage1" },
        { 3, "Stage2" },
    };

    // ���������� MapId (int) ת��Ϊ��������
    public string GetSceneNameFromMapId(int mapId)
    {
        if (MapIdToSceneName.TryGetValue(mapId, out string sceneName))
        {
            return sceneName;
        }
        Debug.LogWarning($"δ����ķ����� MapId: {mapId}��Ĭ��ʹ�� Main");
        return "Main"; // Ĭ�ϳ���
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePlayerPool(); // ��ʼ�������
            Debug.Log("GameManager ������ʼ�����");
        }
        else
        {
            Debug.LogWarning("��⵽�ظ��� GameManager ʵ�������ٵ�ǰ����");
            Destroy(gameObject);
        }
    }

    private void InitializePlayerPool()
    {
        playerPool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                if (playerPrefab == null)
                {
                    Debug.LogError("playerPrefab Ϊ�գ��޷���������أ�");
                    return null;
                }
                GameObject player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                player.SetActive(false); // Ĭ�Ͻ���
                return player;
            },
            actionOnGet: (player) => player.SetActive(true),
            actionOnRelease: (player) => player.SetActive(false),
            actionOnDestroy: (player) => Destroy(player),
            defaultCapacity: 100, // ��ʼ����
            maxSize: MAX_PLAYERS // ��������������������ƥ��
        );
        Debug.Log("��Ҷ���س�ʼ�����");
    }

    public void StartGame()
    {
        this.SetOnline(true);
        this.SetPlayerPrefab(Resources.Load<GameObject>("Prefabs/Player"));
        this.EnterStage();
    }

    public int PlayerId() => playerId;

    public void SetPlayerId(int pid)
    {
        playerId = pid;
    }

    public void SetOnline(bool status)
    {
        online = status;
    }

    public bool GetOnline()
    {
        return online;
    }
    public int GetStage()
    {
        return stage;
    }

    public void SetStage(int stage)
    {
        this.stage = stage;
    }

    public GameObject GetPlayerObject(int playerId)
    {
        playerObjects.TryGetValue(playerId, out GameObject player);
        return player;
    }

    public GameObject SpawnPlayer(int playerId, bool isLocal, Vector3 position)
    {
        if (playerObjects.Count >= MAX_PLAYERS && !isLocal)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"��������������� {MAX_PLAYERS}��������� {playerId}");
#endif
            return null;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("playerPrefab Ϊ�գ�");
            return null;
        }
        // �Ӷ���ػ�ȡ��Ҷ���
        GameObject playerObject = playerPool.Get();
        if (playerObject == null)
        {
            Debug.LogError("����ط��ؿն���");
            return null;
        }

        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("PlayerMovement Ϊ�գ�");
            Destroy(playerObject);
            return null;
        }

        playerObject.transform.position = position;
        playerObject.transform.rotation = Quaternion.identity;
        movement.Initialize(playerId, isLocal, this.stage);
        playerObjects[playerId] = playerObject;
        playerObject.tag = "Player";
        //Debug.Log($"��� {playerId} �Ӷ���ػ�ȡ��λ��={playerObject.transform.position}");
        if (OnPlayerSpawned != null && isLocal)
        {
            Debug.Log($"���� OnPlayerSpawned �¼�������������={OnPlayerSpawned.GetInvocationList().Length}");
            OnPlayerSpawned.Invoke(playerObject);
        }
        
        return playerObject;
    }

    public void RemovePlayer(int playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject player))
        {
            playerPool.Release(player); // �黹�������
            playerObjects.Remove(playerId);
            Debug.Log($"��� {playerId} �ѹ黹������ء�");
        }
    }

    public void EnterStage()
    {
        if (stage == 0)
        {
            Debug.LogError("Ŀ�곡������Ϊ�գ��޷����س�����");
            return;
        }
        Debug.Log($"��ʼ���س�����{stage}");
        SceneManager.LoadSceneAsync(GetSceneNameFromMapId(this.stage), LoadSceneMode.Single).completed += (op) =>
        {
            NetworkMessageHandler.Instance.SendPlayerOnlineRequest(this.playerId, this.stage, this.startPoint);
            // ����������ң��Ƴ� 0.1 ���ӳ�
            GameObject player = SpawnPlayer(this.playerId, true, this.startPoint);
            if (player == null)
            {
                Debug.LogError("���ʵ����ʧ�ܣ�");
            }
            UpdateLightingSettings();
        };
    }

    private void UpdateLightingSettings()
    {
        // ǿ�Ƹ��¹���������ȷ�� Skybox �ͻ�������ȷ��Ⱦ
        if (RenderSettings.skybox != null)
        {
            Debug.Log($"Skybox ����������Ϊ��{RenderSettings.skybox.name}");
        }
        else
        {
            Debug.LogWarning("RenderSettings.skybox δ���ã�");
        }
        DynamicGI.UpdateEnvironment(); // ���»�����
        Debug.Log("���������Ѹ���");
    }

    public void SetLoginStatus(bool status)
    {
        loginStatus = status;
    }

    public bool GetLoginStatus()
    {
        return loginStatus;
    }

    public void SetLoginAccount(string account)
    {
        loginAccount = account;
    }

    public string GetLoginAccount()
    {
        return loginAccount;
    }

    // ������������̬�������Ԥ����
    public void SetPlayerPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("�������õ����Ԥ����Ϊ�գ�");
            return;
        }

        if (prefab.GetComponent<PlayerMovement>() == null)
        {
            Debug.LogError("���õ����Ԥ����ȱ�� PlayerMovement �����");
            return;
        }

        playerPrefab = prefab;
        Debug.Log($"���Ԥ����������Ϊ��{prefab.name}");
    }

    private void OnDestroy()
    {
        // ��������
        playerPool?.Dispose();
        Debug.Log("GameManager �����������");
    }
}