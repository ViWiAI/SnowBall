using Best.HTTP.Shared.PlatformSupport.Memory;
using Game.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Network
{
    public class NetworkMessageHandler : MonoBehaviour
    {
        public static NetworkMessageHandler Instance { get; private set; }

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
            WebSocketManager.Instance.OnMessageReceived += HandleServerMessage;
        }

        private void OnDestroy()
        {
            if (WebSocketManager.Instance != null)
            {
                WebSocketManager.Instance.OnMessageReceived -= HandleServerMessage;
            }
        }

        private void HandleServerMessage(WebSocketManager.MessageType msgType, byte[] payload)
        {
            try
            {
                switch (msgType)
                {
                    case WebSocketManager.MessageType.Connect:
                        HandleConnect(payload);
                        break;
                    case WebSocketManager.MessageType.PlayerLogin:
                        HandlePlayerLogin(payload);
                        break;
                    case WebSocketManager.MessageType.PlayerOnline:
                        HandlePlayerMove(payload);
                        break;
                    case WebSocketManager.MessageType.PlayerMove:
                        HandlePlayerMove(payload);
                        break;
                    case WebSocketManager.MessageType.Character:
                        HandleCharacter(payload);
                        break;
                    case WebSocketManager.MessageType.ItemCollected:
                        HandleItemCollected(payload);
                        break;
                    case WebSocketManager.MessageType.ItemSpawned:
                        HandleItemSpawned(payload);
                        break;
                    default:
                        Debug.LogWarning($"δ֪��Ϣ����: {msgType}");
                        break;

                    case WebSocketManager.MessageType.Error:
                        HandleError(payload);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"��Ϣ����ʧ��: {e.Message}");
            }
        }

        private void HandleCharacter(byte[] payload)
        {
            int offset = 0;
            try
            {
                int status = BinaryProtocol.DecodeStatus(payload, ref offset);
                if(status == 1)
                {
                    int playerId = BinaryProtocol.DecodeInt32(payload, ref offset);
                    string name = BinaryProtocol.DecodeString(payload, ref offset);
                    int lvl = BinaryProtocol.DecodeInt32(payload, ref offset);
                    int curHP = BinaryProtocol.DecodeInt32(payload, ref offset);
                    int maxHP = BinaryProtocol.DecodeInt32(payload, ref offset);
                    int curMP = BinaryProtocol.DecodeInt32(payload, ref offset);
                    int maxMP = BinaryProtocol.DecodeInt32(payload, ref offset);
                    Debug.Log($"��¼�ɹ��յ���ɫ��Ϣ: name={name}, lvl={lvl}, curHP={curHP}, maxHP={maxHP}, curMP={curMP}, maxMP={maxMP}");
                    UIManager.Instance.SetUserInfo(name, lvl, curHP, maxHP, curMP, maxMP);
                    UIManager.Instance.ShowChangeNameUI(false);
                }
                else if (status == 2)
                {
                    UIManager.Instance.ShowChangeNameUI(true);
                }
                
            }
            catch (Exception e)
            {
                Debug.LogWarning($"�����ɫ��Ϣʧ��: {e.Message}");
            }
        }

        private void HandlePlayerMove(byte[] payload)
        {
            int offset = 0;
            try
            {
                int playerId = BinaryProtocol.DecodeInt32(payload, ref offset);
                Debug.Log($"��Ҹ��� playerId: {playerId}");
                if (playerId == GameManager.Instance.PlayerId())
                {
                    Debug.Log($"��Ҹ���: �����Լ�");
                    return;
                }
                int mapId = BinaryProtocol.DecodeInt32(payload, ref offset);
                Vector3 position = new Vector3(
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset)
                );
                Vector3 velocity = new Vector3(
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset)
                );
                Quaternion rotation = new Quaternion(
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset)
                );

                GameObject playerObject = GameManager.Instance.GetPlayerObject(playerId);
                //if (playerObject == null)
                //{
                //    playerObject = GameManager.Instance.SpawnPlayer(playerId.ToString(), false, mapId, position);
                //}

                PlayerMovement playerMovement = playerObject.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.UpdatePosition(position, velocity, rotation);
                }
                Debug.Log($"��Ҹ���: playerId={playerId}, position={position}, velocity={velocity}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"������Ҹ���ʧ��: {e.Message}");
            }
        }

        private void HandleItemSpawned(byte[] payload)
        {
            int offset = 0;
            try
            {
                int spawnId = BinaryProtocol.DecodeInt32(payload, ref offset);
                string itemType = BinaryProtocol.DecodeString(payload, ref offset);
                Vector3 position = new Vector3(
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset),
                    BinaryProtocol.DecodeFloat(payload, ref offset)
                );
                ItemSpawner.Instance.SpawnItemFromServer(spawnId, itemType, position);
                Debug.Log($"�յ�����������Ϣ: spawnId={spawnId}, Type={itemType}, Position={position}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"�����������ʧ��: {e.Message}");
            }
        }


        private void HandleItemCollected(byte[] payload)
        {
            int offset = 0;
            try
            {
                string itemType = BinaryProtocol.DecodeString(payload, ref offset);
                int spawnId = BinaryProtocol.DecodeInt32(payload, ref offset);
                ItemSpawner.Instance.DisableItem(spawnId);
                Debug.Log($"�յ������ռ���Ϣ: Type={itemType}, spawnId={spawnId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"��������ռ�ʧ��: {e.Message}");
            }
        }


        public void SendItemSpawned(int itemId, string itemType, Vector3 position)
        {
            BufferSegment itemIdSegment = BinaryProtocol.EncodeInt32(itemId);
            BufferSegment itemTypeSegment = BinaryProtocol.EncodeString(itemType);
            BufferSegment positionSegment = BinaryProtocol.EncodeVector3(position);

            int totalLength = itemIdSegment.Count + itemTypeSegment.Count + positionSegment.Count;
            byte[] buffer = BufferPool.Get(totalLength, true);
            int offset = 0;
            Array.Copy(itemIdSegment.Data, itemIdSegment.Offset, buffer, offset, itemIdSegment.Count);
            offset += itemIdSegment.Count;
            Array.Copy(itemTypeSegment.Data, itemTypeSegment.Offset, buffer, offset, itemTypeSegment.Count);
            offset += itemTypeSegment.Count;
            Array.Copy(positionSegment.Data, positionSegment.Offset, buffer, offset, positionSegment.Count);

            BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
            WebSocketManager.Instance.Send(WebSocketManager.MessageType.ItemSpawned, payload);

            BufferPool.Release(itemIdSegment.Data);
            BufferPool.Release(itemTypeSegment.Data);
            BufferPool.Release(positionSegment.Data);
            Debug.Log($"���͵���������Ϣ: ID={itemId}, Type={itemType}, Position={position}");
        }

        private void HandleConnect(byte[] payload)
        {
            int offset = 0;
            try
            {
                string msg = BinaryProtocol.DecodeString(payload, ref offset);
                //UIManager.Instance.ShowTipsMessage($"���������ӳɹ�");
                Debug.Log($"�յ�������Ϣ: {msg}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HandleConnect ����ʧ��: {e.Message}, ����: {BitConverter.ToString(payload)}");
            }
        }

        private void HandlePlayerLogin(byte[] payload)
        {
            if (payload.Length < 1)
            {
                Debug.LogWarning("PlayerLogin payload ���Ȳ���");
                return;
            }
            byte status = payload[0];
            if (status == 0)
            {
                GameManager.Instance.SetLoginStatus(true);
                GameManager.Instance.SetStage(2);
                UIManager.Instance.ShowTipsMessage("��¼�ɹ�");
                UIManager.Instance.Close_Login();
                UIManager.Instance.ShowLobby(true);
            }
            else if (status == 1)
            {
                UIManager.Instance.ShowErrorMessage("�˺��ѱ�����������ϵ�ͷ�");
            }
        }



        private void HandleError(byte[] payload)
        {
            int offset = 0;
            string errorMessage = BinaryProtocol.DecodeString(payload, ref offset);
            Debug.LogWarning($"����������: {errorMessage}");
            UIManager.Instance.ShowErrorMessage(errorMessage);
        }


        public void SendCharacterCreate(string name, string account)
        {
            Debug.Log($"SendLoginRequest: name={name}, account={account}");
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(account))
            {
                Debug.LogError("name or account is empty");
                return;
            }

            BufferSegment nameSegment = BinaryProtocol.EncodeString(name);
            BufferSegment accountSegment = BinaryProtocol.EncodeString(account);
            Debug.Log($"usernameSegment: {BitConverter.ToString(nameSegment.Data, nameSegment.Offset, nameSegment.Count)}");
            Debug.Log($"passwordSegment: {BitConverter.ToString(accountSegment.Data, accountSegment.Offset, accountSegment.Count)}");

            int payloadLength = nameSegment.Count + accountSegment.Count;
            int totalLength = 5 + payloadLength;
            byte[] buffer = BufferPool.Get(totalLength, true);
            Array.Clear(buffer, 0, totalLength); // Explicitly clear buffer

            buffer[0] = (byte)WebSocketManager.MessageType.CharacterCreate;
            byte[] lengthBytes = new byte[4];
            lengthBytes[0] = (byte)(payloadLength >> 24);
            lengthBytes[1] = (byte)(payloadLength >> 16);
            lengthBytes[2] = (byte)(payloadLength >> 8);
            lengthBytes[3] = (byte)payloadLength;

            Array.Copy(lengthBytes, 0, buffer, 1, 4);

            int offset = 5;
            Array.Copy(nameSegment.Data, nameSegment.Offset, buffer, offset, nameSegment.Count);
            offset += nameSegment.Count;
            Array.Copy(accountSegment.Data, accountSegment.Offset, buffer, offset, accountSegment.Count);

            BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
            Debug.Log($"SendLoginRequest payload: {BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
            WebSocketManager.Instance.Send(WebSocketManager.MessageType.CharacterCreate, payload);

            BufferPool.Release(nameSegment.Data);
            BufferPool.Release(accountSegment.Data);
        }


        // ���ͷ�������¼����
        public void SendLoginRequest(string username, string password)
        {
            Debug.Log($"SendLoginRequest: username={username}, password={password}");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Debug.LogError("Username or password is empty");
                return;
            }

            BufferSegment usernameSegment = BinaryProtocol.EncodeString(username);
            BufferSegment passwordSegment = BinaryProtocol.EncodeString(password);
            Debug.Log($"usernameSegment: {BitConverter.ToString(usernameSegment.Data, usernameSegment.Offset, usernameSegment.Count)}");
            Debug.Log($"passwordSegment: {BitConverter.ToString(passwordSegment.Data, passwordSegment.Offset, passwordSegment.Count)}");

            int payloadLength = usernameSegment.Count + passwordSegment.Count;
            int totalLength = 5 + payloadLength;
            byte[] buffer = BufferPool.Get(totalLength, true);
            Array.Clear(buffer, 0, totalLength); // Explicitly clear buffer

            buffer[0] = (byte)WebSocketManager.MessageType.PlayerLogin;
            byte[] lengthBytes = new byte[4];
            lengthBytes[0] = (byte)(payloadLength >> 24);
            lengthBytes[1] = (byte)(payloadLength >> 16);
            lengthBytes[2] = (byte)(payloadLength >> 8);
            lengthBytes[3] = (byte)payloadLength;

            Array.Copy(lengthBytes, 0, buffer, 1, 4);

            int offset = 5;
            Array.Copy(usernameSegment.Data, usernameSegment.Offset, buffer, offset, usernameSegment.Count);
            offset += usernameSegment.Count;
            Array.Copy(passwordSegment.Data, passwordSegment.Offset, buffer, offset, passwordSegment.Count);

            BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
            Debug.Log($"SendLoginRequest payload: {BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
            WebSocketManager.Instance.Send(WebSocketManager.MessageType.PlayerLogin, payload);

            // Release username and password buffers
            BufferPool.Release(usernameSegment.Data);
            BufferPool.Release(passwordSegment.Data);
            // Note: Do not release 'buffer' here, as it��s passed to SendInternal and managed there
        }

        // ���ͷ�������������
        public void SendPlayerOnlineRequest(int playerId, string stage, Vector3Int position)
        {
            BufferSegment playerIdSegment = BinaryProtocol.EncodeInt32(playerId);
            BufferSegment mapIdSegment = BinaryProtocol.EncodeString(stage);
            BufferSegment positionSegment = BinaryProtocol.EncodePosition(position);

            int totalLength = playerIdSegment.Count + mapIdSegment.Count + positionSegment.Count;
            byte[] buffer = BufferPool.Get(totalLength, true);
            int offset = 0;
            Array.Copy(playerIdSegment.Data, playerIdSegment.Offset, buffer, offset, playerIdSegment.Count);
            offset += playerIdSegment.Count;
            Array.Copy(mapIdSegment.Data, mapIdSegment.Offset, buffer, offset, mapIdSegment.Count);
            offset += mapIdSegment.Count;
            Array.Copy(positionSegment.Data, positionSegment.Offset, buffer, offset, positionSegment.Count);

            BufferSegment payload = new BufferSegment(buffer, 0, totalLength);
            WebSocketManager.Instance.Send(WebSocketManager.MessageType.PlayerOnline, payload);

            BufferPool.Release(playerIdSegment.Data);
            BufferPool.Release(mapIdSegment.Data);
            BufferPool.Release(positionSegment.Data);
            Debug.Log($"SendPlayerOnlineRequest payload: {BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
        }

        // ���ͷ������ƶ�����
        public void SendMoveRequest(int playerId, int stageId, Vector3Int position)
        {
            BufferSegment playerIdSegment = BinaryProtocol.EncodeInt32(playerId);
            BufferSegment stageIdSegment = BinaryProtocol.EncodeInt32(stageId);
            BufferSegment positionSegment = BinaryProtocol.EncodePosition(position);

            // ������Ч�غɳ���
            int payloadLength = playerIdSegment.Count + stageIdSegment.Count + positionSegment.Count;
            byte[] buffer = BufferPool.Get(payloadLength, true);
            int offset = 0;
            Array.Copy(playerIdSegment.Data, playerIdSegment.Offset, buffer, offset, playerIdSegment.Count);
            offset += playerIdSegment.Count;
            Array.Copy(stageIdSegment.Data, stageIdSegment.Offset, buffer, offset, stageIdSegment.Count);
            offset += stageIdSegment.Count;
            Array.Copy(positionSegment.Data, positionSegment.Offset, buffer, offset, positionSegment.Count);

            // ����������Ϣͷ�� BufferSegment
            BufferSegment payload = new BufferSegment(buffer, 0, payloadLength);
            WebSocketManager.Instance.Send(WebSocketManager.MessageType.PlayerMove, payload);

            // ���ͷ���ʱ BufferSegment �� Data
            BufferPool.Release(playerIdSegment.Data);
            BufferPool.Release(stageIdSegment.Data);
            BufferPool.Release(positionSegment.Data);
            // ���ͷ� buffer������ SendInternal ����
            //Debug.Log($"SendMoveRequest: playerId={playerId}, stageId={stageId}, position=({position.x}, {position.y}, {position.z}), payload={BitConverter.ToString(payload.Data, payload.Offset, payload.Count)}");
        }

    }
}