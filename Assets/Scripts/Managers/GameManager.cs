using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Dictionary<int, GameObject> playerObjects = new Dictionary<int, GameObject>();
    private GameObject playerPrefab; // 玩家预制体，通过 Inspector 或代码设置
    private bool loginStatus = false;
    private bool online = false;
    private string loginAccount;
    private int stage;
    private int playerId;

    public delegate void PlayerSpawnedHandler(GameObject player);
    public event PlayerSpawnedHandler OnPlayerSpawned;


    // MapId (int) 到场景名称的映射
    private static readonly Dictionary<int, string> MapIdToSceneName = new Dictionary<int, string>
    {
        { 1, "Main" },
        { 2, "Stage1" },
        { 3, "Stage2" },
    };

    // 将服务器的 MapId (int) 转换为场景名称
    public string GetSceneNameFromMapId(int mapId)
    {
        if (MapIdToSceneName.TryGetValue(mapId, out string sceneName))
        {
            return sceneName;
        }
        Debug.LogWarning($"未定义的服务器 MapId: {mapId}，默认使用 WorldMap");
        return "Main"; // 默认场景
    }

    public int PlayerId() => playerId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameManager 单例初始化完成");
        }
        else
        {
            Debug.LogWarning("检测到重复的 GameManager 实例，销毁当前对象");
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
            Debug.LogError("playerPrefab 为空！");
            return null;
        }
        GameObject playerObject = Instantiate(playerPrefab, position, Quaternion.identity);
        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("PlayerMovement 为空！");
            Destroy(playerObject);
            return null;
        }
        movement.Initialize(playerId, isLocal, this.stage);
        playerObjects[playerId] = playerObject;
        playerObject.tag = "Player";
        Debug.Log($"玩家 {playerId} 实例化，位置={playerObject.transform.position}");
        if (OnPlayerSpawned != null)
        {
            Debug.Log($"触发 OnPlayerSpawned 事件，监听器数量={OnPlayerSpawned.GetInvocationList().Length}");
            OnPlayerSpawned.Invoke(playerObject);
        }
        else
        {
            Debug.LogWarning("OnPlayerSpawned 事件无监听器！");
        }
        return playerObject;
    }

    public void RemovePlayer(int playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject player))
        {
            Destroy(player);
            playerObjects.Remove(playerId);
            Debug.Log($"玩家 {playerId} 已移除。");
        }
    }

    public void EnterStage()
    {
        if (stage == 0)
        {
            Debug.LogError("目标场景名称为空！无法加载场景。");
            return;
        }
        Debug.Log($"开始加载场景：{stage}");
        SceneManager.LoadSceneAsync(GetSceneNameFromMapId(this.stage), LoadSceneMode.Single).completed += (op) =>
        {
            // 立即生成玩家，移除 0.1 秒延迟
            GameObject player = SpawnPlayer(this.playerId, true, new Vector3(500f, 0.5f, 500f));
            if (player == null)
            {
                Debug.LogError("玩家实例化失败！");
            }
            UpdateLightingSettings();
        };
    }

    private void UpdateLightingSettings()
    {
        // 强制更新光照设置以确保 Skybox 和环境光正确渲染
        if (RenderSettings.skybox != null)
        {
            Debug.Log($"Skybox 材质已设置为：{RenderSettings.skybox.name}");
        }
        else
        {
            Debug.LogWarning("RenderSettings.skybox 未设置！");
        }
        DynamicGI.UpdateEnvironment(); // 更新环境光
        Debug.Log("光照设置已更新");
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

    // 新增方法：动态设置玩家预制体
    public void SetPlayerPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("尝试设置的玩家预制体为空！");
            return;
        }

        if (prefab.GetComponent<PlayerMovement>() == null)
        {
            Debug.LogError("设置的玩家预制体缺少 PlayerMovement 组件！");
            return;
        }

        playerPrefab = prefab;
        Debug.Log($"玩家预制体已设置为：{prefab.name}");
    }
}