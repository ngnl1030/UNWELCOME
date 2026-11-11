using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MakeTrap.Multiplayer
{
    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }
        public NetworkList<PlayerSessionData> Players;
        public readonly NetworkVariable<bool> Starting = new NetworkVariable<bool>();
        public event Action Changed;

        void Awake() { Players = new NetworkList<PlayerSessionData>(); }
        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Players.OnListChanged += OnListChanged;
            Starting.OnValueChanged += OnStarting;
            if (IsServer)
            {
                foreach (ulong id in NetworkManager.ConnectedClientsIds) AddPlayer(id);
                NetworkManager.OnClientConnectedCallback += AddPlayer;
                NetworkManager.OnClientDisconnectCallback += RemovePlayer;
            }
        }
        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            Players.OnListChanged -= OnListChanged;
            Starting.OnValueChanged -= OnStarting;
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= AddPlayer;
                NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
            }
        }
        void OnListChanged(NetworkListEvent<PlayerSessionData> _) => Changed?.Invoke();
        void OnStarting(bool previous, bool next) => Changed?.Invoke();
        public int IndexOf(ulong id)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].ClientId == id) return i;
            return -1;
        }
        void AddPlayer(ulong id)
        {
            if (IndexOf(id) >= 0) return;
            // Bots reserve game slots, never real network connections. Give arriving humans priority.
            if (Players.Count >= 4)
                for (int i = Players.Count - 1; i >= 0; i--)
                    if (Players[i].IsBot) { Players.RemoveAt(i); break; }
            Players.Add(new PlayerSessionData { ClientId = id, Slot = -1 });
        }
        void RemovePlayer(ulong id)
        {
            int index = IndexOf(id);
            if (index >= 0) Players.RemoveAt(index);
        }
        public bool CanStart
        {
            get
            {
                if (Starting.Value || Players.Count != 4) return false;
                int slots = 0;
                foreach (var player in Players)
                {
                    if (!player.Ready || player.Slot < 0 || player.Slot > 3) return false;
                    slots |= 1 << player.Slot;
                }
                return slots == 15;
            }
        }
        bool CanManageBots(ulong sender) => sender == Unity.Netcode.NetworkManager.ServerClientId && !Starting.Value;
        public int OccupantIndex(int slot)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].Slot == slot) return i;
            return -1;
        }
        void AddBot(int slot)
        {
            if (Players.Count >= 4 || slot < 0 || slot > 3 || OccupantIndex(slot) >= 0) return;
            Players.Add(new PlayerSessionData { ClientId = ulong.MaxValue - (ulong)slot, Slot = slot, Ready = true, IsBot = true, CharacterIndex = slot % 2 });
        }
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ToggleBotRpc(int slot, RpcParams rpc = default)
        {
            if (!CanManageBots(rpc.Receive.SenderClientId) || slot < 0 || slot > 3) return;
            int index = OccupantIndex(slot);
            if (index < 0) AddBot(slot);
            else if (Players[index].IsBot) Players.RemoveAt(index);
        }
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void FillBotsRpc(RpcParams rpc = default)
        {
            if (!CanManageBots(rpc.Receive.SenderClientId)) return;
            // Reserve enough empty slots for every human who has not selected yet.
            for (int slot = 0; slot < 4; slot++) AddBot(slot);
        }
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void SelectSlotRpc(int slot, RpcParams rpc = default)
        {
            int index = IndexOf(rpc.Receive.SenderClientId);
            if (Starting.Value || index < 0 || slot < 0 || slot > 3) return;
            foreach (var other in Players) if (other.Slot == slot && other.ClientId != rpc.Receive.SenderClientId) return;
            var player = Players[index];
            if (player.Slot == slot) return;
            player.Slot = slot;
            player.CharacterIndex = slot % 2;
            player.Ready = false;
            Players[index] = player;
        }
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ChangeCharacterRpc(int slot, int direction, RpcParams rpc = default)
        {
            if (Starting.Value || slot < 0 || slot > 3 || (direction != -1 && direction != 1)) return;
            int index = OccupantIndex(slot);
            if (index < 0)
            {
                index = IndexOf(rpc.Receive.SenderClientId);
                if (index < 0) return;
                var selected = Players[index]; selected.Slot = slot; selected.CharacterIndex = slot % 2; selected.Ready = false;
                Players[index] = selected;
            }
            var player = Players[index];
            if (player.IsBot ? rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId : player.ClientId != rpc.Receive.SenderClientId) return;
            player.CharacterIndex = (player.CharacterIndex + direction + 2) % 2;
            player.Ready = player.IsBot;
            Players[index] = player;
        }
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ToggleReadyRpc(RpcParams rpc = default)
        {
            int index = IndexOf(rpc.Receive.SenderClientId);
            if (Starting.Value || index < 0 || Players[index].Slot < 0) return;
            var player = Players[index];
            player.Ready = !player.Ready;
            Players[index] = player;
        }
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void StartGameRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId || !CanStart) return;
            Starting.Value = true;
            var status = NetworkManager.SceneManager.LoadScene(NetworkSessionManager.Instance.gameScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started) Starting.Value = false;
        }
    }
}
