using Best.HTTP.Shared.PlatformSupport.Memory;
using Game.Network;
using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("�˶�����")]
    public float moveSpeed = 3f; // �ƶ��ٶȣ���/�룩
    public float ballRadius = 0.5f; // ѩ��뾶���ף�
    public float drag = 50f; // ģ������
    public float brakeSpeed = 0.01f; // ɲ��ʱÿ֡���ٵ��ٶ�
    public float inputSmoothing = 10f; // ����ƽ��ϵ��
    [SerializeField] private Vector2 mapSize = new Vector2(1000f, 1000f); // ��ͼ�ߴ�

    private Vector3 rawMoveDirection; // ԭʼ���뷽��
    private Vector3 smoothedMoveDirection; // ƽ�����뷽��
    private Vector3 currentVelocity;
    private float currentSpeed;
    private Vector3 lastMoveDirection;
    private ItemSpawner itemSpawner; // ���� ItemSpawner
    private bool isLocalPlayer; // �Ƿ�Ϊ�������
    private int playerId; // ���ΨһID
    private int stage; // ��ͼID
    private float lastSendTime;
    private const float SEND_INTERVAL = 0.1f;

    public float CurrentSpeed => currentSpeed;
    public int PlayerId => playerId;
    public bool IsLocalPlayer => isLocalPlayer;

    public static event Action<string, GameObject> OnItemCollected;

    void Start()
    {
        // ��ʼ�� Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationY;
        }

        // ��ʼ�� MeshRenderer
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ���� ItemSpawner
        itemSpawner = FindObjectOfType<ItemSpawner>();
        if (itemSpawner == null)
        {
            Debug.LogError("δ�ҵ� ItemSpawner �����");
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
        if (!isLocalPlayer) return; // Զ������ɷ���������

        if (Input.GetKey(KeyCode.Space))
        {
            StopMovement();
            Debug.Log($"ɲ���У���ǰ�ٶ� = {currentVelocity.magnitude:F2}");
            SendStopRequest();
        }
        else
        {
            // �����������
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

            // �����ƶ����󣨽��������������ƶ�ʱ��
            if (smoothedMoveDirection != Vector3.zero && currentSpeed > 0.01f)
            {
                SendMoveRequest(moveDirection);
            }
        }

        currentSpeed = currentVelocity.magnitude;

        // ��������ƶ�
        if (currentSpeed > 0.01f)
        {
            Vector3 displacement = currentVelocity * Time.deltaTime;
            Vector3 newPosition = transform.position + displacement;

            // �������λ��
            newPosition.x = Mathf.Clamp(newPosition.x, 1f, mapSize.x);
            newPosition.z = Mathf.Clamp(newPosition.z, 1f, mapSize.y);
            newPosition.y = 0.5f; // ȷ�� Y = 0.5

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
        //Debug.Log($"�����ƶ�����: ���ID={playerId}, λ��={position}, ����={moveDirection}");
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
        //Debug.Log($"����ɲ������: ���ID={playerId}, λ��={position}");
    }

    private void SendItemCollected(string itemType, int spawnId)
    {
        BufferSegment itemTypeSegment = BinaryProtocol.EncodeString(itemType);
        BufferSegment spawnIdSegment = BinaryProtocol.EncodeInt32(spawnId);

        // ������Ч�غɳ���
        int totalLength = itemTypeSegment.Count + spawnIdSegment.Count;
        byte[] buffer = BufferPool.Get(totalLength, true);
        int offset = 0;
        Array.Copy(itemTypeSegment.Data, itemTypeSegment.Offset, buffer, offset, itemTypeSegment.Count);
        offset += itemTypeSegment.Count;
        Array.Copy(spawnIdSegment.Data, spawnIdSegment.Offset, buffer, offset, spawnIdSegment.Count);

        // ����������Ϣͷ�� BufferSegment
        BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
        WebSocketManager.Instance.Send(WebSocketManager.MessageType.ItemCollected, payload);

        BufferPool.Release(itemTypeSegment.Data);
        BufferPool.Release(spawnIdSegment.Data);
        Debug.Log($"���͵����ռ���Ϣ: itemType={itemType}, spawnId={spawnId}, payload={BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
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
        Debug.Log($"��ҵ�ͼ��С����: {mapSize}");
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            currentVelocity = Vector3.zero;
            currentSpeed = 0f;
            Debug.Log("ײǽ��ֹͣ�ƶ�");
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
        Debug.Log($"�ռ������ߣ�{itemType}");
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