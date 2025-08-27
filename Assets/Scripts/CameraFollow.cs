using Cinemachine;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CinemachineVirtualCamera), typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Header("�����������")]
    [SerializeField] private float damping = 0.5f; // ����ϵ��
    [SerializeField] private Vector2 mapSize = new Vector2(1000f, 1000f); // ��ͼ�ߴ�

    [Header("�ֶ���������")]
    [SerializeField] private float mouseScrollSensitivity = 20f;
    [SerializeField] private float minManualHeight = 10f;
    [SerializeField] private float maxManualHeight = 50f;
    [SerializeField] private float manualZoomSpeed = 20f;
    [SerializeField] private float minOrthoSize = 50f;
    [SerializeField] private float maxOrthoSize = 400f;

    [Header("�Զ���������")]
    [SerializeField] private bool enableAutoZoom = true;
    [SerializeField] private float minAutoHeight = 15f;
    [SerializeField] private float maxAutoHeight = 80f;
    [SerializeField] private float autoZoomSensitivity = 0.5f;
    [SerializeField] private float autoZoomSmoothTime = 0.5f;

    private Transform player; // ���Transform
    private CinemachineVirtualCamera vcam;
    private CinemachineTransposer transposer;
    private CinemachineConfiner confiner;
    private float currentManualHeight;
    private float currentAutoHeight;
    private float autoZoomVelocity;
    private Transform followTarget;
    private bool isSceneLoading; // ��ǳ�������״̬
    private Vector3 initialPlayerPosition = new Vector3(500f, 0.5f, 500f); // ��ҳ�����

    void Awake()
    {
        // ����ģʽ
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("CameraFollow ������ʼ�����");
        }
        else
        {
            Debug.LogWarning("��⵽�ظ��� CameraFollow ʵ�������ٵ�ǰ����");
            Destroy(gameObject);
            return;
        }

        // ��ʼ��������
        vcam = GetComponent<CinemachineVirtualCamera>();
        Camera mainCamera = GetComponent<Camera>();
        if (mainCamera != null)
        {
            mainCamera.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.Skybox; // ȷ����Ⱦ Skybox
            if (!mainCamera.GetComponent<CinemachineBrain>())
            {
                mainCamera.gameObject.AddComponent<CinemachineBrain>();
            }
            Debug.Log($"��������ã���ǩ=MainCamera, ClearFlags={mainCamera.clearFlags}");
        }
        else
        {
            Debug.LogError("CameraFollow ��Ҫ���� Camera �����");
        }
        SubscribeToGameManager();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // �ٴγ��Զ��ģ�ȷ�� GameManager �ѳ�ʼ��
        SubscribeToGameManager();
        // ������Ϸ״̬Ϊ online ʱ������Ҳ���
        if (GameManager.Instance != null && GameManager.Instance.GetOnline())
        {
            StartCoroutine(CheckForPlayer());
        }
    }

    private void SubscribeToGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerSpawned -= OnPlayerSpawned; // ��ֹ�ظ�����
            GameManager.Instance.OnPlayerSpawned += OnPlayerSpawned;
            Debug.Log("CameraFollow �ɹ����� GameManager.OnPlayerSpawned �¼�");
        }
        else
        {
            Debug.LogWarning("�޷����� OnPlayerSpawned �¼���GameManager δ��ʼ��");
        }
    }


    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"���� {scene.name} �������");
        if (RenderSettings.skybox != null)
        {
            Debug.Log($"�³��� Skybox ���ʣ�{RenderSettings.skybox.name}");
        }
        else
        {
            Debug.LogWarning($"�³���δ���� Skybox ���ʣ�");
        }

        // ��ǳ�������״̬
        isSceneLoading = true;

        // �������λ�ã������ʼ�ζ���
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
            vcam.LookAt = null; // ��ʱ��� LookAt���ȴ��������
            // ����������������λ
            if (transposer != null)
            {
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
            }
            // ǿ�� CinemachineBrain ��������
            var brain = GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                brain.ManualUpdate();
            }
            Debug.Log($"�����ʼ��λ����ҳ�����: {initialPlayerPosition}, ��ת={vcam.transform.rotation.eulerAngles}, ���λ��={transform.position}");
        }

        // ���³������غ�������״̬��������Ҳ���
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
                    Debug.Log($"ͨ�����ü���ҵ���ң�{playerObj.name}");
                    isSceneLoading = false; // ����ҵ��������������״̬
                    yield break;
                }
            }
            attempts++;
        }
        if (player == null)
        {
            Debug.LogError("���ü��δ���ҵ���Ҷ���");
        }
        isSceneLoading = false;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerSpawned -= OnPlayerSpawned;
            Debug.Log("CameraFollow ȡ������ OnPlayerSpawned �¼�");
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void InitializeCamera()
    {
        if (player == null)
        {
            Debug.LogWarning("��ʼ�����ʧ�ܣ��������Ϊ��");
            return;
        }

        // �����������Ŀ��
        if (followTarget == null)
        {
            GameObject followTargetObj = new GameObject("CameraFollowTarget");
            followTarget = followTargetObj.transform;
            DontDestroyOnLoad(followTargetObj);
        }
        followTarget.position = new Vector3(player.position.x, 0f, player.position.z);
        vcam.Follow = followTarget;
        vcam.LookAt = player;

        // ���� Transposer
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

        // ǿ�� 90�� ����
        vcam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        vcam.m_Lens.Orthographic = true;
        vcam.m_Lens.OrthographicSize = minOrthoSize;

        // ���� Confiner
        confiner = vcam.GetComponent<CinemachineConfiner>();
        if (confiner == null)
        {
            confiner = vcam.AddComponent<CinemachineConfiner>();
        }
        confiner.m_ConfineMode = CinemachineConfiner.Mode.Confine2D;
        confiner.m_BoundingShape2D = CreateBoundingBox();
        confiner.m_ConfineScreenEdges = true;

        // ǿ�����������λ�����λ��
        transform.position = new Vector3(player.position.x, maxManualHeight, player.position.z);
        followTarget.position = new Vector3(player.position.x, 0f, player.position.z);
        // ǿ�� CinemachineBrain ��������
        var brain = GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.ManualUpdate();
        }
        Debug.Log($"�����ʼ�����: �߶�={currentManualHeight:F2}, OrthoSize={vcam.m_Lens.OrthographicSize:F2}, " +
                  $"��Ұ���={vcam.m_Lens.OrthographicSize * 1.777:F2}, ��ת={vcam.transform.rotation.eulerAngles}, ���λ��={transform.position}");
    }

    private void OnPlayerSpawned(GameObject playerObj)
    {
        Debug.Log($"OnPlayerSpawned �������󶨵���ң�{playerObj.name}, λ��={playerObj.transform.position}");
        player = playerObj.transform;
        InitializeCamera();
    }

    private Collider2D CreateBoundingBox()
    {
        GameObject boundsObj = new GameObject("CameraBounds");
        BoxCollider2D bounds = boundsObj.AddComponent<BoxCollider2D>();
        bounds.size = mapSize;
        bounds.offset = mapSize / 2f;
        Debug.Log($"�߽�򴴽�: ��С={mapSize}, ����={bounds.offset}");
        return bounds;
    }

    void Update()
    {
        if (player == null)
        {
            // Debug.LogWarning("Update ���������Ϊ�գ�����޷����棡");
            return;
        }

        // ÿ֡ȷ�����Ϊ 90�� ����
        if (vcam.transform.rotation.eulerAngles != new Vector3(90f, 0f, 0f))
        {
            vcam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Debug.LogWarning("�����ת���޸ģ���ǿ������Ϊ 90�� ����");
        }

        // ȷ���������Ⱦ Skybox
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.clearFlags != CameraClearFlags.Skybox)
        {
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            Debug.LogWarning("����� ClearFlags ���޸ģ�������Ϊ Skybox");
        }

        // ȷ�� Follow Ŀ����Ч
        if (vcam.Follow != followTarget)
        {
            vcam.Follow = followTarget;
            Debug.LogWarning("��� Follow Ŀ�걻�޸ģ�������Ϊ followTarget");
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
            //Debug.Log($"�ֶ������߶�: {currentManualHeight:F2}, OrthoSize: {vcam.m_Lens.OrthographicSize:F2}, ��Ұ���: {vcam.m_Lens.OrthographicSize * 1.777:F2}");
        }
    }

    private void UpdateAutoZoom()
    {
        if (!enableAutoZoom) return;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogWarning("��Ҷ���ȱ�� PlayerMovement ������Զ������ѽ��á�");
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
            Debug.LogWarning("ConfineCamera: followTarget �� player Ϊ�գ���������");
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

        // ��������Ŀ��λ�ã���ʹ����ҵ� x �� z ����
        float clampedX = isSceneLoading ? playerPos.x : Mathf.Clamp(playerPos.x, minX, maxX);
        float clampedZ = isSceneLoading ? playerPos.z : Mathf.Clamp(playerPos.z, minZ, maxZ);
        followTarget.position = new Vector3(clampedX, 0f, clampedZ); // �̶� y=0������߶ȸ���

        //Debug.Log($"�������: ���λ��={playerPos}, ����Ŀ��={followTarget.position}, OrthoSize={orthoSize}, ��ͼ��С={mapSize}");
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
            Debug.Log($"���µ�ͼ��С: {newMapSize}");
        }
    }
}