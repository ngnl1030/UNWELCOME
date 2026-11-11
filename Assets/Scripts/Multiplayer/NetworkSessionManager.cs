using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MakeTrap.Multiplayer
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class NetworkSessionManager : MonoBehaviour
    {
        public static NetworkSessionManager Instance { get; private set; }
        [Header("Scene names")]
        public string menuScene = "MainMenu";
        public string lobbyScene = "CharacterSelectLobby";
        public string gameScene = "GameScene";
        [Header("Network prefabs")]
        public LobbyManager roomPrefab;
        public NetworkObject kidsPrefab;
        public NetworkObject thievesPrefab;
        public NetworkObject[] kidVariants;
        public NetworkObject[] thiefVariants;
        public ushort directPort = 7777;
        public bool Busy { get; private set; }
        public bool IsLeaving => leaving;
        public string Status { get; private set; } = "친구와 방 코드를 공유하세요.";
        public string RoomCode { get; private set; } = "";
        public event Action Changed;
        public NetworkManager Net { get; private set; }
        ISession session;
        bool leaving;
        bool quitting;
        float connectionDeadline;
        readonly HashSet<ulong> approvedClients = new HashSet<ulong>();
        int operation;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Net = GetComponent<NetworkManager>();
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Net.ConnectionApprovalCallback = Approve;
            Net.OnServerStarted += ServerStarted;
            Net.OnClientConnectedCallback += Connected;
            Net.OnClientDisconnectCallback += Disconnected;
            Net.OnTransportFailure += TransportFailed;
        }
        public void SetStatus(string value) { Status = value; Changed?.Invoke(); }
        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool inGame = LobbyManager.Instance != null && LobbyManager.Instance.Starting.Value;
            // NGO processes approvals after callbacks; reserve seats immediately for simultaneous joins.
            response.Approved = !inGame && (approvedClients.Contains(request.ClientNetworkId) || approvedClients.Count < 4);
            if (response.Approved) approvedClients.Add(request.ClientNetworkId);
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = inGame ? "이미 게임이 시작된 방입니다." : "방이 가득 찼습니다. (최대 4명)";
        }
        async Task InitializeServices()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Separate local processes must not share one anonymous player identity.
                var options = new InitializationOptions().SetProfile("player" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        public async void Host(bool internet = true)
        {
            if (Busy || Net.IsListening || leaving) return;
            int currentOperation = ++operation;
            Busy = true; SetStatus("방을 만드는 중...");
            try
            {
                if (internet)
                {
                    await InitializeServices();
                    if (currentOperation != operation || this == null) return;
                    var created = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = 4, IsPrivate = true, Name = "MakeTrap" }.WithRelayNetwork());
                    if (currentOperation != operation || this == null) { await created.LeaveAsync(); return; }
                    session = created;
                    RoomCode = session.Code;
                }
                else
                {
                    GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", directPort, "0.0.0.0");
                    if (!Net.StartHost()) throw new Exception("호스트를 시작하지 못했습니다.");
                    RoomCode = "LAN : " + directPort;
                }
                SetStatus("방 생성 완료. 친구에게 연결 정보를 전달하세요.");
            }
            catch (Exception e) { if (currentOperation == operation && this != null) await Fail(e); }
            finally { Busy = false; Changed?.Invoke(); }
        }
        public async void Join(string code, bool internet = true)
        {
            if (Busy || Net.IsListening || leaving) return;
            if (string.IsNullOrWhiteSpace(code)) { SetStatus("방 코드 또는 IP 주소를 입력하세요."); return; }
            int currentOperation = ++operation;
            Busy = true; SetStatus("방에 연결하는 중...");
            try
            {
                if (internet)
                {
                    await InitializeServices();
                    if (currentOperation != operation || this == null) return;
                    var joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                    if (currentOperation != operation || this == null) { await joined.LeaveAsync(); return; }
                    session = joined;
                    RoomCode = session.Code;
                }
                else
                {
                    if (!System.Net.IPAddress.TryParse(code.Trim(), out _)) throw new Exception("올바른 IP 주소를 입력하세요.");
                    GetComponent<UnityTransport>().SetConnectionData(code.Trim(), directPort);
                    if (!Net.StartClient()) throw new Exception("클라이언트를 시작하지 못했습니다.");
                    connectionDeadline = Time.realtimeSinceStartup + 20;
                    RoomCode = code.Trim();
                }
            }
            catch (Exception e) { if (currentOperation == operation && this != null) await Fail(e); }
            finally { Busy = false; Changed?.Invoke(); }
        }
        async Task Fail(Exception e)
        {
            Debug.LogWarning("Session connection: " + e.Message);
            await LeaveInternal("연결 실패: " + e.Message);
        }
        void Update()
        {
            if (connectionDeadline > 0 && Time.realtimeSinceStartup > connectionDeadline)
            {
                connectionDeadline = 0;
                Leave("연결 시간이 초과되었습니다. 주소와 방 상태를 확인하세요.");
            }
        }
        void ServerStarted()
        {
            var room = Instantiate(roomPrefab);
            DontDestroyOnLoad(room.gameObject);
            room.NetworkObject.Spawn(false);
            Net.SceneManager.OnLoadEventCompleted += SceneLoaded;
            Net.SceneManager.LoadScene(lobbyScene, LoadSceneMode.Single);
        }
        void SceneLoaded(string scene, LoadSceneMode mode, System.Collections.Generic.List<ulong> completed, System.Collections.Generic.List<ulong> timedOut)
        {
            if (scene != gameScene || !Net.IsServer) return;
            foreach (ulong id in timedOut) if (id != Unity.Netcode.NetworkManager.ServerClientId) Net.DisconnectClient(id, "맵 로딩 시간이 초과되었습니다.");
            var spawns = FindFirstObjectByType<PlayerSpawnManager>();
            if (spawns == null) { Leave("게임 씬에 Spawn Point 설정이 없습니다."); return; }
            spawns.SpawnPlayers(this);
        }
        void Connected(ulong id)
        {
            if (Net.IsServer) approvedClients.Add(id);
            if (id != Net.LocalClientId) return;
            connectionDeadline = 0;
            SetStatus("연결되었습니다. 원하는 슬롯을 선택하세요.");
        }
        void Disconnected(ulong id)
        {
            approvedClients.Remove(id);
            if (quitting || leaving || id != Net.LocalClientId) return;
            string reason = Net.DisconnectReason;
            Leave(string.IsNullOrEmpty(reason) ? "호스트와 연결이 종료되었습니다." : reason);
        }
        void TransportFailed() { if (!leaving && !quitting) Leave("네트워크 연결에 실패했습니다."); }
        public async void Leave(string message = "방에서 나왔습니다.") { await LeaveInternal(message); }
        async Task LeaveInternal(string message)
        {
            if (leaving) return;
            leaving = true;
            operation++;
            connectionDeadline = 0;
            if (Net.SceneManager != null) Net.SceneManager.OnLoadEventCompleted -= SceneLoaded;
            var oldSession = session; session = null;
            RoomCode = "";
            // Sessions owns NGO's lifecycle for Relay connections.
            if (oldSession != null)
            {
                try { await oldSession.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning("Session cleanup: " + e.Message); }
            }
            if (Net.IsListening) Net.Shutdown();
            approvedClients.Clear();
            // Let NGO finish releasing scene objects before loading an offline scene.
            while (Net.ShutdownInProgress) await Task.Yield();
            if (!quitting)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (SceneManager.GetActiveScene().name != menuScene) await LoadMenu();
                SetStatus(message);
            }
            leaving = false;
        }
        async Task LoadMenu()
        {
            var load = SceneManager.LoadSceneAsync(menuScene);
            while (!load.isDone) await Task.Yield();
        }
        void OnApplicationQuit() { quitting = true; operation++; }
        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Net.OnServerStarted -= ServerStarted;
            Net.OnClientConnectedCallback -= Connected;
            Net.OnClientDisconnectCallback -= Disconnected;
            Net.OnTransportFailure -= TransportFailed;
        }
    }
}
