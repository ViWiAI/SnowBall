using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();
    private GameObject playerPrefab; // ���Ԥ���壬ͨ�� Inspector ���������
    private bool loginStatus = false;
    private bool online = false;
    private string loginAccount;
    private int stage;
    private int playerId;

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
        Debug.LogWarning($"δ����ķ����� MapId: {mapId}��Ĭ��ʹ�� WorldMap");
        return "Main"; // Ĭ�ϳ���
    }

    public int PlayerId() => playerId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameManager ������ʼ�����");
        }
        else
        {
            Debug.LogWarning("��⵽�ظ��� GameManager ʵ�������ٵ�ǰ����");
            Destroy(gameObject);
        }
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
        if (playerPrefab == null)
        {
            Debug.LogError("playerPrefab Ϊ�գ�");
            return null;
        }
        GameObject playerObject = Instantiate(playerPrefab, position, Quaternion.identity);
        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("PlayerMovement Ϊ�գ�");
            Destroy(playerObject);
            return null;
        }
        movement.Initialize(playerId, isLocal, this.stage);
        playerObjects[playerId] = playerObject;
        playerObject.tag = "Player";
        Debug.Log($"��� {playerId} ʵ������λ��={playerObject.transform.position}");
        if (OnPlayerSpawned != null)
        {
            Debug.Log($"���� OnPlayerSpawned �¼�������������={OnPlayerSpawned.GetInvocationList().Length}");
            OnPlayerSpawned.Invoke(playerObject);
        }
        else
        {
            Debug.LogWarning("OnPlayerSpawned �¼��޼�������");
        }
        return playerObject;
    }

    public void RemovePlayer(int playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject player))
        {
            Destroy(player);
            playerObjects.Remove(playerId);
            Debug.Log($"��� {playerId} ���Ƴ���");
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
            // ����������ң��Ƴ� 0.1 ���ӳ�
            GameObject player = SpawnPlayer(this.playerId, true, new Vector3(500f, 0.5f, 500f));
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
}