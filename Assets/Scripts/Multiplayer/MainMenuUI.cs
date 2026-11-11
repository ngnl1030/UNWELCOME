using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MakeTrap.Multiplayer
{
    public class MainMenuUI : MonoBehaviour
    {
        public Button hostButton, joinButton, connectButton, settingsButton, creditsButton, quitButton;
        public Button closeJoinButton, closeSettingsButton, closeCreditsButton;
        public GameObject joinPanel, settingsPanel, creditsPanel;
        public TMP_InputField connectionInput;
        public Toggle lanToggle, fullscreenToggle;
        public Slider volumeSlider;
        public TMP_Text statusText;
        // Menus without the optional LAN toggle use internet room codes.
        bool UseInternet => lanToggle == null || !lanToggle.isOn;
        void Start()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            hostButton.onClick.AddListener(() => NetworkSessionManager.Instance.Host(UseInternet));
            joinButton.onClick.AddListener(() => joinPanel.SetActive(true));
            connectButton.onClick.AddListener(() => NetworkSessionManager.Instance.Join(connectionInput.text, UseInternet));
            settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            creditsButton.onClick.AddListener(() => creditsPanel.SetActive(true));
            closeJoinButton.onClick.AddListener(() => joinPanel.SetActive(false));
            closeSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(false));
            closeCreditsButton.onClick.AddListener(() => creditsPanel.SetActive(false));
            quitButton.onClick.AddListener(Quit);
            volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MasterVolume", 0.8f));
            AudioListener.volume = volumeSlider.value;
            volumeSlider.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat("MasterVolume", value); PlayerPrefs.Save(); });
            fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1);
            if (!Application.isEditor) Screen.fullScreen = fullscreenToggle.isOn;
            fullscreenToggle.onValueChanged.AddListener(value => { Screen.fullScreen = value; PlayerPrefs.SetInt("Fullscreen", value ? 1 : 0); PlayerPrefs.Save(); });
        }
        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (session == null) return;
            statusText.text = session.Status;
            hostButton.interactable = connectButton.interactable = !session.Busy && !session.IsLeaving && !session.Net.IsListening;
        }
        public void OpenPractice(string scene) { SceneManager.LoadScene(scene); }
        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
