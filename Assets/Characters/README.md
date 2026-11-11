# 캐릭터 폴더 안내

## 현재 사용하는 네 캐릭터

- ThirdPerson/Kids/KID_Boy
- ThirdPerson/Kids/KID_Girl
- ThirdPerson/Thieves/Thief_Fat
- ThirdPerson/Thieves/Thief_Thin

각 캐릭터 폴더는 다음과 같이 통일했습니다.

| 폴더 | 내용 |
|---|---|
| Source | 가져온 원본 ZIP, 보관용 |
| Model | 캐릭터 이름으로 정리한 FBX |
| Textures | BaseColor 텍스처 |
| Materials | URP용 Body 머티리얼 |
| Rig | 실제 뼈 크기로 재구성한 휴머노이드 Avatar |

기존 Man1(Remy), Models, Materials, Textures는 ThirdPerson/Legacy 아래로 이동하여 보존했습니다. SpaceRobotKyle은 기존 위치에 유지했습니다. Unity AssetDatabase.MoveAsset을 사용해 GUID와 기존 연결을 유지했습니다.

## 게임에서 사용되는 프리팹과 애니메이션

기능이 있는 프리팹은 Assets/Prefabs/Multiplayer에 유지했습니다.

- NetworkKid.prefab: KID_Boy
- NetworkKidGirl.prefab: KID_Girl
- NetworkThief.prefab: Thief_Fat
- NetworkThiefThin.prefab: Thief_Thin
- KidPreview / KidGirlPreview / ThiefPreview / ThiefThinPreview: 로비 표시용

공용 애니메이션과 Animator Controller는 Assets/Animations/ThirdPerson에 유지했습니다. 새 캐릭터 폴더에 중복 복사하지 않았습니다. KID 둘은 기존 이동·점프·함정 설치를 사용합니다. 도둑 둘은 기존 1인칭 조작을 유지하고, 다른 참가자에게 보이는 몸에 이동/점프 애니메이션을 적용합니다.

## 로비 선택

각 카드 좌우 화살표로 해당 팀의 두 캐릭터를 순환합니다. 빈 카드의 화살표를 누르면 그 슬롯을 선택합니다. 자신의 슬롯만 바꿀 수 있고 방장은 컴퓨터 슬롯도 변경할 수 있습니다. 사람 외형을 변경하면 준비 상태가 해제됩니다. 선택은 네트워크로 공유되고 실제 생성 프리팹에도 적용됩니다. 서로 같은 외형을 선택하는 것도 가능합니다.

네트워크 프로토콜은 5입니다. 친구와 함께 테스트할 때는 양쪽 모두 새 Builds/MultiplayerTest 폴더를 사용하세요.

## 리그 주의

FBX의 자동 Avatar는 크기 정보가 실제 스켈레톤과 달라 애니메이션 재생 시 몸이 크게 떠오르는 문제가 있었습니다. Rig의 Avatar는 최종 임포트된 실제 Transform의 위치·스케일과 ModelImporter.humanDescription의 T-pose 회전을 조합해 생성합니다. 모델의 바인드 자세(A-pose) 회전으로 덮어쓰면 팔이 등 뒤에서 교차하므로 사용하면 안 됩니다. 네 캐릭터 모두 이 기준으로 수정했습니다.

FBX 뼈 구조나 Import Scale을 변경한다면 Play 모드를 종료한 뒤 프로젝트 루트에서 `unity command eval_file --file Tools/RebuildCharacterAvatars.cs --format json`으로 다시 생성할 수 있습니다. 기존 Avatar 에셋의 GUID를 유지하므로 게임·로비 프리팹의 연결이 유지됩니다.

3인칭 연습용 RemyPlayer 원본은 보존했습니다. 네 캐릭터 선택은 멀티플레이 로비와 게임에 적용됩니다.

## 검증 결과

- 팔 교차 수정 후 네 캐릭터의 앞·뒤 대기, 걷기, 달리기, 함정 설치 중간 자세를 확인했습니다. 캡처: Documentation/Screenshots/ArmsFixed*.png. 수정 빌드 Relay 2개 프로세스에서 외형 선택, 네 캐릭터 생성, 양쪽 KID 함정 설치 동기화 회귀 테스트 통과: Documentation/KidMan/AvatarReferencePoseFixResult.json.
- 네 모델 모두 유효한 Humanoid Avatar와 텍스처, 공용 Animator 연결 확인.
- 실제 로비 버튼 이벤트로 빈 슬롯 선택, 자신의 좌우 변경, 호스트의 컴퓨터 외형 변경 확인.
- 실제 Relay 독립 프로세스2개: 외형 공유, 변경 시 준비 해제, 타인 변경 차단, 잘못된 방향 입력 차단, 선택한 네 프리팹 생성 및 양쪽 KID 함정 설치 동기화 통과.
- 실제 Relay 사람4명 + 초과 참가1명: 접속 제한, 캐릭터 생성, 입력·카메라 소유권, 두 도둑 걷기 애니메이션의 원격 동기화, 이동·회전·퇴장 테스트 통과. 테스트 런타임 오류0.
- 결과: Documentation/KidMan/CharactersRelayResult.json, FourCharacterMovementResult.json.
- 몸 비율이 서로 달라 손·발의 접촉이나 옷 변형은 모션마다 추가 미세 조정이 필요할 수 있습니다. 모든 애니메이션 프레임을 수동 검사한 것은 아닙니다.
