# MakeTrap 멀티플레이 프레임워크

## 실행

1. `Assets/Scenes/MainMenu.unity`를 열고 Play 합니다.
2. 인터넷 연결은 **LAN 직접 연결을 끈 상태**에서 **게임 호스트**를 누릅니다.
3. 로비 상단의 **코드 복사**로 방 코드를 친구에게 전달합니다.
4. 친구는 같은 버전의 빌드에서 **게임 참가 → 방 코드 → 접속**을 누릅니다.
5. 각자 빈 슬롯을 선택하고 **준비 완료**를 누릅니다. 부족한 인원은 방장이 **빈자리 컴퓨터로 채우기** 또는 슬롯별 **컴퓨터 추가**로 채울 수 있습니다. 아이 2명, 도둑 2명 총 4자리(사람+컴퓨터)가 모두 준비하면 **게임 시작** 버튼이 활성화됩니다.
6. 게임 안에서 WASD와 마우스로 조작합니다. **Tab**으로 메뉴를 열고 나갈 수 있습니다. 아이는 기존 **F → 함정 미리보기 → 좌클릭 → 설치 애니메이션**을 계속 사용할 수 있습니다.

같은 PC/같은 LAN 테스트는 **LAN 직접 연결**을 켜고 호스트를 생성합니다. 같은 PC 참가 주소는 `127.0.0.1`, 다른 PC는 호스트의 LAN IP입니다. UDP 기본 포트는 7777입니다. 외부 인터넷 친구 접속에는 기본 방 코드 방식을 사용하세요.

`1인칭 연습`, `3인칭 연습` 버튼은 원래의 독립 테스트 씬을 엽니다. 멀티플레이 로비와 연결되지 않는 기존 연습 기능입니다. 연습을 마치면 Play를 종료하고 MainMenu를 다시 실행합니다.

## 씬과 보존한 기능

- 기존 `startScenes.unity`는 GUID를 유지하여 `MainMenu.unity`로 이동했습니다. 기존 시작 UI는 비활성 상태로 씬 안에 보존했고, 원래 1인칭/3인칭 이동 기능은 새 메뉴의 연습 버튼에 연결했습니다.
- `CharacterSelectLobby.unity`: 팀 슬롯 4개, 서버 검증, 준비 상태, 코드 복사, 독립 캐릭터 미리보기.
- `GameScene.unity`: 공용 테스트 바닥, 팀별 Spawn Point 4개, 로컬 플레이어 카메라, 메뉴 HUD.
- 기존 `1stPerson.unity`, `3rdPerson.unity`, 이동 컨트롤러, 원본 캐릭터 프리팹, 함정 애니메이션 및 프리뷰 스크립트는 보존했습니다.

도둑은 기존 `PlayerCapsule`이므로 현재 시각 모델은 캡슐입니다. 임의의 새 컨트롤러/캐릭터를 만들지 않았습니다.

`ThirdPersonController`는 원격 캐릭터에서도 발소리 애니메이션 이벤트가 정상 처리되도록 CharacterController 참조 초기화만 Start에서 Awake로 옮겼습니다. 기존 이동·카메라 계산은 변경하지 않았습니다.

## Inspector 설정 위치

### 네트워크 세션

`Assets/Prefabs/Multiplayer/NetworkSession.prefab` → **NetworkSessionManager**

- **Menu Scene / Lobby Scene / Game Scene**: 씬 이름. 이름을 바꾸면 Build Settings와 함께 수정합니다.
- **Room Prefab**: `RoomState.prefab`.
- **Kids Prefab**: `NetworkKid.prefab` (기존 Remy 기반).
- **Thieves Prefab**: `NetworkThief.prefab` (기존 PlayerCapsule 기반).
- **Direct Port**: LAN 직접 연결 포트.

같은 오브젝트의 **NetworkManager → NetworkConfig → Prefabs**에는 `NetworkPrefabs.asset`이 연결되어 있습니다. 새 네트워크 캐릭터 프리팹은 이 목록에도 등록해야 합니다. 자동 Player Prefab은 비워두고 로비 역할에 따라 서버가 직접 생성합니다.

인원은 2:2 규칙에 맞춰 **4명으로 고정**했습니다. 단순히 숫자 하나를 바꿔 다른 인원을 지원하지는 않습니다.

### 플레이어와 카메라

`NetworkKid.prefab` / `NetworkThief.prefab` → **NetworkPlayer**

- **Follow Camera Prefab**: 기존 팀별 `PlayerFollowCamera.prefab`.
- **Camera Target**: 캐릭터의 `PlayerCameraRoot`.
- **Owner Behaviours**: 본인에게만 켜지는 PlayerInput, StarterAssetsInputs, 기존 이동 컨트롤러와 함정 입력.
- **Hide For First Person Owner**: 1인칭 본인에게 몸체를 숨길 렌더러. 다른 사람에게는 그대로 보입니다.

`GameScene → Main Camera`의 CinemachineBrain이 로컬 플레이어 카메라를 사용합니다. 각 클라이언트에는 활성 메인 카메라/AudioListener가 하나입니다.

이동은 기존 컨트롤러와 충돌하지 않도록 **소유자 권한 NetworkTransform**을 사용합니다. 서버는 슬롯/준비/시작/소환을 검증합니다. 경쟁 게임 수준의 서버 이동 검증이나 치트 방지는 이번 프레임워크 범위가 아닙니다.

### 맵과 스폰

`GameScene → PlayerSpawnManager → Spawn Points`

배열 순서: `KidSpawn01`, `KidSpawn02`, `ThiefSpawn01`, `ThiefSpawn02`.

씬에서 각 Transform을 이동하면 소환 위치가 바뀝니다. 바닥은 기존 컨트롤러의 Ground Layers에 포함되는 Default 레이어입니다. 새 맵의 충돌 레이어도 확인하세요.

### UI와 프리뷰

`Assets/UI/Multiplayer/MainMenuUI.prefab`, `LobbyUI.prefab`, `GameHUD.prefab`에서 배치·문구·Image Sprite를 수정할 수 있습니다. CanvasScaler 기준은 1600×900입니다.

- **MainMenuUI**: 패널·버튼·음량·전체 화면 연결.
- **LobbyUI**: Kids Color / Thieves Color / Selected Color 및 슬롯 버튼 연결.
- 슬롯의 **CharacterPreview**: Visual Prefab과 RawImage 연결. 미리보기 프리팹에는 입력·이동·네트워크 컴포넌트가 없습니다.
- `MainMenu → BackgroundPlacement` 아래에 집/정원 등을 배치합니다. 기존 Main Camera의 고정 구도를 변경할 수 있습니다.

## 인터넷 서비스 설정

패키지: NGO 2.13.3, Multiplayer Services 2.4.1 및 해결된 Transport/Authentication 의존성.

현재 Unity 프로젝트의 기존 Cloud 연결을 사용합니다. 별도 프로젝트로 옮길 때는 다음을 확인하세요.

1. Unity Editor **Edit → Project Settings → Services**에서 올바른 Organization / Cloud Project에 연결합니다.
2. [Unity Dashboard](https://cloud.unity.com/)에서 같은 프로젝트를 선택합니다.
3. Authentication의 익명 로그인, Multiplayer의 Lobby/Relay 서비스가 사용할 수 있는 상태인지 확인합니다. 조직 정책에 따른 이용 약관/결제 설정은 프로젝트 소유자가 처리해야 합니다.
4. 서로 같은 Cloud Project와 빌드를 사용하는지 확인합니다. 방 코드는 실행 중인 호스트 방에만 유효합니다.

앱은 Unity Services 초기화 → 익명 로그인 → 세션 생성/방 코드 참가 → Relay/NGO 연결 순서로 실행됩니다. 코드에 API 키나 계정 비밀번호를 저장하지 않습니다. 호스트 마이그레이션은 제공하지 않으며 호스트 연결 종료 시 메인 메뉴로 돌아갑니다.

## 코드 역할

- `NetworkSessionManager.cs`: 호스트/참가, 인터넷/LAN, 최대 인원 승인, 연결 종료, NGO 씬 전환.
- `LobbyManager.cs`: 네트워크 플레이어 목록, 슬롯 점유, Ready, 서버 시작 검증.
- `PlayerSessionData.cs`: 클라이언트 ID, 슬롯, Ready와 팀 매핑.
- `PlayerSpawnManager.cs`: 서버의 팀별 PlayerObject 생성.
- `NetworkPlayer.cs`: 소유권에 따른 입력·카메라 활성화.
- `OwnerNetworkTransform.cs`, `OwnerNetworkAnimator.cs`: 기존 컨트롤러에 맞는 소유자 동기화.
- `MainMenuUI.cs`, `LobbyUI.cs`, `GameHUD.cs`, `CharacterPreview.cs`: UI 표시와 프리뷰.

## 검증 도구

`Tools/MultiplayerSmoke.ps1`은 Development Build를 5개 프로세스로 실행해 슬롯·접속 제한·씬 전환·소유권·이동 동기화·종료 흐름을 검사합니다. 기본 실행은 LAN이며 `-Internet`을 주면 방 코드/Relay를 사용합니다. 결과와 각 프로세스 로그는 `Temp/MultiplayerTests/<RunName>`에 저장됩니다.

`MultiplayerTestClient`는 Editor/Development Build에서 **명시적인 `-mtTestPath` 실행 인자가 있을 때만** 동작합니다. 일반 실행에서는 설치되지 않고 Release Build에는 포함되지 않습니다.

### 2026-10-22 검증 결과

- Windows Development Build 성공, 빌드 오류 0.
- LAN 및 실제 인터넷 Relay 방 코드 연결로 각각 독립 프로세스 5개 테스트 통과. 호스트 포함 4명 접속, 5번째 거부.
- 슬롯 중복·잘못된 슬롯 차단, 슬롯 변경 시 Ready 해제, 퇴장 시 슬롯 해제, 재접속 통과.
- 전원 Ready 전 시작 차단, 클라이언트 시작 요청 차단, 전원 게임 씬 전환 통과.
- 2종 캐릭터의 소유권별 입력/이동 제한, 카메라·AudioListener 각 1개, 위치·회전 동기화 통과.
- 클라이언트 퇴장/소환 해제 및 호스트 종료 시 메인 메뉴 복귀 통과. 최종 테스트 런타임 오류 0.
- 5개 씬의 누락 스크립트 0. 메뉴·로비·3인칭·1인칭 화면도 에디터에서 확인.

결과: `Documentation/LANTestResult.json`, `Documentation/InternetTestResult.json`.
화면: `Documentation/Screenshots/`.

인터넷 검증은 같은 PC의 독립 프로세스들이 실제 Relay 서비스에 연결한 테스트입니다. 서로 다른 집의 PC/공유기/방화벽 조합은 별도로 확인해야 합니다.

테스트용 실행 파일은 `Builds/MultiplayerTest/MakeTrap.exe`입니다. 친구에게 전달할 때는 `.exe`만 보내지 말고 **MultiplayerTest 폴더 전체**를 전달하세요.

## 현재 범위

승패, 귀중품, 실제 주택 맵, 호스트 마이그레이션은 요구사항에 따라 추가하지 않았습니다. 함정은 설치 애니메이션이 끝날 때 서버가 NetworkObject로 생성하여 모든 참가자에게 표시합니다. F 미리보기는 본인에게만 보입니다. 피해·발동·해제 등 함정 게임 규칙은 아직 추가하지 않았습니다.

## 컴퓨터 참가자

혼자 실행: **게임 호스트 → 자기 슬롯 선택 → 빈자리 컴퓨터로 채우기 → 준비 완료 → 게임 시작**. 게임은 `GameScene`으로 이동합니다.

- 컴퓨터는 자동으로 준비 완료됩니다. 방장만 슬롯별 추가/제거를 할 수 있습니다.
- 로비가 컴퓨터 포함 4자리인 상태에서 사람이 접속하면 컴퓨터 한 자리를 자동으로 비웁니다. 참가자는 그 빈 슬롯을 선택하면 됩니다. 이미 시작된 게임에는 중도 참가할 수 없습니다.
- 컴퓨터는 서버에서 기본 순찰 이동을 하고 위치·회전·애니메이션을 다른 참가자에게 동기화합니다. 추적, 함정 설치, 귀중품 수집, 승패 판단을 하는 전략 AI는 아직 아닙니다.
- `Assets/Prefabs/Multiplayer/NetworkKid.prefab`, `NetworkThief.prefab`의 **BotPatrol**에서 Speed(속도), Patrol Radius(순찰 반경), Turn Speed(회전 속도)를 조절합니다. 컴포넌트 활성화는 네트워크 스폰 시 자동 처리하므로 프리팹에서는 꺼 둡니다.
- 기존 사람의 이동·카메라·함정 조작은 유지했습니다. 네트워크 데이터 형식이 바뀌었으므로 친구에게도 새 `Builds/MultiplayerTest` 폴더 전체를 전달하세요.

컴퓨터 검증: `Tools/MultiplayerBotsSmoke.ps1 -RunName BotsRelay -Internet`. 실제 Relay에 연결한 독립 프로세스 2개로 사람 참가 시 컴퓨터 교체, 방장 권한, 슬롯 동기화, 사람 2명+컴퓨터 2명 시작, 입력·카메라 분리, 서버 순찰 및 상대 화면 위치 동기화, 종료 처리와 런타임 오류 0을 확인했습니다. 결과: `Documentation/BotTestResult.json`.

추가 확인: 혼자+컴퓨터 3명 게임 시작/종료 및 기존 사람 4명 Relay 회귀 테스트도 통과했습니다. 새 빌드 오류 0, 테스트 런타임 오류 0. 사람 전용 결과: `Documentation/HumansAfterBotsTestResult.json`.


## 함정 네트워크 표시

- 기존 F → 실루엣 → 좌클릭 → 설치 애니메이션 순서를 유지합니다. 설치 트리거도 NetworkAnimator로 전송합니다.
- `TrapPlacementAnimation.PlaceBox()`는 멀티플레이에서 `NetworkPlayer.PlaceTrapRpc()`를 호출하고, 서버가 소유권·거리·유효한 지면·요청 간격을 확인한 뒤 `NetworkTrapBox.prefab`을 생성합니다. 설치된 함정은 방 전체에 표시되며 씬 종료 때 정리됩니다.
- `Assets/Prefabs/Multiplayer/NetworkTrapBox.prefab`은 원본 `Assets/Prefabs/Traps/TrapBox.prefab`과 별도입니다. 기존 싱글 연습 씬은 원래 로컬 설치 방식을 유지합니다.
- 변경된 네트워크 프로토콜은 3입니다. 호스트와 친구 모두 새 빌드를 사용하세요.
- 검증 도구: `Tools/MultiplayerTrapsSmoke.ps1 -RunName TrapsRelay -Internet`. 실제 설치 애니메이션 이벤트를 통해 호스트와 참가자가 각각 생성한 함정의 네트워크 ID·위치를 두 프로세스에서 비교합니다.

함정 검증 결과: 실제 Relay 연결로 호스트 설치 1개와 참가자 설치 1개가 양쪽에서 동일한 네트워크 ID·위치로 생성되었습니다. 중복 생성 없음, 빌드 오류 0, 테스트 런타임 오류 0. 결과: `Documentation/TrapTestResult.json`. 테스트는 같은 PC의 별도 프로세스 두 개에서 수행했습니다.
