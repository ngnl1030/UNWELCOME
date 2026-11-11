#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

namespace MakeTrap.Multiplayer
{
    // Opt-in development harness. Never runs in a normal player session.
    public class MultiplayerTestClient : MonoBehaviour
    {
        [Serializable] public class Command { public int sequence; public string action; public string value; public int slot; public bool internet; }
        [Serializable] public class PlayerState { public ulong id; public int slot, character; public bool ready, bot; }
        [Serializable] public class AvatarState { public ulong id; public int slot; public string model; public float animationSpeed; public bool owner, bot, patrol; public Vector3 position; public float yaw; public bool input; public bool movement; }
        [Serializable] public class State
        {
            public int frame, sequence, errors, cameras, listeners;
            public string scene, status, code;
            public bool connected, host, canStart;
            public ulong localId;
            public PlayerState[] players;
            public AvatarState[] avatars;
            public string[] traps;
        }
        string folder;
        int sequence, errors;
        float next;
        Vector2 move;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-mtTestPath");
            if (index < 0 || index + 1 >= args.Length) return;
            var test = new GameObject("MultiplayerTestClient").AddComponent<MultiplayerTestClient>();
            test.folder = args[index + 1];Directory.CreateDirectory(test.folder);
            DontDestroyOnLoad(test.gameObject);
            Application.targetFrameRate = 30;
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        void Update()
        {
            var session = NetworkSessionManager.Instance;
            if (session == null || string.IsNullOrEmpty(folder)) return;
            var local = session.Net.LocalClient?.PlayerObject;
            if (local != null) local.GetComponent<StarterAssetsInputs>().move = move;
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + .25f;
            var commandPath = Path.Combine(folder,"command.json");
            try
            {
                if (File.Exists(commandPath))
                {
                    var command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath));
                    if (command.sequence > sequence)
                    {
                        sequence = command.sequence;
                        switch(command.action)
                        {
                            case "host": session.Host(command.internet); break;
                            case "join": session.Join(command.value,command.internet); break;
                            case "select": LobbyManager.Instance?.SelectSlotRpc(command.slot); break;
                            case "character": LobbyManager.Instance?.ChangeCharacterRpc(command.slot, int.Parse(command.value)); break;
                            case "ready": LobbyManager.Instance?.ToggleReadyRpc(); break;
                            case "start": LobbyManager.Instance?.StartGameRpc(); break;
                            case "fillbots": LobbyManager.Instance?.FillBotsRpc(); break;
                            case "bot": LobbyManager.Instance?.ToggleBotRpc(command.slot); break;
                            case "leave": session.Leave(); break;
                            case "move": move = command.slot == 0 ? Vector2.zero : new Vector2(command.slot == 2 ? 1 : 0,command.slot == 2 ? 0 : 1); break;
                            case "look": if(local!=null)local.GetComponent<StarterAssetsInputs>().look=command.slot==0?Vector2.zero:new Vector2(2,0); break;
                            case "trap":
                                if (local != null)
                                {
                                    var placement = local.GetComponent<TrapPlacementAnimation>();
                                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                                    var type = typeof(TrapPlacementAnimation);
                                    type.GetField("selectedTrap", flags).SetValue(placement, command.value == "banana" ? 1 : 0);
                                    type.GetField("placementPosition", flags).SetValue(placement, local.transform.position + local.transform.forward * 1.2f + Vector3.up * .3f);
                                    type.GetField("placementRotation", flags).SetValue(placement, local.transform.rotation);
                                    type.GetField("placementPending", flags).SetValue(placement, true);
                                    local.GetComponent<OwnerNetworkAnimator>().SetTrigger("PlaceTrap");
                                }
                                break;
                            case "quit": Application.Quit(); break;
                        }
                    }
                }
                var room=LobbyManager.Instance;
                var state=new State { frame=Time.frameCount,sequence=sequence,errors=errors,scene=SceneManager.GetActiveScene().name,status=session.Status,code=session.RoomCode,connected=session.Net.IsConnectedClient,host=session.Net.IsHost,localId=session.Net.LocalClientId,canStart=room!=null && room.CanStart };
                var players = new System.Collections.Generic.List<PlayerState>();
                if(room!=null) foreach(var p in room.Players) players.Add(new PlayerState { id=p.ClientId,slot=p.Slot,ready=p.Ready,bot=p.IsBot,character=p.CharacterIndex });
                state.players=players.ToArray();
                state.avatars=FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Select(p=>new AvatarState { id=p.OwnerClientId,slot=p.Slot.Value,model=p.name,animationSpeed=p.GetComponent<Animator>()!=null?p.GetComponent<Animator>().GetFloat("Speed"):0,owner=p.IsOwner,bot=p.IsBot,patrol=p.GetComponent<BotPatrol>()!=null && p.GetComponent<BotPatrol>().enabled,position=p.transform.position,yaw=p.transform.eulerAngles.y,input=p.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled,movement=p.GetComponent<ThirdPersonController>()!=null?p.GetComponent<ThirdPersonController>().enabled:p.GetComponent<FirstPersonController>().enabled }).ToArray();
                state.cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled);
                state.listeners=FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c=>c.enabled);
                state.traps=FindObjectsByType<Unity.Netcode.NetworkObject>(FindObjectsSortMode.None).Where(n=>n.IsSpawned && (n.name.StartsWith("NetworkTrapBox") || n.name.StartsWith("NetworkBananaTrap"))).OrderBy(n=>n.NetworkObjectId).Select(n=>n.NetworkObjectId+":"+n.name+":"+n.transform.position.ToString("F2")).ToArray();
                File.WriteAllText(Path.Combine(folder,"state.tmp"),JsonUtility.ToJson(state,true));
                if(File.Exists(Path.Combine(folder,"state.json"))) File.Replace(Path.Combine(folder,"state.tmp"),Path.Combine(folder,"state.json"),null);
                else File.Move(Path.Combine(folder,"state.tmp"),Path.Combine(folder,"state.json"));
            }
            catch(IOException) { /* A command can be replaced while the test driver is writing it. */ }
        }
    }
}
#endif
