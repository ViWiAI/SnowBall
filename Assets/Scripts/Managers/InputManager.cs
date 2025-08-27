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
    [SerializeField] private Button loginButton; // ��¼��ť
    [SerializeField] public TMP_InputField usernameInput; // ��ק�˺�����򵽴��ֶ�
    [SerializeField] public TMP_InputField passwordInput; // ��ק��������򵽴��ֶ�

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
                Debug.LogWarning("�û��������������δ����");
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
            Debug.Log("��⵽�س���");
            if (GameManager.Instance.GetLoginStatus())
            {
                Debug.Log("����Ѿ���¼");
                return;
            }
            if (EventSystem.current.currentSelectedGameObject == usernameInput.gameObject ||
                EventSystem.current.currentSelectedGameObject == passwordInput.gameObject)
            {
                if (loginButton != null && loginButton.interactable)
                {
                    loginButton.onClick.Invoke();
                    Debug.Log("�س���������¼��ť");
                }
                else
                {
                    Debug.LogWarning("��¼��ťδ���û򲻿ɽ���");
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
        Debug.Log("���������");
        // ��ѩ����ص�����߼�
    }

    private void OnMouseRightClick()
    {
        Debug.Log("����Ҽ����");
        // ��ѩ����ص��Ҽ��߼�
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}