using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MakeTrap.Multiplayer
{
    public class GameHUD : MonoBehaviour
    {
        public TMP_Text teamLabel;
        public GameObject pausePanel;
        public Button leaveButton, resumeButton;
        public Button settingsButton, closeSettingsButton;
        public GameObject settingsPanel;
        public Slider sensitivitySlider;
        public TMP_Text sensitivityLabel;
        void Start()
        {
            leaveButton.onClick.AddListener(() => NetworkSessionManager.Instance.Leave());
            resumeButton.onClick.AddListener(() => SetPause(false));
            settingsButton.onClick.AddListener(() => { pausePanel.SetActive(false); settingsPanel.SetActive(true); });
            closeSettingsButton.onClick.AddListener(CloseSettings);
            sensitivitySlider.minValue = .2f;
            sensitivitySlider.maxValue = 3f;
            sensitivitySlider.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetFloat(StarterAssets.StarterAssetsInputs.SensitivityPreference, 1f), .2f, 3f));
            sensitivityLabel.text = $"{sensitivitySlider.value:0.0}x";
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
            settingsPanel.SetActive(false);
        }
        void Update()
        {
            var session = NetworkSessionManager.Instance;
            var player = session != null && session.Net.LocalClient != null ? session.Net.LocalClient.PlayerObject : null;
            if (player != null)
            {
                int slot = player.GetComponent<NetworkPlayer>().Slot.Value;
                teamLabel.text = slot < 2 ? "KIDS · 3인칭   |   WASD 이동 · Q 레고 / E 바나나 · 좌클릭 설치 · Esc 메뉴" : "THIEVES · 1인칭   |   WASD 이동 · 마우스 시점 · Esc 메뉴";
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (settingsPanel.activeSelf) CloseSettings();
                else SetPause(!pausePanel.activeSelf);
            }
        }
        void SetSensitivity(float value)
        {
            PlayerPrefs.SetFloat(StarterAssets.StarterAssetsInputs.SensitivityPreference, value);
            sensitivityLabel.text = $"{value:0.0}x";
            var player = NetworkSessionManager.Instance?.Net.LocalClient?.PlayerObject;
            if (player != null) player.GetComponent<StarterAssets.StarterAssetsInputs>().cameraSensitivity = value;
        }
        void CloseSettings()
        {
            if (settingsPanel.activeSelf) pausePanel.SetActive(true);
            settingsPanel.SetActive(false);
            PlayerPrefs.Save();
        }
        void OnDestroy() { PlayerPrefs.Save(); }
        void SetPause(bool show)
        {
            CloseSettings();
            pausePanel.SetActive(show);
            var player = NetworkSessionManager.Instance.Net.LocalClient?.PlayerObject;
            if (player != null)
            {
                var input = player.GetComponent<StarterAssets.StarterAssetsInputs>();
                input.move = Vector2.zero; input.look = Vector2.zero; input.jump = input.sprint = false;
                input.cursorLocked = !show; input.cursorInputForLook = !show;
                player.GetComponent<PlayerInput>().enabled = !show;
                var trap = player.GetComponent<TrapPlacementAnimation>();
                if (trap != null) trap.enabled = !show;
            }
            Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = show;
        }
    }
}
