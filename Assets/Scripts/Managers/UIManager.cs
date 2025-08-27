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

        //登录UI
        [SerializeField] private GameObject LoginUI;

        //登录UI 按钮
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

        [SerializeField] private GameObject errorMessage; // 父 GameObject，包含 TextMeshProUGUI
        [SerializeField] private GameObject tipsMessage;


        private TextMeshProUGUI errorText; // TextMeshProUGUI 组件
        private TextMeshProUGUI tipsText; // TextMeshProUGUI 组件
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
            // 获取 errorMessage 的 TextMeshProUGUI 组件（位于子对象第三级）
            if (errorMessage != null)
            {
                errorText = errorMessage.GetComponentInChildren<TextMeshProUGUI>();
                if (errorText == null)
                {
                    Debug.LogError("UIManager: errorMessage 中未找到 TextMeshProUGUI 组件！");
                }
                else
                {
                    errorMessage.SetActive(false); // 初始化隐藏
                }
            }

            // 获取 tipsMessage 的 TextMeshProUGUI 组件（位于子对象第三级）
            if (tipsMessage != null)
            {
                tipsText = tipsMessage.GetComponentInChildren<TextMeshProUGUI>();
                if (tipsText == null)
                {
                    Debug.LogError("UIManager: tipsMessage 中未找到 TextMeshProUGUI 组件！");
                }
                else
                {
                    tipsMessage.SetActive(false); // 初始化隐藏
                }
            }


            // 绑定按钮点击事件
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
            // 获取输入框的文本
            string nameText = characterName.text;

            // 验证输入是否有效
            if (string.IsNullOrEmpty(nameText))
            {
                ShowErrorMessage("角色名称不能为空！");
                return;
            }

            // 通知服务器用户登录
            if (WebSocketManager.Instance.IsConnected)
            {
                NetworkMessageHandler.Instance.SendCharacterCreate(nameText,GameManager.Instance.GetLoginAccount());
                Debug.Log($"发送玩家角色创建消息: name: {nameText}");
                // 假设服务器会异步返回结果，错误提示在 WebSocket 回调中处理
            }
            else
            {
                ShowErrorMessage("网络未连接，请检查网络！");
            }
        }

        private void Click_Login()
        {
            // 获取输入框的文本
            string usernameText = username.text;
            string passwordText = password.text;

            // 验证输入是否有效
            if (string.IsNullOrEmpty(usernameText) || string.IsNullOrEmpty(passwordText))
            {
                ShowErrorMessage("用户名或密码不能为空！");
                return;
            }

            // 通知服务器用户登录
            if (WebSocketManager.Instance.IsConnected)
            {
                NetworkMessageHandler.Instance.SendLoginRequest(usernameText, passwordText);
                GameManager.Instance.SetLoginAccount(usernameText);
                Debug.Log($"发送玩家登录消息: username: {usernameText}, password: {passwordText}");
                // 假设服务器会异步返回结果，错误提示在 WebSocket 回调中处理
            }
            else
            {
                ShowErrorMessage("网络未连接，请检查网络！");
            }
        }

        // 新增：显示错误消息，5秒后隐藏
        public void ShowErrorMessage(string message)
        {
            if (errorText == null || errorMessage == null)
            {
                Debug.LogError("UIManager: errorMessage 或 errorText 未正确初始化！");
                return;
            }

            // 确保文本编码为 UTF-8（通常无需手动转换，Unity 默认支持）
            errorText.text = message;
            errorMessage.SetActive(true);
            StartCoroutine(HideErrorMessageAfterDelay(5f));

            // 调试：检查是否包含不支持的字符
            foreach (char c in message)
            {
                if (!errorText.font.HasCharacter(c))
                {
                    Debug.LogWarning($"字体 {errorText.font.name} 不支持字符: {c} (Unicode: \\u{(int)c:X4})");
                }
            }
        }

        // 新增：显示消息，5秒后隐藏
        public void ShowTipsMessage(string message)
        {
            if (tipsText == null || tipsMessage == null)
            {
                Debug.LogError("UIManager: tipsMessage 或 tipsText 未正确初始化！");
                return;
            }

            // 确保文本编码为 UTF-8（通常无需手动转换，Unity 默认支持）
            tipsText.text = message;
            tipsMessage.SetActive(true);
            StartCoroutine(HideTipsMessageAfterDelay(5f));

            // 调试：检查是否包含不支持的字符
            foreach (char c in message)
            {
                if (!tipsText.font.HasCharacter(c))
                {
                    Debug.LogWarning($"字体 {tipsText.font.name} 不支持字符: {c} (Unicode: \\u{(int)c:X4})");
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
                errorMessage.SetActive(false); // 关闭登录界面时隐藏错误消息
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