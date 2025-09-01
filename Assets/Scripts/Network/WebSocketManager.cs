using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.WebSockets;
using Best.WebSockets.Implementations;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Network
{
    public class WebSocketManager : MonoBehaviour
    {
        public static WebSocketManager Instance { get; private set; }

        private WebSocket ws;
        private bool isConnecting;
        private readonly Queue<(MessageType, BufferSegment)> messageQueue = new Queue<(MessageType, BufferSegment)>();
        private readonly object queueLock = new object(); // Added for thread safety
        private const float RECONNECT_INTERVAL = 5f;
        private const float CONNECTION_CHECK_INTERVAL = 2f;
        private DateTime lastConnectionAttemptTime = DateTime.MinValue;
        private bool isManualDisconnect;

        public Action<MessageType, byte[]> OnMessageReceived;

        public enum MessageType : byte
        {
            Connect = 0,
            PlayerLogin = 1,
            PlayerOnline = 2,
            PlayerMove = 3,
            Character = 4,
            CharacterCreate = 5,
            ItemCollected = 6,
            ItemSpawned = 7,
            PlayerList = 8,
            Ping = 9,
            Pong = 10,
            PlayerOffline = 11,
            Error = 255
        }

        public bool IsConnected => ws != null && ws.IsOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (UnityMainThreadDispatcher.Instance() == null)
            {
                Debug.LogError("UnityMainThreadDispatcher 未初始化");
            }
            StartCoroutine(ConnectionMonitor());
        }

        private IEnumerator ConnectionMonitor()
        {
            while (true)
            {
                yield return new WaitForSeconds(CONNECTION_CHECK_INTERVAL);

                if (isManualDisconnect) continue;

                if (!IsConnected && !isConnecting &&
                    (DateTime.Now - lastConnectionAttemptTime).TotalSeconds >= RECONNECT_INTERVAL)
                {
                    StartCoroutine(Connect());
                }
            }
        }

        private IEnumerator Connect()
        {
            if (isConnecting) yield break;

            isConnecting = true;
            lastConnectionAttemptTime = DateTime.Now;

            if (ws != null)
            {
                ws.OnOpen -= OnWebSocketOpen;
                ws.OnBinary -= OnWebSocketBinary;
                ws.OnClosed -= OnWebSocketClosed;
                ws.Close();
                ws = null;
            }

            Debug.Log("正在连接WebSocket服务器...");
            ws = new WebSocket(new Uri("ws://124.156.203.23:7272"));

            ws.OnOpen += OnWebSocketOpen;
            ws.OnBinary += OnWebSocketBinary;
            ws.OnClosed += OnWebSocketClosed;

            ws.Open();

            float timeout = 10f;
            float elapsed = 0f;
            while (isConnecting && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeout)
            {
                Debug.LogWarning("WebSocket连接超时");
                if (ws != null)
                {
                    ws.Close();
                    ws = null;
                }
            }

            isConnecting = false;
        }

        private void OnWebSocketOpen(WebSocket webSocket)
        {
            Debug.Log("WebSocket连接成功");
            isConnecting = false;
            isManualDisconnect = false;

            lock (queueLock)
            {
                while (messageQueue.Count > 0)
                {
                    var (msgType, message) = messageQueue.Dequeue();
                    SendInternal(msgType, message);
                }
            }
        }

        private void OnWebSocketBinary(WebSocket webSocket, BufferSegment buffer)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                try
                {
                    if (buffer.Count < 5)
                    {
                        Debug.LogWarning($"收到无效消息，长度不足: {buffer.Count}");
                        return;
                    }

                    byte[] data = new byte[buffer.Count];
                    Array.Copy(buffer.Data, buffer.Offset, data, 0, buffer.Count);

                    MessageType msgType = (MessageType)data[0];
                    int payloadLength = BitConverter.ToInt32(data, 1);
                    if (BitConverter.IsLittleEndian)
                    {
                        payloadLength = System.Net.IPAddress.NetworkToHostOrder(payloadLength);
                    }

                    if (payloadLength > data.Length - 5)
                    {
                        //Debug.LogWarning($"Payload长度不匹配: 期望{payloadLength}, 实际{data.Length - 5}");
                        return;
                    }
                    //Debug.Log($"OnWebSocketBinary： msgType={msgType}, data={BitConverter.ToString(buffer.Data, buffer.Offset, buffer.Count)}");
                    byte[] payload = new byte[payloadLength];
                    Array.Copy(data, 5, payload, 0, payloadLength);
                    OnMessageReceived?.Invoke(msgType, payload);
                }
                catch (Exception e)
                {
                    Debug.Log($"二进制消息解析失败: {e.Message}");
                }
            });
        }

        private void OnWebSocketClosed(WebSocket webSocket, WebSocketStatusCodes code, string message)
        {
            Debug.Log($"WebSocket关闭: {message} (Code: {code})");
            isConnecting = false;

            if (ws != null)
            {
                ws.OnOpen -= OnWebSocketOpen;
                ws.OnBinary -= OnWebSocketBinary;
                ws.OnClosed -= OnWebSocketClosed;
                ws = null;
            }

            if (!isManualDisconnect && code != WebSocketStatusCodes.NormalClosure)
            {
                Debug.LogWarning($"WebSocket异常关闭，将尝试重连: Code={code}, Message={message}");
            }
        }

        public void Send(MessageType msgType, BufferSegment message)
        {
            lock (queueLock)
            {
                //Debug.Log($"WebSocketManager.Send: msgType={msgType}, data={BitConverter.ToString(message.Data, message.Offset, message.Count)}");
                if (IsConnected)
                {
                    SendInternal(msgType, message);
                }
                else
                {
                    messageQueue.Enqueue((msgType, message));
                    Debug.Log($"WebSocket未连接，消息已加入队列: msgType={msgType}, data={BitConverter.ToString(message.Data, message.Offset, message.Count)}");
                }
            }
        }

        private void SendInternal(MessageType msgType, BufferSegment message)
        {
            try
            {
                // Check if message already contains a valid header (msgType + 4-byte length)
                if (message.Count >= 5 && message.Data[message.Offset] == (byte)msgType)
                {
                    // Directly send the message without adding a header
                    Debug.Log($"SendInternal: Direct send, msgType={msgType}, data={BitConverter.ToString(message.Data, message.Offset, message.Count)}");
                    ws.SendAsBinary(message);
                }
                else
                {
                    // Construct new message: 1-byte msgType + 4-byte length + payload
                    int totalLength = 5 + message.Count;
                    byte[] buffer = BufferPool.Get(totalLength, true);
                    Array.Clear(buffer, 0, totalLength); // Explicitly clear buffer
                    buffer[0] = (byte)msgType;
                    byte[] lengthBytes = BitConverter.GetBytes(message.Count);
                    if (BitConverter.IsLittleEndian)
                    {
                        lengthBytes = lengthBytes.Reverse().ToArray();
                    }
                    Array.Copy(lengthBytes, 0, buffer, 1, 4);
                    Array.Copy(message.Data, message.Offset, buffer, 5, message.Count);

                    BufferSegment newSegment = new BufferSegment(buffer, 0, totalLength);
                    //Debug.Log($"SendInternal: Constructed new message, msgType={msgType}, data={BitConverter.ToString(buffer, 0, totalLength)}");
                    ws.SendAsBinary(newSegment);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"发送消息失败: {e.Message}");
                lock (queueLock)
                {
                    messageQueue.Enqueue((msgType, message));
                }
                if (ws != null)
                {
                    ws.Close();
                    ws = null;
                }
            }
        }

        public void Disconnect()
        {
            isManualDisconnect = true;
            if (ws != null)
            {
                ws.Close();
                ws = null;
            }
            lock (queueLock)
            {
                messageQueue.Clear();
            }
        }

        public void Reconnect()
        {
            isManualDisconnect = false;
            if (!IsConnected && !isConnecting)
            {
                StartCoroutine(Connect());
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}