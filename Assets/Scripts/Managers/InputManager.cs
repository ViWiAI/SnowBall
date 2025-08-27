using Game.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField] private TMP_Text TipsMouse;
    [SerializeField] private TMP_Text TipsPlayer;
    [SerializeField] private Button loginButton; // 登录按钮
    [SerializeField] public TMP_InputField usernameInput; // 拖拽账号输入框到此字段
    [SerializeField] public TMP_InputField passwordInput; // 拖拽密码输入框到此字段

    void Awake()
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

    void Update()
    {
        HandleTabKeydown();
        HandleEnterKeydown();
        HandleMouseInput();
    }

    private void HandleTabKeydown()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("Tab key down");
            if (usernameInput == null || passwordInput == null)
            {
                Debug.LogWarning("用户名或密码输入框未设置");
                return;
            }

            GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
            if (currentSelected == null)
            {
                usernameInput.Select();
                return;
            }

            if (currentSelected == usernameInput.gameObject)
            {
                passwordInput.Select();
            }
            else
            {
                usernameInput.Select();
            }
        }
    }

    private void HandleEnterKeydown()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("检测到回车键");
            if (GameManager.Instance.GetLoginStatus())
            {
                Debug.Log("玩家已经登录");
                return;
            }
            if (EventSystem.current.currentSelectedGameObject == usernameInput.gameObject ||
                EventSystem.current.currentSelectedGameObject == passwordInput.gameObject)
            {
                if (loginButton != null && loginButton.interactable)
                {
                    loginButton.onClick.Invoke();
                    Debug.Log("回车键触发登录按钮");
                }
                else
                {
                    Debug.LogWarning("登录按钮未设置或不可交互");
                }
            }
        }
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0)) OnMouseLeftClick();
        if (Input.GetMouseButtonDown(1)) OnMouseRightClick();
    }

    private void OnMouseLeftClick()
    {
        Debug.Log("鼠标左键点击");
        // 滚雪球相关的左键逻辑
    }

    private void OnMouseRightClick()
    {
        Debug.Log("鼠标右键点击");
        // 滚雪球相关的右键逻辑
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}