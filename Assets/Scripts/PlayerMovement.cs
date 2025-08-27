using Best.HTTP.Shared.PlatformSupport.Memory;
using Game.Network;
using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("运动设置")]
    public float moveSpeed = 3f; // 移动速度（米/秒）
    public float ballRadius = 0.5f; // 雪球半径（米）
    public float drag = 50f; // 模拟阻力
    public float brakeSpeed = 0.01f; // 刹车时每帧减少的速度
    public float inputSmoothing = 10f; // 输入平滑系数
    [SerializeField] private Vector2 mapSize = new Vector2(1000f, 1000f); // 地图尺寸

    private Vector3 rawMoveDirection; // 原始输入方向
    private Vector3 smoothedMoveDirection; // 平滑输入方向
    private Vector3 currentVelocity;
    private float currentSpeed;
    private Vector3 lastMoveDirection;
    private ItemSpawner itemSpawner; // 引用 ItemSpawner
    private bool isLocalPlayer; // 是否为本地玩家
    private int playerId; // 玩家唯一ID
    private int stage; // 地图ID
    private float lastSendTime;
    private const float SEND_INTERVAL = 0.1f;

    public float CurrentSpeed => currentSpeed;
    public int PlayerId => playerId;
    public bool IsLocalPlayer => isLocalPlayer;

    public static event Action<string, GameObject> OnItemCollected;

    void Start()
    {
        // 初始化 Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationY;
        }

        // 初始化 MeshRenderer
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // 查找 ItemSpawner
        itemSpawner = FindObjectOfType<ItemSpawner>();
        if (itemSpawner == null)
        {
            Debug.LogError("未找到 ItemSpawner 组件！");
        }

        Application.targetFrameRate = 60;
    }

    public void Initialize(int playerId, bool isLocal, int stage)
    {
        this.playerId = playerId;
        this.isLocalPlayer = isLocal;
        this.stage = stage;
    }

    void Update()
    {
        if (!isLocalPlayer) return; // 远程玩家由服务器控制

        if (Input.GetKey(KeyCode.Space))
        {
            StopMovement();
            Debug.Log($"刹车中：当前速度 = {currentVelocity.magnitude:F2}");
            SendStopRequest();
        }
        else
        {
            // 处理键盘输入
            HandleKeyboardInput();

            float xThreshold = 0.1f;
            Vector3 moveDirection;
            if (Mathf.Abs(smoothedMoveDirection.z) > Mathf.Abs(smoothedMoveDirection.x) + xThreshold)
            {
                moveDirection = new Vector3(0, 0, smoothedMoveDirection.z).normalized;
            }
            else if (Mathf.Abs(smoothedMoveDirection.x) > Mathf.Abs(smoothedMoveDirection.z) + xThreshold)
            {
                moveDirection = new Vector3(smoothedMoveDirection.x, 0, 0).normalized;
            }
            else
            {
                moveDirection = new Vector3(smoothedMoveDirection.x, 0, smoothedMoveDirection.z).normalized;
            }

            if (moveDirection != Vector3.zero)
            {
                currentVelocity = Vector3.Lerp(currentVelocity, moveDirection * moveSpeed, Time.deltaTime * 0.5f);
                lastMoveDirection = moveDirection;
            }
            else
            {
                currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, Time.deltaTime * drag);
                lastMoveDirection = currentVelocity.normalized;
            }

            // 发送移动请求（仅在有输入且在移动时）
            if (smoothedMoveDirection != Vector3.zero && currentSpeed > 0.01f)
            {
                SendMoveRequest(moveDirection);
            }
        }

        currentSpeed = currentVelocity.magnitude;

        // 本地玩家移动
        if (currentSpeed > 0.01f)
        {
            Vector3 displacement = currentVelocity * Time.deltaTime;
            Vector3 newPosition = transform.position + displacement;

            // 限制玩家位置
            newPosition.x = Mathf.Clamp(newPosition.x, 1f, mapSize.x);
            newPosition.z = Mathf.Clamp(newPosition.z, 1f, mapSize.y);
            newPosition.y = 0.5f; // 确保 Y = 0.5

            transform.position = newPosition;

            float distance = displacement.magnitude;
            if (ballRadius > 0)
            {
                float rotationAngle = (distance / (2 * Mathf.PI * ballRadius)) * 360f;
                Vector3 rotationAxis = Vector3.Cross(Vector3.up, lastMoveDirection).normalized;
                transform.Rotate(rotationAxis, rotationAngle, Space.World);
            }
        }
    }

    private void HandleKeyboardInput()
    {
        rawMoveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) rawMoveDirection += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) rawMoveDirection += Vector3.back;
        if (Input.GetKey(KeyCode.A)) rawMoveDirection += Vector3.left;
        if (Input.GetKey(KeyCode.D)) rawMoveDirection += Vector3.right;

        if (rawMoveDirection.magnitude > 1f)
        {
            rawMoveDirection.Normalize();
        }

        smoothedMoveDirection = Vector3.Lerp(smoothedMoveDirection, rawMoveDirection, Time.deltaTime * inputSmoothing);
    }

    private void SendMoveRequest(Vector3 moveDirection)
    {
        if (Time.time - lastSendTime < SEND_INTERVAL) return;
        lastSendTime = Time.time;
        Vector3Int position = new Vector3Int(
            Mathf.RoundToInt(transform.position.x * 1000),
            Mathf.RoundToInt(transform.position.y * 1000),
            Mathf.RoundToInt(transform.position.z * 1000)
        );
        NetworkMessageHandler.Instance.SendMoveRequest(playerId, stage, position);
        //Debug.Log($"发送移动请求: 玩家ID={playerId}, 位置={position}, 方向={moveDirection}");
    }

    private void SendStopRequest()
    {
        if (Time.time - lastSendTime < SEND_INTERVAL) return;
        lastSendTime = Time.time;
        Vector3Int position = new Vector3Int(
            Mathf.RoundToInt(transform.position.x * 1000),
            Mathf.RoundToInt(transform.position.y * 1000),
            Mathf.RoundToInt(transform.position.z * 1000)
        );
        NetworkMessageHandler.Instance.SendMoveRequest(playerId, stage, position);
        //Debug.Log($"发送刹车请求: 玩家ID={playerId}, 位置={position}");
    }

    private void SendItemCollected(string itemType, int spawnId)
    {
        BufferSegment itemTypeSegment = BinaryProtocol.EncodeString(itemType);
        BufferSegment spawnIdSegment = BinaryProtocol.EncodeInt32(spawnId);

        // 计算有效载荷长度
        int totalLength = itemTypeSegment.Count + spawnIdSegment.Count;
        byte[] buffer = BufferPool.Get(totalLength, true);
        int offset = 0;
        Array.Copy(itemTypeSegment.Data, itemTypeSegment.Offset, buffer, offset, itemTypeSegment.Count);
        offset += itemTypeSegment.Count;
        Array.Copy(spawnIdSegment.Data, spawnIdSegment.Offset, buffer, offset, spawnIdSegment.Count);

        // 创建不含消息头的 BufferSegment
        BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
        WebSocketManager.Instance.Send(WebSocketManager.MessageType.ItemCollected, payload);

        BufferPool.Release(itemTypeSegment.Data);
        BufferPool.Release(spawnIdSegment.Data);
        Debug.Log($"发送道具收集消息: itemType={itemType}, spawnId={spawnId}, payload={BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
    }

    public void UpdatePosition(Vector3 position, Vector3 velocity, Quaternion rotation)
    {
        transform.position = position;
        currentVelocity = velocity;
        transform.rotation = rotation;
        currentSpeed = velocity.magnitude;
    }

    public float GetCurrentSpeed() => currentSpeed;
    public bool IsMoving() => smoothedMoveDirection != Vector3.zero && currentSpeed > 0.1f;
    public Vector3 GetMoveDirection() => smoothedMoveDirection;

    public void StopMovement()
    {
        float speedReduction = brakeSpeed * Time.deltaTime * 60f;
        currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, speedReduction);
        smoothedMoveDirection = Vector3.zero;
        currentSpeed = currentVelocity.magnitude;

        if (currentSpeed < 0.01f)
        {
            currentVelocity = Vector3.zero;
            currentSpeed = 0f;
        }
    }

    public void AddKnockback(Vector3 direction, float force)
    {
        currentVelocity += direction.normalized * force;
    }

    public void SetMapSize(Vector2 newMapSize)
    {
        mapSize = newMapSize;
        Debug.Log($"玩家地图大小更新: {mapSize}");
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            currentVelocity = Vector3.zero;
            currentSpeed = 0f;
            Debug.Log("撞墙：停止移动");
            if (isLocalPlayer)
            {
                SendStopRequest();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer) return;

        if (other.CompareTag("Gem01"))
        {
            CollectItem(other.gameObject, "Gem01");
        }
        else if (other.CompareTag("Gem02"))
        {
            CollectItem(other.gameObject, "Gem02");
        }
        else if (other.CompareTag("Potion01"))
        {
            CollectItem(other.gameObject, "Potion01");
        }
        else if (other.CompareTag("Potion02"))
        {
            CollectItem(other.gameObject, "Potion02");
        }
        else if (other.CompareTag("Star01"))
        {
            CollectItem(other.gameObject, "Star01");
        }
        else if (other.CompareTag("Star02"))
        {
            CollectItem(other.gameObject, "Star02");
        }
    }

    private void CollectItem(GameObject item, string itemType)
    {
        OnItemCollected?.Invoke(itemType, item);
        Debug.Log($"收集到道具：{itemType}");
        if (ItemSpawner.Instance != null)
        {
            int spawnId = ItemSpawner.Instance.GetSpawnId(item);
            ItemSpawner.Instance.ReturnItemToPool(item);
            SendItemCollected(itemType, spawnId);
        }
        else
        {
            Destroy(item);
        }
    }
}