using Game.Network;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Managers
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        // 登录 UI
        [SerializeField] private GameObject LoginUI;
        [SerializeField] private Button buttonLogin;
        [SerializeField] private Button buttonSignup;
        [SerializeField] private Button buttonPwd;
        [SerializeField] private Button characterCreate;
        [SerializeField] private Button stageButton;
        [SerializeField] private Button stageBackButton;
        [SerializeField] private Button startGameButton;

        // Lobby
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
        [SerializeField] private GameObject errorMessage;
        [SerializeField] private GameObject tipsMessage;

        private TextMeshProUGUI errorText;
        private TextMeshProUGUI tipsText;
        private string signupUrl = "https://www.baidu.com";
        private string forgotPwdUrl = "https://www.baidu.com";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUIComponents();
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // 绑定按钮事件
            if (buttonLogin != null) buttonLogin.onClick.AddListener(Click_Login);
            else Debug.LogError("UIManager: buttonLogin 未赋值！");
            if (buttonSignup != null) buttonSignup.onClick.AddListener(Click_Signup);
            else Debug.LogError("UIManager: buttonSignup 未赋值！");
            if (buttonPwd != null) buttonPwd.onClick.AddListener(Click_Pwd);
            else Debug.LogError("UIManager: buttonPwd 未赋值！");
            if (characterCreate != null) characterCreate.onClick.AddListener(Click_CharacterCreate);
            else Debug.LogError("UIManager: characterCreate 未赋值！");
            if (stageButton != null) stageButton.onClick.AddListener(Click_Stage);
            else Debug.LogError("UIManager: stageButton 未赋值！");
            if (stageBackButton != null) stageBackButton.onClick.AddListener(Click_StageBack);
            else Debug.LogError("UIManager: stageBackButton 未赋值！");
            if (startGameButton != null) startGameButton.onClick.AddListener(Click_StartGame);
            else Debug.LogError("UIManager: startGameButton 未赋值！");
        }

        private void InitializeUIComponents()
        {
            // 验证 UI 预制体引用
            if (LoginUI == null || LobbyUI == null || ChangeNameUI == null || StageUI == null)
            {
                Debug.LogError("UIManager: 部分 UI 预制体引用未设置！");
            }
         
            // 初始化错误和提示文本
            if (errorMessage != null)
            {
                errorText = errorMessage.GetComponentInChildren<TextMeshProUGUI>();
                errorMessage.SetActive(false);
            }
            if (tipsMessage != null)
            {
                tipsText = tipsMessage.GetComponentInChildren<TextMeshProUGUI>();
                tipsMessage.SetActive(false);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 确保 Canvas 存在
            if (GetComponentInChildren<Canvas>() == null)
            {
                Debug.LogWarning("UIManager: Canvas 丢失，尝试重新初始化！");
                InitializeUIComponents();
            }

            // 确保 EventSystem 存在
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(eventSystem);
            }


        }

        public void Click_StartGame()
        {
            GameManager.Instance.StartGame();
            ShowLobby(false);
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
            string nameText = characterName.text;
            if (string.IsNullOrEmpty(nameText))
            {
                ShowErrorMessage("角色名称不能为空！");
                return;
            }

            if (WebSocketManager.Instance.IsConnected)
            {
                NetworkMessageHandler.Instance.SendCharacterCreate(nameText, GameManager.Instance.GetLoginAccount());
                Debug.Log($"发送玩家角色创建消息: name: {nameText}");
            }
            else
            {
                ShowErrorMessage("网络未连接，请检查网络！");
            }
        }

        private void Click_Login()
        {
            string usernameText = username.text;
            string passwordText = password.text;

            if (string.IsNullOrEmpty(usernameText) || string.IsNullOrEmpty(passwordText))
            {
                ShowErrorMessage("用户名或密码不能为空！");
                return;
            }

            if (WebSocketManager.Instance.IsConnected)
            {
                NetworkMessageHandler.Instance.SendLoginRequest(usernameText, passwordText);
                GameManager.Instance.SetLoginAccount(usernameText);
                Debug.Log($"发送玩家登录消息: username: {usernameText}, password: {passwordText}");
            }
            else
            {
                ShowErrorMessage("网络未连接，请检查网络！");
            }
        }

        public void ShowErrorMessage(string message)
        {
            if (errorText == null || errorMessage == null)
            {
                Debug.LogError("UIManager: errorMessage 或 errorText 未正确初始化！");
                InitializeUIComponents();
                if (errorText == null || errorMessage == null) return;
            }

            errorText.text = message;
            errorMessage.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(HideErrorMessageAfterDelay(5f));
        }

        public void ShowTipsMessage(string message)
        {
            if (tipsText == null || tipsMessage == null)
            {
                Debug.LogError("UIManager: tipsMessage 或 tipsText 未正确初始化！");
                InitializeUIComponents();
                if (tipsText == null || tipsMessage == null) return;
            }

            tipsText.text = message;
            tipsMessage.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(HideTipsMessageAfterDelay(5f));
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
                errorMessage.SetActive(false);
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

        public void SetUserInfo(string name, int lvl, int curHP, int maxHP, int curMP, int maxMP)
        {
            chaName.text = name;
            HP.text = $"{curHP}/{maxHP}";
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
            if (Instance == this)
            {
                Instance = null;
            }
            if (buttonLogin != null) buttonLogin.onClick.RemoveListener(Click_Login);
            if (buttonSignup != null) buttonSignup.onClick.RemoveListener(Click_Signup);
            if (buttonPwd != null) buttonPwd.onClick.RemoveListener(Click_Pwd);
            if (characterCreate != null) characterCreate.onClick.RemoveListener(Click_CharacterCreate);
            if (stageButton != null) stageButton.onClick.RemoveListener(Click_Stage);
            if (stageBackButton != null) stageBackButton.onClick.RemoveListener(Click_StageBack);
            if (startGameButton != null) startGameButton.onClick.RemoveListener(Click_StartGame);
        }
    }
}