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
    private ObjectPool<GameObject> playerPool; // 对象池
    private GameObject playerPrefab; // 玩家预制体，通过 Inspector 或代码设置
    private Vector3 startPoint = new Vector3(500f, 0.5f, 500f);
    private bool loginStatus = false;
    private bool online = false;
    private string loginAccount;
    private int stage;
    private int playerId;
    private const int MAX_PLAYERS = 200; // 最大玩家数量


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
        Debug.LogWarning($"未定义的服务器 MapId: {mapId}，默认使用 Main");
        return "Main"; // 默认场景
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePlayerPool(); // 初始化对象池
            Debug.Log("GameManager 单例初始化完成");
        }
        else
        {
            Debug.LogWarning("检测到重复的 GameManager 实例，销毁当前对象");
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
                    Debug.LogError("playerPrefab 为空，无法创建对象池！");
                    return null;
                }
                GameObject player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                player.SetActive(false); // 默认禁用
                return player;
            },
            actionOnGet: (player) => player.SetActive(true),
            actionOnRelease: (player) => player.SetActive(false),
            actionOnDestroy: (player) => Destroy(player),
            defaultCapacity: 100, // 初始容量
            maxSize: MAX_PLAYERS // 最大容量，与机器人数量匹配
        );
        Debug.Log("玩家对象池初始化完成");
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
            Debug.LogWarning($"玩家数量超过上限 {MAX_PLAYERS}，忽略玩家 {playerId}");
#endif
            return null;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("playerPrefab 为空！");
            return null;
        }
        // 从对象池获取玩家对象
        GameObject playerObject = playerPool.Get();
        if (playerObject == null)
        {
            Debug.LogError("对象池返回空对象！");
            return null;
        }

        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("PlayerMovement 为空！");
            Destroy(playerObject);
            return null;
        }

        playerObject.transform.position = position;
        playerObject.transform.rotation = Quaternion.identity;
        movement.Initialize(playerId, isLocal, this.stage);
        playerObjects[playerId] = playerObject;
        playerObject.tag = "Player";
        //Debug.Log($"玩家 {playerId} 从对象池获取，位置={playerObject.transform.position}");
        if (OnPlayerSpawned != null && isLocal)
        {
            Debug.Log($"触发 OnPlayerSpawned 事件，监听器数量={OnPlayerSpawned.GetInvocationList().Length}");
            OnPlayerSpawned.Invoke(playerObject);
        }
        
        return playerObject;
    }

    public void RemovePlayer(int playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject player))
        {
            playerPool.Release(player); // 归还到对象池
            playerObjects.Remove(playerId);
            Debug.Log($"玩家 {playerId} 已归还到对象池。");
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
            NetworkMessageHandler.Instance.SendPlayerOnlineRequest(this.playerId, this.stage, this.startPoint);
            // 立即生成玩家，移除 0.1 秒延迟
            GameObject player = SpawnPlayer(this.playerId, true, this.startPoint);
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

    private void OnDestroy()
    {
        // 清理对象池
        playerPool?.Dispose();
        Debug.Log("GameManager 对象池已销毁");
    }
}