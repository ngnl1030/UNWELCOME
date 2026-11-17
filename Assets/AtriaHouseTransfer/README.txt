집과 마당 가져오기

1. 대상 프로젝트에서 Assets > Import Package > Custom Package로 unitypackage를 가져옵니다.
2. Assets/AtriaHouseTransfer/AtriaHouseAndYard.prefab을 씬으로 드래그합니다.
3. 대상 프로젝트에 이미 태양 조명이 있다면 프리팹의 03 Lighting에서 중복 조명을 조절하세요.

Unity 6000.6.9f1에서 내보냈으며 건축물, 마당, 가구, 조명, 충돌체, 문 리지드바디/힌지와 PushOpenDoor 스크립트를 포함합니다. 테스트 플레이어와 이동경로 점검 도구는 제외했습니다. 원본 씬은 변경하지 않았습니다.

재질은 Built-in Standard입니다. URP에서는 가져온 재질을 URP로 변환해야 합니다. 프로젝트 전체 렌더 설정은 포함하지 않았습니다.
문 밀기: 대상 CharacterController의 OnControllerColliderHit에서 충돌한 문에 붙은 AtriaHouse.PushOpenDoor.Push(hit.point, controller.velocity)를 호출하면 기존 밀기 동작을 사용할 수 있습니다. 이 패키지 자체에는 멀티플레이 문 동기화가 포함되지 않습니다.
