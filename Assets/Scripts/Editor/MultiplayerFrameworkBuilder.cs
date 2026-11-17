using System.IO;
using System.Linq;
using MakeTrap.Multiplayer;
using StarterAssets;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MultiplayerFrameworkBuilder
{
    const string Prefabs = "Assets/Prefabs/Multiplayer/";
    static TMP_FontAsset font;
    static Color paper = new Color(.93f, .89f, .76f);
    static Color wood = new Color(.25f, .16f, .105f);
    static Color dark = new Color(.07f, .10f, .12f, .97f);

    [MenuItem("Tools/MakeTrap/Build Multiplayer Framework")]
    public static void Build()
    {
        if (File.Exists("Assets/Scenes/CharacterSelectLobby.unity")) throw new System.InvalidOperationException("Framework already exists; edit existing assets instead of rebuilding.");
        EditorSceneManager.SaveOpenScenes();
        Directory.CreateDirectory(Prefabs);
        Directory.CreateDirectory("Assets/UI/Multiplayer");
        AssetDatabase.Refresh();
        font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NotoSansKR.ttf"));
        font.name = "MakeTrap Korean"; font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(font, "Assets/UI/Fonts/MakeTrapKorean.asset");
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
        var kids = Player("Assets/Prefabs/ThirdPerson/RemyPlayer.prefab", "NetworkKid", false);
        var thieves = Player("Assets/Prefabs/FirstPerson/PlayerCapsule.prefab", "NetworkThief", true);
        var kidPreview = Preview("Assets/Prefabs/ThirdPerson/RemyPlayer.prefab", "KidPreview");
        var thiefPreview = Preview("Assets/Prefabs/FirstPerson/PlayerCapsule.prefab", "ThiefPreview");
        var room = new GameObject("RoomState", typeof(NetworkObject), typeof(LobbyManager));
        var roomPrefab = PrefabUtility.SaveAsPrefabAsset(room, Prefabs + "RoomState.prefab");
        Object.DestroyImmediate(room);
        var root = new GameObject("NetworkSession", typeof(NetworkManager), typeof(UnityTransport), typeof(NetworkSessionManager));
        var net = root.GetComponent<NetworkManager>();
        net.NetworkConfig.NetworkTransport = root.GetComponent<UnityTransport>();
        net.NetworkConfig.ConnectionApproval = true;
        net.NetworkConfig.EnableSceneManagement = true;
        net.NetworkConfig.TickRate = 30;
        net.NetworkConfig.LoadSceneTimeOut = 60;
        net.NetworkConfig.PlayerPrefab = null;
        var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
        list.Add(new NetworkPrefab { Prefab = roomPrefab });
        list.Add(new NetworkPrefab { Prefab = kids });
        list.Add(new NetworkPrefab { Prefab = thieves });
        AssetDatabase.CreateAsset(list, Prefabs + "NetworkPrefabs.asset");
        net.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
        net.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        var session = root.GetComponent<NetworkSessionManager>();
        session.roomPrefab = roomPrefab.GetComponent<LobbyManager>();
        session.kidsPrefab = kids.GetComponent<NetworkObject>();
        session.thievesPrefab = thieves.GetComponent<NetworkObject>();
        var sessionPrefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "NetworkSession.prefab");
        Object.DestroyImmediate(root);
        Menu(sessionPrefab);
        Lobby(kidPreview, thiefPreview);
        Game();
        var scenePaths = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/CharacterSelectLobby.unity", "Assets/Scenes/GameScene.unity" };
        EditorBuildSettings.scenes = scenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(scenePaths[0]);
        Debug.Log("MULTIPLAYER_FRAMEWORK_BUILT");
    }
    static GameObject Player(string source, string name, bool firstPerson)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source)); go.name = name;
        go.transform.position = Vector3.zero;
        go.AddComponent<NetworkObject>();
        go.AddComponent<OwnerNetworkTransform>();
        var player = go.AddComponent<NetworkPlayer>();
        player.cameraTarget = firstPerson ? go.GetComponent<FirstPersonController>().CinemachineCameraTarget.transform : go.GetComponent<ThirdPersonController>().CinemachineCameraTarget.transform;
        player.followCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(firstPerson ? "Assets/Prefabs/FirstPerson/PlayerFollowCamera.prefab" : "Assets/Prefabs/ThirdPerson/PlayerFollowCamera.prefab").GetComponent<CinemachineCamera>();
        player.ownerBehaviours = go.GetComponents<Behaviour>().Where(b => b is PlayerInput || b is StarterAssetsInputs || b is ThirdPersonController || b is FirstPersonController || b is BasicRigidBodyPush || b is TrapPlacementAnimation).ToArray();
        foreach (var behaviour in player.ownerBehaviours) behaviour.enabled = false;
        player.hideForFirstPersonOwner = firstPerson ? go.GetComponentsInChildren<Renderer>() : new Renderer[0];
        if (!firstPerson) go.AddComponent<OwnerNetworkAnimator>().Animator = go.GetComponent<Animator>();
        var result = PrefabUtility.SaveAsPrefabAsset(go, Prefabs + name + ".prefab"); Object.DestroyImmediate(go); return result;
    }
    static GameObject Preview(string source, string name)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source)); go.name = name;
        // Strip controllers before their RequireComponent dependencies.
        foreach (var b in go.GetComponentsInChildren<TrapPlacementAnimation>(true)) Object.DestroyImmediate(b);
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true).Where(b => !(b is PlayerInput)).ToArray()) Object.DestroyImmediate(b);
        foreach (var b in go.GetComponentsInChildren<PlayerInput>(true)) Object.DestroyImmediate(b);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var result = PrefabUtility.SaveAsPrefabAsset(go, Prefabs + name + ".prefab"); Object.DestroyImmediate(go); return result;
    }
    static void Menu(GameObject session)
    {
        string oldPath = "Assets/Scenes/startScenes.unity";
        string newPath = "Assets/Scenes/MainMenu.unity";
        var error = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error)) throw new System.Exception(error);
        var scene = EditorSceneManager.OpenScene(newPath);
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
        var camera = Camera.main; camera.transform.position = new Vector3(10, 6, -12); camera.transform.LookAt(new Vector3(0, 1, 2));
        Floor("MenuGround", new Vector3(0, -.1f, 0), new Vector3(40, .2f, 40), new Color(.27f,.35f,.23f));
        new GameObject("BackgroundPlacement — 여기에 집과 정원을 배치하세요");
        PrefabUtility.InstantiatePrefab(session);
        var canvasRoot = Canvas("MainMenuCanvas");
        var ui = canvasRoot.AddComponent<MainMenuUI>();
        Text(canvasRoot.transform,"MAKETRAP",new Vector2(.06f,.78f),new Vector2(.58f,.94f),64);
        Text(canvasRoot.transform,"KIDS  vs  THIEVES\n2 : 2 · 집을 지키거나, 침입하거나",new Vector2(.065f,.68f),new Vector2(.6f,.79f),23);
        var menu = Panel(canvasRoot.transform,"Menu",new Vector2(.06f,.20f),new Vector2(.34f,.65f),new Color(0,0,0,0));
        var layout=menu.AddComponent<VerticalLayoutGroup>(); layout.spacing=12; layout.childControlWidth=true; layout.childForceExpandWidth=true; layout.childControlHeight=true; layout.childForceExpandHeight=true;
        ui.hostButton = Button(menu.transform,"게임 호스트",Vector2.zero,Vector2.one);
        ui.joinButton = Button(menu.transform,"게임 참가",Vector2.zero,Vector2.one);
        ui.settingsButton = Button(menu.transform,"설정",Vector2.zero,Vector2.one);
        ui.creditsButton = Button(menu.transform,"제작진",Vector2.zero,Vector2.one);
        ui.quitButton = Button(menu.transform,"종료",Vector2.zero,Vector2.one);
        ui.lanToggle = Toggle(canvasRoot.transform,"LAN 직접 연결 (기본: 인터넷 방 코드)",new Vector2(.06f,.12f),new Vector2(.57f,.18f));
        ui.statusText = Text(canvasRoot.transform,"",new Vector2(.06f,.025f),new Vector2(.94f,.10f),20);
        ui.joinPanel = Modal(canvasRoot.transform,"JoinRoom", "게임 참가");
        Text(ui.joinPanel.transform,"인터넷: 방 코드 / LAN: 호스트 IP",new Vector2(.08f,.62f),new Vector2(.92f,.74f),21);
        ui.connectionInput = Input(ui.joinPanel.transform,new Vector2(.08f,.42f),new Vector2(.92f,.6f));
        ui.connectButton=Button(ui.joinPanel.transform,"접속",new Vector2(.53f,.1f),new Vector2(.92f,.3f));
        ui.closeJoinButton=Button(ui.joinPanel.transform,"닫기",new Vector2(.08f,.1f),new Vector2(.47f,.3f));
        ui.settingsPanel=Modal(canvasRoot.transform,"Settings","설정");
        Text(ui.settingsPanel.transform,"전체 음량",new Vector2(.08f,.61f),new Vector2(.9f,.73f),22);
        ui.volumeSlider=Slider(ui.settingsPanel.transform,new Vector2(.08f,.48f),new Vector2(.92f,.58f));
        ui.fullscreenToggle=Toggle(ui.settingsPanel.transform,"전체 화면",new Vector2(.08f,.30f),new Vector2(.92f,.43f));
        ui.closeSettingsButton=Button(ui.settingsPanel.transform,"닫기",new Vector2(.3f,.06f),new Vector2(.7f,.24f));
        ui.creditsPanel=Modal(canvasRoot.transform,"Credits","제작진");
        Text(ui.creditsPanel.transform,"MakeTrap\n기획 / 제작 : 프로젝트 제작자\n캐릭터 / 컨트롤러 : 기존 프로젝트 리소스\n폰트 : Noto Sans KR (SIL OFL)",new Vector2(.08f,.27f),new Vector2(.92f,.75f),22);
        ui.closeCreditsButton=Button(ui.creditsPanel.transform,"닫기",new Vector2(.3f,.05f),new Vector2(.7f,.23f));
        ui.joinPanel.SetActive(false);ui.settingsPanel.SetActive(false);ui.creditsPanel.SetActive(false);
        SaveUI(canvasRoot,"MainMenuUI");
        EditorSceneManager.SaveScene(scene);
    }
    static void Lobby(GameObject kid,GameObject thief)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        Camera.main.backgroundColor=dark;Camera.main.clearFlags=CameraClearFlags.SolidColor;
        var root=Canvas("LobbyCanvas");var ui=root.AddComponent<LobbyUI>();
        Text(root.transform,"CHOOSE YOUR SIDE",new Vector2(.05f,.86f),new Vector2(.60f,.98f),40);
        ui.codeLabel=Text(root.transform,"ROOM",new Vector2(.64f,.90f),new Vector2(.95f,.98f),23);
        ui.copyCodeButton=Button(root.transform,"코드 복사",new Vector2(.78f,.83f),new Vector2(.95f,.89f));
        Text(root.transform,"KIDS",new Vector2(.06f,.74f),new Vector2(.44f,.85f),36);
        Text(root.transform,"THIEVES",new Vector2(.56f,.74f),new Vector2(.94f,.85f),36);
        Text(root.transform,"VS",new Vector2(.46f,.43f),new Vector2(.54f,.62f),40);
        ui.slots=new Button[4];ui.slotLabels=new TMP_Text[4];
        for(int i=0;i<4;i++)
        {
            float x=i<2?.06f+i*.2f:.56f+(i-2)*.2f;
            var card=Panel(root.transform,"Slot"+i,new Vector2(x,.23f),new Vector2(x+.18f,.73f),dark);
            var raw=Panel(card.transform,"Character",new Vector2(.04f,.35f),new Vector2(.96f,.98f),Color.white);
            Object.DestroyImmediate(raw.GetComponent<Image>());var image=raw.AddComponent<RawImage>();image.raycastTarget=false;
            var preview=raw.AddComponent<CharacterPreview>();preview.visualPrefab=i<2?kid:thief;preview.display=image;preview.previewIndex=i;
            ui.slots[i]=Button(card.transform,"",new Vector2(.02f,.02f),new Vector2(.98f,.33f));
            ui.slotLabels[i]=ui.slots[i].GetComponentInChildren<TMP_Text>();ui.slotLabels[i].fontSize=20;
        }
        ui.leaveButton=Button(root.transform,"방 나가기",new Vector2(.06f,.08f),new Vector2(.22f,.17f));
        ui.readyButton=Button(root.transform,"준비 완료",new Vector2(.57f,.08f),new Vector2(.74f,.17f));ui.readyLabel=ui.readyButton.GetComponentInChildren<TMP_Text>();
        ui.startButton=Button(root.transform,"게임 시작",new Vector2(.77f,.08f),new Vector2(.94f,.17f));
        ui.statusLabel=Text(root.transform,"연결 중...",new Vector2(.06f,.01f),new Vector2(.96f,.07f),20);
        SaveUI(root,"LobbyUI"); EditorSceneManager.SaveScene(scene,"Assets/Scenes/CharacterSelectLobby.unity");
    }
    static void Game()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        Camera.main.gameObject.AddComponent<CinemachineBrain>();
        Camera.main.transform.position=new Vector3(0,10,-12);Camera.main.transform.LookAt(Vector3.zero);
        Floor("Playground",new Vector3(0,-.1f,0),new Vector3(40,.2f,40),new Color(.35f,.39f,.34f));
        var spawn=new GameObject("PlayerSpawnManager").AddComponent<PlayerSpawnManager>();
        for(int i=0;i<4;i++)
        {
            var point=new GameObject(i<2?"KidSpawn0"+(i+1):"ThiefSpawn0"+(i-1));point.transform.SetParent(spawn.transform);
            point.transform.position=new Vector3(i%2==0?-3:3,.15f,i<2?-4:4);point.transform.rotation=Quaternion.Euler(0,i<2?0:180,0);spawn.spawnPoints[i]=point.transform;
        }
        new GameObject("MapContent — 여기에 맵을 배치하세요");
        var root=Canvas("GameHUD");var hud=root.AddComponent<GameHUD>();
        hud.teamLabel=Text(root.transform,"플레이어를 기다리는 중...",new Vector2(.025f,.91f),new Vector2(.98f,.99f),21);
        hud.pausePanel=Modal(root.transform,"Pause","게임 메뉴");
        hud.resumeButton=Button(hud.pausePanel.transform,"계속하기",new Vector2(.15f,.43f),new Vector2(.85f,.65f));
        hud.leaveButton=Button(hud.pausePanel.transform,"메인 메뉴로",new Vector2(.15f,.15f),new Vector2(.85f,.37f));
        hud.pausePanel.SetActive(false);SaveUI(root,"GameHUD");EditorSceneManager.SaveScene(scene,"Assets/Scenes/GameScene.unity");
    }
    static void Floor(string name,Vector3 position,Vector3 scale,Color color)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;
        AssetDatabase.CreateAsset(material, "Assets/UI/Multiplayer/"+name+".mat");go.GetComponent<Renderer>().sharedMaterial=material;
    }
    static GameObject Canvas(string name)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        if(Object.FindFirstObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        return go;
    }
    static RectTransform Rect(GameObject go,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    static GameObject Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));Rect(go,parent,min,max);go.GetComponent<Image>().color=color;return go;
    }
    static TMP_Text Text(Transform parent,string text,Vector2 min,Vector2 max,int size)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));Rect(go,parent,min,max);
        var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=paper;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    static Button Button(Transform parent,string label,Vector2 min,Vector2 max)
    {
        var go=Panel(parent,string.IsNullOrEmpty(label)?"SelectSlot":label,min,max,wood);var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
        var colors=button.colors;colors.highlightedColor=new Color(1.3f,1.2f,1.05f);colors.disabledColor=new Color(.6f,.6f,.6f,.6f);button.colors=colors;
        var text=Text(go.transform,label,new Vector2(.07f,.04f),new Vector2(.93f,.96f),25);text.alignment=TextAlignmentOptions.Center;return button;
    }
    static GameObject Modal(Transform parent,string name,string title)
    {
        var go=Panel(parent,name,new Vector2(.34f,.25f),new Vector2(.85f,.78f),dark);Text(go.transform,title,new Vector2(.08f,.77f),new Vector2(.92f,.96f),32);return go;
    }
    static Toggle Toggle(Transform parent,string label,Vector2 min,Vector2 max)
    {
        var go=Panel(parent,"Toggle",min,max,new Color(0,0,0,0));var toggle=go.AddComponent<Toggle>();
        var box=Panel(go.transform,"Box",new Vector2(0,.1f),new Vector2(.055f,.9f),wood);
        var check=Panel(box.transform,"Check",new Vector2(.2f,.2f),new Vector2(.8f,.8f),paper);
        toggle.targetGraphic=box.GetComponent<Image>();toggle.graphic=check.GetComponent<Image>();Text(go.transform,label,new Vector2(.08f,0),Vector2.one,20);return toggle;
    }
    static Slider Slider(Transform parent,Vector2 min,Vector2 max)
    {
        var go=Panel(parent,"Volume",min,max,wood);var slider=go.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
        var fill=Panel(go.transform,"Fill",Vector2.zero,Vector2.one,paper);slider.fillRect=fill.GetComponent<RectTransform>();
        var handle=Panel(go.transform,"Handle",Vector2.zero,new Vector2(.03f,1),Color.white);slider.handleRect=handle.GetComponent<RectTransform>();slider.targetGraphic=handle.GetComponent<Image>();return slider;
    }
    static TMP_InputField Input(Transform parent,Vector2 min,Vector2 max)
    {
        var go=Panel(parent,"ConnectionInput",min,max,new Color(.16f,.21f,.25f));var field=go.AddComponent<TMP_InputField>();
        var text=Text(go.transform,"",new Vector2(.04f,.08f),new Vector2(.96f,.92f),27);field.textViewport=(RectTransform)go.transform;field.textComponent=(TextMeshProUGUI)text;
        var placeholder=Text(go.transform,"방 코드 또는 IP",new Vector2(.04f,.08f),new Vector2(.96f,.92f),25);placeholder.color=new Color(.6f,.65f,.65f);field.placeholder=placeholder;field.characterLimit=64;return field;
    }
    static void SaveUI(GameObject root,string name) { PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/UI/Multiplayer/"+name+".prefab",InteractionMode.AutomatedAction); }
}
