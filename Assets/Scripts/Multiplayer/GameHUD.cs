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
        void Start()
        {
            leaveButton.onClick.AddListener(() => NetworkSessionManager.Instance.Leave());
            resumeButton.onClick.AddListener(() => SetPause(false));
        }
        void Update()
        {
            var session = NetworkSessionManager.Instance;
            var player = session != null && session.Net.LocalClient != null ? session.Net.LocalClient.PlayerObject : null;
            if (player != null)
            {
                int slot = player.GetComponent<NetworkPlayer>().Slot.Value;
                teamLabel.text = slot < 2 ? "KIDS · 3인칭   |   WASD 이동 · Q 레고 / E 바나나 · 좌클릭 설치 · Tab 메뉴" : "THIEVES · 1인칭   |   WASD 이동 · 마우스 시점 · Tab 메뉴";
            }
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) SetPause(!pausePanel.activeSelf);
        }
        void SetPause(bool show)
        {
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
