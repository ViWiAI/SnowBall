using Cinemachine;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CinemachineVirtualCamera), typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Header("相机基础设置")]
    [SerializeField] private float damping = 0.5f; // 阻尼系数
    [SerializeField] private Vector2 mapSize = new Vector2(1000f, 1000f); // 地图尺寸

    [Header("手动缩放设置")]
    [SerializeField] private float mouseScrollSensitivity = 20f;
    [SerializeField] private float minManualHeight = 10f;
    [SerializeField] private float maxManualHeight = 50f;
    [SerializeField] private float manualZoomSpeed = 20f;
    [SerializeField] private float minOrthoSize = 50f;
    [SerializeField] private float maxOrthoSize = 400f;

    [Header("自动缩放设置")]
    [SerializeField] private bool enableAutoZoom = true;
    [SerializeField] private float minAutoHeight = 15f;
    [SerializeField] private float maxAutoHeight = 80f;
    [SerializeField] private float autoZoomSensitivity = 0.5f;
    [SerializeField] private float autoZoomSmoothTime = 0.5f;

    private Transform player; // 玩家Transform
    private CinemachineVirtualCamera vcam;
    private CinemachineTransposer transposer;
    private CinemachineConfiner confiner;
    private float currentManualHeight;
    private float currentAutoHeight;
    private float autoZoomVelocity;
    private Transform followTarget;
    private bool isSceneLoading; // 标记场景加载状态
    private Vector3 initialPlayerPosition = new Vector3(500f, 0.5f, 500f); // 玩家出生点

    void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("CameraFollow 单例初始化完成");
        }
        else
        {
            Debug.LogWarning("检测到重复的 CameraFollow 实例，销毁当前对象");
            Destroy(gameObject);
            return;
        }

        // 初始化相机组件
        vcam = GetComponent<CinemachineVirtualCamera>();
        Camera mainCamera = GetComponent<Camera>();
        if (mainCamera != null)
        {
            mainCamera.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.Skybox; // 确保渲染 Skybox
            if (!mainCamera.GetComponent<CinemachineBrain>())
            {
                mainCamera.gameObject.AddComponent<CinemachineBrain>();
            }
            Debug.Log($"主相机配置：标签=MainCamera, ClearFlags={mainCamera.clearFlags}");
        }
        else
        {
            Debug.LogError("CameraFollow 需要附加 Camera 组件！");
        }
        SubscribeToGameManager();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // 再次尝试订阅，确保 GameManager 已初始化
        SubscribeToGameManager();
        // 仅在游戏状态为 online 时启动玩家查找
        if (GameManager.Instance != null && GameManager.Instance.GetOnline())
        {
            StartCoroutine(CheckForPlayer());
        }
    }

    private void SubscribeToGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerSpawned -= OnPlayerSpawned; // 防止重复订阅
            GameManager.Instance.OnPlayerSpawned += OnPlayerSpawned;
            Debug.Log("CameraFollow 成功订阅 GameManager.OnPlayerSpawned 事件");
        }
        else
        {
            Debug.LogWarning("无法订阅 OnPlayerSpawned 事件：GameManager 未初始化");
        }
    }


    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"场景 {scene.name} 加载完成");
        if (RenderSettings.skybox != null)
        {
            Debug.Log($"新场景 Skybox 材质：{RenderSettings.skybox.name}");
        }
        else
        {
            Debug.LogWarning($"新场景未设置 Skybox 材质！");
        }

        // 标记场景加载状态
        isSceneLoading = true;

        // 重置相机位置（避免初始晃动）
        if (scene.name == GameManager.Instance.GetSceneNameFromMapId(GameManager.Instance.GetStage()))
        {
            if (followTarget == null)
            {
                GameObject followTargetObj = new GameObject("CameraFollowTarget");
                followTarget = followTargetObj.transform;
                DontDestroyOnLoad(followTargetObj);
            }
            followTarget.position = new Vector3(initialPlayerPosition.x, 0f, initialPlayerPosition.z);
            transform.position = new Vector3(initialPlayerPosition.x, maxManualHeight, initialPlayerPosition.z);
            vcam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            vcam.Follow = followTarget;
            vcam.LookAt = null; // 暂时清空 LookAt，等待玩家生成
            // 禁用阻尼以立即定位
            if (transposer != null)
            {
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
            }
            // 强制 CinemachineBrain 立即更新
            var brain = GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                brain.ManualUpdate();
            }
            Debug.Log($"相机初始定位到玩家出生点: {initialPlayerPosition}, 旋转={vcam.transform.rotation.eulerAngles}, 相机位置={transform.position}");
        }

        // 在新场景加载后检查在线状态并启动玩家查找
        if (GameManager.Instance != null && GameManager.Instance.GetOnline())
        {
            StartCoroutine(CheckForPlayer());
        }
    }

    private IEnumerator CheckForPlayer()
    {
        int attempts = 0;
        while (player == null && attempts < 20)
        {
            yield return new WaitForSeconds(0.1f);
            if (GameManager.Instance != null && GameManager.Instance.GetOnline())
            {
                GameObject playerObj = GameManager.Instance.GetPlayerObject(GameManager.Instance.PlayerId());
                if (playerObj != null)
                {
                    player = playerObj.transform;
                    InitializeCamera();
                    Debug.Log($"通过备用检查找到玩家：{playerObj.name}");
                    isSceneLoading = false; // 玩家找到后结束场景加载状态
                    yield break;
                }
            }
            attempts++;
        }
        if (player == null)
        {
            Debug.LogError("备用检查未能找到玩家对象！");
        }
        isSceneLoading = false;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerSpawned -= OnPlayerSpawned;
            Debug.Log("CameraFollow 取消订阅 OnPlayerSpawned 事件");
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void InitializeCamera()
    {
        if (player == null)
        {
            Debug.LogWarning("初始化相机失败：玩家引用为空");
            return;
        }

        // 创建虚拟跟随目标
        if (followTarget == null)
        {
            GameObject followTargetObj = new GameObject("CameraFollowTarget");
            followTarget = followTargetObj.transform;
            DontDestroyOnLoad(followTargetObj);
        }
        followTarget.position = new Vector3(player.position.x, 0f, player.position.z);
        vcam.Follow = followTarget;
        vcam.LookAt = player;

        // 设置 Transposer
        transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
        {
            transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        }

        currentManualHeight = maxManualHeight;
        currentAutoHeight = maxManualHeight;
        transposer.m_FollowOffset = new Vector3(0f, maxManualHeight, 0f);
        transposer.m_XDamping = damping;
        transposer.m_YDamping = damping;
        transposer.m_ZDamping = damping;

        // 强制 90° 俯视
        vcam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        vcam.m_Lens.Orthographic = true;
        vcam.m_Lens.OrthographicSize = minOrthoSize;

        // 设置 Confiner
        confiner = vcam.GetComponent<CinemachineConfiner>();
        if (confiner == null)
        {
            confiner = vcam.AddComponent<CinemachineConfiner>();
        }
        confiner.m_ConfineMode = CinemachineConfiner.Mode.Confine2D;
        confiner.m_BoundingShape2D = CreateBoundingBox();
        confiner.m_ConfineScreenEdges = true;

        // 强制相机立即定位到玩家位置
        transform.position = new Vector3(player.position.x, maxManualHeight, player.position.z);
        followTarget.position = new Vector3(player.position.x, 0f, player.position.z);
        // 强制 CinemachineBrain 立即更新
        var brain = GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.ManualUpdate();
        }
        Debug.Log($"相机初始化完成: 高度={currentManualHeight:F2}, OrthoSize={vcam.m_Lens.OrthographicSize:F2}, " +
                  $"视野宽度={vcam.m_Lens.OrthographicSize * 1.777:F2}, 旋转={vcam.transform.rotation.eulerAngles}, 相机位置={transform.position}");
    }

    private void OnPlayerSpawned(GameObject playerObj)
    {
        Debug.Log($"OnPlayerSpawned 触发，绑定到玩家：{playerObj.name}, 位置={playerObj.transform.position}");
        player = playerObj.transform;
        InitializeCamera();
    }

    private Collider2D CreateBoundingBox()
    {
        GameObject boundsObj = new GameObject("CameraBounds");
        BoxCollider2D bounds = boundsObj.AddComponent<BoxCollider2D>();
        bounds.size = mapSize;
        bounds.offset = mapSize / 2f;
        Debug.Log($"边界框创建: 大小={mapSize}, 中心={bounds.offset}");
        return bounds;
    }

    void Update()
    {
        if (player == null)
        {
            // Debug.LogWarning("Update 中玩家引用为空，相机无法跟随！");
            return;
        }

        // 每帧确保相机为 90° 俯视
        if (vcam.transform.rotation.eulerAngles != new Vector3(90f, 0f, 0f))
        {
            vcam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Debug.LogWarning("相机旋转被修改，已强制重设为 90° 俯视");
        }

        // 确保主相机渲染 Skybox
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.clearFlags != CameraClearFlags.Skybox)
        {
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            Debug.LogWarning("主相机 ClearFlags 被修改，已重设为 Skybox");
        }

        // 确保 Follow 目标有效
        if (vcam.Follow != followTarget)
        {
            vcam.Follow = followTarget;
            Debug.LogWarning("相机 Follow 目标被修改，已重设为 followTarget");
        }

        HandleMouseScroll();
        UpdateAutoZoom();
        UpdateCameraHeight();
        ConfineCamera();
    }

    private void HandleMouseScroll()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.001f)
        {
            currentManualHeight -= scrollInput * mouseScrollSensitivity * manualZoomSpeed;
            currentManualHeight = Mathf.Clamp(currentManualHeight, minManualHeight, maxManualHeight);
            //Debug.Log($"手动调整高度: {currentManualHeight:F2}, OrthoSize: {vcam.m_Lens.OrthographicSize:F2}, 视野宽度: {vcam.m_Lens.OrthographicSize * 1.777:F2}");
        }
    }

    private void UpdateAutoZoom()
    {
        if (!enableAutoZoom) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogWarning("玩家对象缺少 PlayerMovement 组件，自动缩放已禁用。");
            return;
        }

        float snowballSize = movement.ballRadius;
        float targetAutoHeight = Mathf.Lerp(minAutoHeight, maxAutoHeight, snowballSize * autoZoomSensitivity);
        currentAutoHeight = Mathf.SmoothDamp(currentAutoHeight, targetAutoHeight, ref autoZoomVelocity, autoZoomSmoothTime);
    }

    private void UpdateCameraHeight()
    {
        float finalHeight = Mathf.Max(currentManualHeight, currentAutoHeight);
        Vector3 offset = transposer.m_FollowOffset;
        offset.y = finalHeight;
        transposer.m_FollowOffset = offset;

        float heightRatio = (finalHeight - minManualHeight) / (maxManualHeight - minManualHeight);
        float targetOrthoSize = Mathf.Lerp(maxOrthoSize, minOrthoSize, heightRatio);
        vcam.m_Lens.OrthographicSize = Mathf.Lerp(vcam.m_Lens.OrthographicSize, targetOrthoSize, Time.deltaTime * 15f);
    }

    private void ConfineCamera()
    {
        if (followTarget == null || player == null)
        {
            Debug.LogWarning("ConfineCamera: followTarget 或 player 为空，跳过更新");
            return;
        }

        float orthoSize = vcam.m_Lens.OrthographicSize;
        float aspectRatio = (float)Screen.width / Screen.height;
        float viewportWidth = orthoSize * aspectRatio;
        float viewportHeight = orthoSize;

        Vector3 playerPos = player.position;
        float minX = viewportWidth;
        float maxX = mapSize.x - viewportWidth;
        float minZ = viewportHeight;
        float maxZ = mapSize.y - viewportHeight;

        // 调整跟随目标位置，仅使用玩家的 x 和 z 坐标
        float clampedX = isSceneLoading ? playerPos.x : Mathf.Clamp(playerPos.x, minX, maxX);
        float clampedZ = isSceneLoading ? playerPos.z : Mathf.Clamp(playerPos.z, minZ, maxZ);
        followTarget.position = new Vector3(clampedX, 0f, clampedZ); // 固定 y=0，避免高度干扰

        //Debug.Log($"相机跟随: 玩家位置={playerPos}, 跟随目标={followTarget.position}, OrthoSize={orthoSize}, 地图大小={mapSize}");
    }

    public void SetAutoZoomEnabled(bool enabled) => enableAutoZoom = enabled;

    public void SetManualZoomRange(float min, float max)
    {
        minManualHeight = min;
        maxManualHeight = max;
        currentManualHeight = Mathf.Clamp(currentManualHeight, min, max);
    }

    public void SetAutoZoomRange(float min, float max)
    {
        minAutoHeight = min;
        maxAutoHeight = max;
    }

    public void SetCameraHeightImmediate(float height)
    {
        currentManualHeight = Mathf.Clamp(height, minManualHeight, maxManualHeight);
        UpdateCameraHeight();
    }

    public void SetMapSize(Vector2 newMapSize)
    {
        mapSize = newMapSize;
        if (confiner != null)
        {
            confiner.m_BoundingShape2D = CreateBoundingBox();
            Debug.Log($"更新地图大小: {newMapSize}");
        }
    }
}