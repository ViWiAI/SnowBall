using Best.HTTP.Shared.PlatformSupport.Memory;
using Game.Network;
using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("�˶�����")]
    public float moveSpeed = 10f;
    public float ballRadius = 0.5f;
    public float drag = 50f;
    public float brakeSpeed = 0.01f;
    public float inputSmoothing = 10f;
    [SerializeField] private Vector2 mapSize = new Vector2(1000f, 1000f);
    private int scaleFactor = 1000;

    private Vector3 rawMoveDirection;
    private Vector3 smoothedMoveDirection;
    private Vector3 currentVelocity;
    private float currentSpeed;
    private Vector3 lastMoveDirection;
    private ItemSpawner itemSpawner;
    private bool isLocalPlayer;
    private int playerId;
    private int stage;
    private float lastSendTime;
    private const float SEND_INTERVAL = 0.1f;
    private MeshRenderer meshRenderer;
    private Transform localPlayerTransform;
    private float renderDistance = 50f; // ��Ⱦ����

    // ���������ڲ�ֵ��Ŀ��״̬��Զ����ң�
    private Vector3 targetPosition;
    private Vector3 targetVelocity;
    private Quaternion targetRotation;
    private int targetScaleFactor;
    private float lerpSpeed = 10f; // ��ֵ�ٶ�

    // ����������͵�����ʱ���������reconciliation��
    private float lastInputTime;

    public float CurrentSpeed => currentSpeed;
    public int PlayerId => playerId;
    public bool IsLocalPlayer => isLocalPlayer;
    public int ScaleFactor => scaleFactor;

    public static event Action<string, GameObject> OnItemCollected;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationY;
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        itemSpawner = FindObjectOfType<ItemSpawner>();
        if (itemSpawner == null)
        {
            Debug.LogError("δ�ҵ� ItemSpawner �����");
        }

        Application.targetFrameRate = 60;
        UpdateScale();

        // ��ȡ�������
        localPlayerTransform = GameManager.Instance.GetPlayerObject(GameManager.Instance.PlayerId())?.transform;

        targetPosition = transform.position;
        targetVelocity = currentVelocity;
        targetRotation = transform.rotation;
        targetScaleFactor = scaleFactor;
    }

    void FixedUpdate()  // ��ΪFixedUpdate��ȷ��ȷ����
    {
        // ��̬������Ⱦ
        if (!isLocalPlayer && meshRenderer != null && localPlayerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, localPlayerTransform.position);
            meshRenderer.enabled = distance < renderDistance;
        }

        if (!isLocalPlayer)
        {
            // Զ����ң���ֵƽ��
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * lerpSpeed);
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * lerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * lerpSpeed);
            scaleFactor = (int)Mathf.Lerp(scaleFactor, targetScaleFactor, Time.fixedDeltaTime * lerpSpeed);
            UpdateScale();
            currentSpeed = currentVelocity.magnitude;
            return;
        }

        // ������ң����������Ԥ��
        HandleKeyboardInput();  // �Ƴ�Update����������

        if (Input.GetKey(KeyCode.Space))
        {
            StopMovement();
#if UNITY_EDITOR
            Debug.Log($"ɲ���У���ǰ�ٶ� = {currentVelocity.magnitude:F2}");
#endif
            SendInputRequest(Vector3.zero);  // ����ɲ������
        }
        else
        {
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
                currentVelocity = Vector3.Lerp(currentVelocity, moveDirection * moveSpeed, Time.fixedDeltaTime * 0.5f);
                lastMoveDirection = moveDirection;
            }
            else
            {
                StopMovement();
            }

            if (currentSpeed > 0.01f)
            {
                SendInputRequest(lastMoveDirection);  // �����������λ��
            }
        }

        currentSpeed = currentVelocity.magnitude;

        if (currentSpeed > 0.01f)
        {
            Vector3 displacement = currentVelocity * Time.fixedDeltaTime;
            Vector3 newPosition = transform.position + displacement;

            newPosition.x = Mathf.Clamp(newPosition.x, 1f, mapSize.x);
            newPosition.z = Mathf.Clamp(newPosition.z, 1f, mapSize.y);
            newPosition.y = 0.5f;

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

        smoothedMoveDirection = Vector3.Lerp(smoothedMoveDirection, rawMoveDirection, Time.fixedDeltaTime * inputSmoothing);
    }

    private void SendInputRequest(Vector3 inputDirection)
    {
        if (Time.time - lastSendTime < SEND_INTERVAL) return;
        lastSendTime = Time.time;
        lastInputTime = Time.time;  // ��¼ʱ���������reconciliation

        // �������뷽��ʱ����ȣ����޸�NetworkMessageHandler��
        NetworkMessageHandler.Instance.SendInputRequest(playerId, stage, inputDirection, lastInputTime, scaleFactor);
#if UNITY_EDITOR
        Debug.Log($"������������: ���ID={playerId}, ����={inputDirection}, ʱ��={lastInputTime}, ���ű���={scaleFactor}");
#endif
    }

    private void SendItemCollected(string itemType, int spawnId)
    {
        BufferSegment itemTypeSegment = BinaryProtocol.EncodeString(itemType);
        BufferSegment spawnIdSegment = BinaryProtocol.EncodeInt32(spawnId);

        int totalLength = itemTypeSegment.Count + spawnIdSegment.Count;
        byte[] buffer = BufferPool.Get(totalLength, true);
        int offset = 0;
        Array.Copy(itemTypeSegment.Data, itemTypeSegment.Offset, buffer, offset, itemTypeSegment.Count);
        offset += itemTypeSegment.Count;
        Array.Copy(spawnIdSegment.Data, spawnIdSegment.Offset, buffer, offset, spawnIdSegment.Count);

        BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
        WebSocketManager.Instance.Send(WebSocketManager.MessageType.ItemCollected, payload);

        BufferPool.Release(itemTypeSegment.Data);
        BufferPool.Release(spawnIdSegment.Data);
#if UNITY_EDITOR
        Debug.Log($"���͵����ռ���Ϣ: itemType={itemType}, spawnId={spawnId}, payload={BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
#endif
    }

    public void Initialize(int playerId, bool isLocal, int stage)
    {
        this.playerId = playerId;
        this.isLocalPlayer = isLocal;
        this.stage = stage;
    }

    private void UpdateScale()
    {
        float scale = scaleFactor / 1000f;
        transform.localScale = new Vector3(scale, scale, scale);
#if UNITY_EDITOR
        //Debug.Log($"�����������: playerId={playerId}, scaleFactor={scaleFactor}, scale={scale}");
#endif
    }

    // ����������㲥Ȩ��״̬ʱ���ô˷���
    public void UpdatePosition(Vector3 position, Vector3 velocity, Quaternion rotation, int newScaleFactor, float serverTime)
    {
        if (isLocalPlayer)
        {
            // ���أ�reconciliation - ���������ʱ�� > ������룬�������طű�������
            if (serverTime > lastInputTime)
            {
                transform.position = position;
                currentVelocity = velocity;
                transform.rotation = rotation;
                scaleFactor = newScaleFactor;
                UpdateScale();
                // �طű������루�򻯰棺��serverTime�����ڣ���ģ�⣩
                float delta = Time.time - serverTime;
                Vector3 displacement = currentVelocity * delta;
                transform.position += displacement;
                // ... �����ת�ط������Ҫ
                float distance = displacement.magnitude;
                if (ballRadius > 0)
                {
                    float rotationAngle = (distance / (2 * Mathf.PI * ballRadius)) * 360f;
                    Vector3 rotationAxis = Vector3.Cross(Vector3.up, lastMoveDirection).normalized;
                    transform.Rotate(rotationAxis, rotationAngle, Space.World);
                }
            }
        }
        else
        {
            // Զ�̣�����Ŀ�����ڲ�ֵ
            targetPosition = position;
            targetVelocity = velocity;
            targetRotation = rotation;
            targetScaleFactor = newScaleFactor;
        }

        currentSpeed = currentVelocity.magnitude;
    }

    public float GetCurrentSpeed() => currentSpeed;
    public bool IsMoving() => smoothedMoveDirection != Vector3.zero && currentSpeed > 0.1f;
    public Vector3 GetMoveDirection() => smoothedMoveDirection;

    public void StopMovement()
    {
        float speedReduction = brakeSpeed * Time.fixedDeltaTime * 60f;
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
#if UNITY_EDITOR
        Debug.Log($"��ҵ�ͼ��С����: {mapSize}");
#endif
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            currentVelocity = Vector3.zero;
            currentSpeed = 0f;
#if UNITY_EDITOR
            Debug.Log("ײǽ��ֹͣ�ƶ�");
#endif
            if (isLocalPlayer)
            {
                SendInputRequest(Vector3.zero);
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
#if UNITY_EDITOR
        Debug.Log($"�ռ������ߣ�{itemType}");
#endif
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