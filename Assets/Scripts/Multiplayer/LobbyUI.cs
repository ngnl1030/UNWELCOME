using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MakeTrap.Multiplayer
{
    public class LobbyUI : MonoBehaviour
    {
        public Button[] slots;
        public TMP_Text[] slotLabels;
        public Button readyButton, startButton, leaveButton, copyCodeButton;
        public Button fillBotsButton;
        public Button[] botButtons = new Button[0];
        public Button[] previousCharacterButtons = new Button[0], nextCharacterButtons = new Button[0];
        public CharacterPreview[] previews = new CharacterPreview[0];
        public TMP_Text[] characterNames = new TMP_Text[0];
        public TMP_Text readyLabel, statusLabel, codeLabel;
        public Color kidsColor = new Color(0.2f, 0.53f, 0.66f);
        public Color thievesColor = new Color(0.65f, 0.33f, 0.22f);
        public Color selectedColor = new Color(0.82f, 0.67f, 0.27f);
        void Start()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            for (int i = 0; i < slots.Length; i++) { int slot = i; slots[i].onClick.AddListener(() => LobbyManager.Instance?.SelectSlotRpc(slot)); }
            readyButton.onClick.AddListener(() => LobbyManager.Instance?.ToggleReadyRpc());
            startButton.onClick.AddListener(() => LobbyManager.Instance?.StartGameRpc());
            leaveButton.onClick.AddListener(() => NetworkSessionManager.Instance.Leave());
            copyCodeButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = NetworkSessionManager.Instance.RoomCode);
            if (fillBotsButton != null) fillBotsButton.onClick.AddListener(() => LobbyManager.Instance?.FillBotsRpc());
            for (int i = 0; i < botButtons.Length; i++) { int slot = i; botButtons[i].onClick.AddListener(() => LobbyManager.Instance?.ToggleBotRpc(slot)); }
            for (int i = 0; i < previousCharacterButtons.Length; i++) { int slot = i; previousCharacterButtons[i].onClick.AddListener(() => LobbyManager.Instance?.ChangeCharacterRpc(slot,-1)); nextCharacterButtons[i].onClick.AddListener(() => LobbyManager.Instance?.ChangeCharacterRpc(slot,1)); }
        }
        void Update()
        {
            var session = NetworkSessionManager.Instance;
            var room = LobbyManager.Instance;
            if (session == null || room == null || !room.IsSpawned) return;
            codeLabel.text = "ROOM  " + session.RoomCode;
            var own = room.IndexOf(session.Net.LocalClientId);
            readyButton.interactable = own >= 0 && room.Players[own].Slot >= 0 && !room.Starting.Value;
            readyLabel.text = own >= 0 && room.Players[own].Ready ? "준비 취소" : "준비 완료";
            startButton.gameObject.SetActive(session.Net.IsHost);
            startButton.interactable = room.CanStart;
            int bots = 0; foreach (var player in room.Players) if (player.IsBot) bots++;
            if (fillBotsButton != null) { fillBotsButton.gameObject.SetActive(session.Net.IsHost); fillBotsButton.interactable = !room.Starting.Value && room.Players.Count < 4; }
            statusLabel.text = room.Starting.Value ? "게임 씬을 불러오는 중..." : $"사람 {room.Players.Count - bots} + 컴퓨터 {bots} / 4 · 슬롯 선택 후 준비하세요. 컴퓨터는 자동 준비됩니다.";
            for (int i = 0; i < 4; i++)
            {
                bool occupied = false, mine = false, ready = false, isBot = false;
                ulong id = 0;
                int choice = i % 2;
                int occupant = room.OccupantIndex(i);
                if (occupant >= 0) choice = room.Players[occupant].CharacterIndex;
                foreach (var player in room.Players) if (player.Slot == i) { occupied = true; id = player.ClientId; mine = !player.IsBot && id == session.Net.LocalClientId; ready = player.Ready; isBot = player.IsBot; break; }
                slots[i].interactable = (!occupied || mine) && !room.Starting.Value;
                slots[i].image.color = mine ? selectedColor : i < 2 ? kidsColor : thievesColor;
                if (i < previews.Length) previews[i].SetCharacter(choice);
                if (i < characterNames.Length) characterNames[i].text = i < 2 ? (choice == 0 ? "KID BOY" : "KID GIRL") : (choice == 0 ? "THIEF FAT" : "THIEF THIN");
                if (i < previousCharacterButtons.Length)
                {
                    bool canChange = !room.Starting.Value && (!occupied || mine || (isBot && session.Net.IsHost));
                    previousCharacterButtons[i].interactable = nextCharacterButtons[i].interactable = canChange;
                }
                string title = (i < 2 ? "KID " : "THIEF ") + (i % 2 + 1);
                slotLabels[i].text = title + "\n" + (occupied ? (isBot ? "컴퓨터" : $"Player {id}" + (mine ? " · 나" : "")) + "\n" + (ready ? "READY" : "선택 중") : "EMPTY · 클릭하여 선택");
                if (i < botButtons.Length && botButtons[i] != null)
                {
                    botButtons[i].gameObject.SetActive(session.Net.IsHost);
                    botButtons[i].interactable = !room.Starting.Value && (isBot || (!occupied && room.Players.Count < 4));
                    botButtons[i].GetComponentInChildren<TMP_Text>().text = isBot ? "컴퓨터 제거" : "컴퓨터 추가";
                }
            }
        }
    }
}
