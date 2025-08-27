using Best.HTTP.Shared.PlatformSupport.Memory;
using Game.Data;
using Game.Managers;
using Game.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Managers
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        //��¼UI
        [SerializeField] private GameObject LoginUI;

        //��¼UI ��ť
        [SerializeField] private Button buttonLogin;
        [SerializeField] private Button buttonSignup;
        [SerializeField] private Button buttonPwd;

        [SerializeField] private Button characterCreate;
        [SerializeField] private Button stageButton;
        [SerializeField] private Button stageBackButton;
        [SerializeField] private Button startGameButton;

        //Lobby
        [SerializeField] private GameObject LobbyUI;
        [SerializeField] private GameObject ChangeNameUI;
        [SerializeField] private GameObject StageUI;

        [SerializeField] private TextMeshProUGUI HP;
        [SerializeField] private TextMeshProUGUI LVL;
        [SerializeField] private TextMeshProUGUI chaName;

        [SerializeField] private Slider SliderHP;
        [SerializeField] private Slider SliderMP;

        [SerializeField] private TMP_InputField username;
        [SerializeField] private TMP_InputField password;

        [SerializeField] private TMP_InputField characterName;

        [SerializeField] private GameObject errorMessage; // �� GameObject������ TextMeshProUGUI
        [SerializeField] private GameObject tipsMessage;


        private TextMeshProUGUI errorText; // TextMeshProUGUI ���
        private TextMeshProUGUI tipsText; // TextMeshProUGUI ���
        private string signupUrl = "https://www.baidu.com";
        private string forgotPwdUrl = "https://www.baidu.com";

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
            // ��ȡ errorMessage �� TextMeshProUGUI �����λ���Ӷ����������
            if (errorMessage != null)
            {
                errorText = errorMessage.GetComponentInChildren<TextMeshProUGUI>();
                if (errorText == null)
                {
                    Debug.LogError("UIManager: errorMessage ��δ�ҵ� TextMeshProUGUI �����");
                }
                else
                {
                    errorMessage.SetActive(false); // ��ʼ������
                }
            }

            // ��ȡ tipsMessage �� TextMeshProUGUI �����λ���Ӷ����������
            if (tipsMessage != null)
            {
                tipsText = tipsMessage.GetComponentInChildren<TextMeshProUGUI>();
                if (tipsText == null)
                {
                    Debug.LogError("UIManager: tipsMessage ��δ�ҵ� TextMeshProUGUI �����");
                }
                else
                {
                    tipsMessage.SetActive(false); // ��ʼ������
                }
            }


            // �󶨰�ť����¼�
            buttonLogin.onClick.AddListener(Click_Login);
            buttonSignup.onClick.AddListener(Click_Signup);
            buttonPwd.onClick.AddListener(Click_Pwd);

            characterCreate.onClick.AddListener(Click_CharacterCreate);
            stageButton.onClick.AddListener(Click_Stage);
            stageBackButton.onClick.AddListener(Click_StageBack);
            startGameButton.onClick.AddListener(Click_StartGame);
        }

        public void Click_StartGame()
        {
            GameManager.Instance.SetOnline(true);
            GameManager.Instance.SetPlayerPrefab(Resources.Load<GameObject>("Prefabs/Player"));
            GameManager.Instance.EnterStage();
        }

        public void Click_StageBack()
        {
            StageUI.SetActive(false);
        }

        public void Click_Stage()
        {
            StageUI.SetActive(true);
        }

        private void Click_CharacterCreate()
        {
            // ��ȡ�������ı�
            string nameText = characterName.text;

            // ��֤�����Ƿ���Ч
            if (string.IsNullOrEmpty(nameText))
            {
                ShowErrorMessage("��ɫ���Ʋ���Ϊ�գ�");
                return;
            }

            // ֪ͨ�������û���¼
            if (WebSocketManager.Instance.IsConnected)
            {
                NetworkMessageHandler.Instance.SendCharacterCreate(nameText,GameManager.Instance.GetLoginAccount());
                Debug.Log($"������ҽ�ɫ������Ϣ: name: {nameText}");
                // ������������첽���ؽ����������ʾ�� WebSocket �ص��д���
            }
            else
            {
                ShowErrorMessage("����δ���ӣ��������磡");
            }
        }

        private void Click_Login()
        {
            // ��ȡ�������ı�
            string usernameText = username.text;
            string passwordText = password.text;

            // ��֤�����Ƿ���Ч
            if (string.IsNullOrEmpty(usernameText) || string.IsNullOrEmpty(passwordText))
            {
                ShowErrorMessage("�û��������벻��Ϊ�գ�");
                return;
            }

            // ֪ͨ�������û���¼
            if (WebSocketManager.Instance.IsConnected)
            {
                NetworkMessageHandler.Instance.SendLoginRequest(usernameText, passwordText);
                GameManager.Instance.SetLoginAccount(usernameText);
                Debug.Log($"������ҵ�¼��Ϣ: username: {usernameText}, password: {passwordText}");
                // ������������첽���ؽ����������ʾ�� WebSocket �ص��д���
            }
            else
            {
                ShowErrorMessage("����δ���ӣ��������磡");
            }
        }

        // ��������ʾ������Ϣ��5�������
        public void ShowErrorMessage(string message)
        {
            if (errorText == null || errorMessage == null)
            {
                Debug.LogError("UIManager: errorMessage �� errorText δ��ȷ��ʼ����");
                return;
            }

            // ȷ���ı�����Ϊ UTF-8��ͨ�������ֶ�ת����Unity Ĭ��֧�֣�
            errorText.text = message;
            errorMessage.SetActive(true);
            StartCoroutine(HideErrorMessageAfterDelay(5f));

            // ���ԣ�����Ƿ������֧�ֵ��ַ�
            foreach (char c in message)
            {
                if (!errorText.font.HasCharacter(c))
                {
                    Debug.LogWarning($"���� {errorText.font.name} ��֧���ַ�: {c} (Unicode: \\u{(int)c:X4})");
                }
            }
        }

        // ��������ʾ��Ϣ��5�������
        public void ShowTipsMessage(string message)
        {
            if (tipsText == null || tipsMessage == null)
            {
                Debug.LogError("UIManager: tipsMessage �� tipsText δ��ȷ��ʼ����");
                return;
            }

            // ȷ���ı�����Ϊ UTF-8��ͨ�������ֶ�ת����Unity Ĭ��֧�֣�
            tipsText.text = message;
            tipsMessage.SetActive(true);
            StartCoroutine(HideTipsMessageAfterDelay(5f));

            // ���ԣ�����Ƿ������֧�ֵ��ַ�
            foreach (char c in message)
            {
                if (!tipsText.font.HasCharacter(c))
                {
                    Debug.LogWarning($"���� {tipsText.font.name} ��֧���ַ�: {c} (Unicode: \\u{(int)c:X4})");
                }
            }
        }

        private IEnumerator HideErrorMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (errorMessage != null)
            {
                errorMessage.SetActive(false);
            }
        }

        private IEnumerator HideTipsMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (tipsMessage != null)
            {
                tipsMessage.SetActive(false);
            }
        }

        private void Click_Pwd()
        {
            Application.OpenURL(forgotPwdUrl);
        }


        private void Click_Signup()
        {
            Application.OpenURL(signupUrl);
        }

        public void Close_Login()
        {
            LoginUI.SetActive(false);
            if (errorMessage != null)
            {
                errorMessage.SetActive(false); // �رյ�¼����ʱ���ش�����Ϣ
            }
        } 
        
        public void ShowLobby(bool show)
        {
            LobbyUI.SetActive(show);
        }

        public void ShowChangeNameUI(bool show)
        {
            ChangeNameUI.SetActive(show);
        }

        public void SetUserInfo(string name,int lvl,int curHP,int maxHP,int curMP,int maxMP)
        {
            chaName.text = name;
            HP.text = curHP+ "/" + maxHP;
            LVL.text = lvl.ToString();
            SliderHP.minValue = 0f;
            SliderHP.maxValue = maxHP;
            SliderMP.minValue = 0f;
            SliderMP.maxValue = maxMP;
            SliderHP.value = curHP;
            SliderMP.value = curMP;
        }

        private void OnDestroy()
        {
            if (buttonLogin != null) buttonLogin.onClick.RemoveListener(Click_Login);
            if (buttonSignup != null) buttonSignup.onClick.RemoveListener(Click_Signup);
            if (buttonPwd != null) buttonPwd.onClick.RemoveListener(Click_Pwd);
        }
    }
}