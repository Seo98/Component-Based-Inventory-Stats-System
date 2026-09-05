# Component-Based-Inventory-Stats-System

컴포넌트 기반 캐릭터 스탯, 네트워크 전투 및 그리드 인벤토리를 테스트하기 위한 Unity 프로젝트입니다.  
게임 데이터와 행동 설정은 ScriptableObject 중심으로 구성하며, 반복 작업을 줄이기 위한 전용 에디터 도구를 제공합니다.

## Essential Libraries

### Unity Registry

- Netcode for GameObjects
- Addressables
- Cinemachine
- Multiplayer Play Mode

## Public Repository Scope

팀 프로젝트에서는 네트워크 환경의 인벤토리 동기화 작업까지 완료했습니다. 다만 해당 기능이 의존하는 **네트워크 커맨드 패턴은 다른 작업자가 구현한 코드**이므로, 이 공개 저장소에서는 관련 스크립트를 제외했습니다.

따라서 이 저장소는 협업자의 네트워크 커맨드 패턴 구현을 포함하거나 제 작업으로 주장하지 않습니다. 현재 `MonsterRoot`는 `RPC`와 `NetworkList`를 이용해 루팅 데이터의 추가·제거를 확인하는 간단한 네트워크 테스트 용도입니다.

## Test Scene

테스트 씬: [Assets/Scenes/TestScenes.unity](Assets/Scenes/TestScenes.unity)

테스트 가능한 항목:

- 플레이어 이동
- 네트워크 환경에서 서버 기준 데미지 처리 및 체력 동기화
- ScriptableObject를 이용한 범용 캐릭터·오브젝트 스탯 구성
- 로컬 그리드 인벤토리의 아이템 배치 및 이동
- 설정된 최대 수량에 따른 동일 아이템 중첩
- 아이템 드래그 중 `R` 키를 이용한 회전
- 테스트용 몬스터 루팅 데이터의 간단한 네트워크 동기화

## Demo & Showcase

### YouTube Demo

> 데모 영상 링크 추가 예정

<!-- 아래 링크를 실제 영상 주소로 교체하세요. -->
<!-- [YouTube에서 데모 영상 보기](https://www.youtube.com/watch?v=VIDEO_ID) -->

### Example Game

> 인벤토리, 스탯 및 전투 시스템을 적용한 예시 게임 작업물 추가 예정

- **프로젝트명:** 추가 예정
- **개발 기간:** 추가 예정
- **담당 범위:** 추가 예정
- **주요 구현:** 추가 예정

#### Screenshots

이미지는 `Docs/Images` 폴더에 아래 이름으로 추가한 뒤 주석을 해제하면 표시됩니다.

<!--
| Gameplay | Inventory | Combat |
| --- | --- | --- |
| ![Gameplay](Docs/Images/example-gameplay.png) | ![Inventory](Docs/Images/example-inventory.png) | ![Combat](Docs/Images/example-combat.png) |
-->

## Editor Tools

### Item Database Editor

실행 경로: `Tools > Item Database Editor`

사용 방법:

1. `Item Database`에 편집할 데이터베이스 에셋을 지정합니다.
2. `Search Root`에 ItemData를 검색할 폴더를 지정합니다.
3. `Add All ItemData`로 폴더의 아이템 데이터를 데이터베이스에 일괄 등록합니다.
4. 필요한 항목을 수정한 뒤 `Save Assets`로 저장합니다.

지원 기능:

- ItemData 일괄 검색 및 등록
- ID, 이름, 등급, 타입, 장비 슬롯, 중첩 수량 등 주요 데이터 편집
- ItemID 중복 감지 및 중복 항목 필터링
- 타입별 필터 및 정렬
- Null 항목 제거와 ID 기준 데이터베이스 정렬

### Action Hitbox Authoring Tool

실행 경로: `Tools > Project G > Action Hitbox Authoring`

사용 방법:

1. 캐릭터 또는 공격 기준이 될 `Origin` Transform을 지정합니다.
2. Sphere, Box, Capsule 중 필요한 히트 볼륨을 추가합니다.
3. Scene 뷰의 Move, Rotate, Scale 도구로 위치와 크기를 조정합니다.
4. 쿨다운, 실행 지연, 데미지 배율, 최소 데미지, 대상 레이어 등을 설정합니다.
5. 저장 경로와 이름을 정한 뒤 `Create Hitbox Action SO`를 눌러 ScriptableObject를 생성합니다.

지원 기능:

- Sphere, Box, Capsule 형태의 복수 히트 볼륨 미리보기
- 기존 HitboxActionDefinition 불러오기 및 수정
- 공격 판정, 데미지 값, Friendly Fire 및 레이어 설정
- 완성된 HitboxActionDefinition ScriptableObject 생성
- 선택한 CharacterActionController에 생성 결과 자동 등록

## Future Architecture Considerations

현재 구조는 기능을 작은 컴포넌트로 분리하고, Inspector 참조와 이벤트를 통해 연결합니다. 프로젝트 규모가 커져 컴포넌트 간 의존성과 초기화 순서 관리가 복잡해질 경우 다음 방식을 선택적으로 검토할 수 있습니다.

- **Dependency Injection:** 인벤토리, 스탯, 전투 서비스의 생성과 연결을 Composition Root에 모아 결합도를 낮추고 테스트 교체를 쉽게 만듭니다. Unity 환경에서는 [VContainer](https://github.com/hadashiA/VContainer) 같은 DI 컨테이너를 후보로 고려할 수 있습니다.
- **R3:** 체력·스탯·인벤토리 변경처럼 연속적으로 발생하는 이벤트를 반응형 스트림으로 구성하여 UI 갱신과 상태 구독 코드를 단순화할 수 있습니다. 후보 라이브러리로 [Cysharp R3](https://github.com/Cysharp/R3)를 검토할 수 있습니다.

두 방식은 현재 프로젝트의 필수 의존성이 아니며, 단순한 컴포넌트 연결까지 무조건 대체하기보다 의존 관계와 이벤트 흐름이 실제로 복잡해지는 시점에 필요한 영역부터 도입하는 것을 목표로 합니다.

## Third-Party Attribution

### Grid Inventory

This repository includes modified portions of a Grid Inventory implementation.

- Original copyright: Copyright (c) 2020 Farrokh Games
- License: MIT License
- License notice: [Farrokh Games MIT License](LICENSES/Farrokh-Games-MIT.md)

The inventory implementation was adapted and extended for this project.

### Character Stats Reference

The character stat architecture was inspired by **Character Stats** by Kryzarel. No original package source files or assets are included or redistributed in this repository.

- [Character Stats - Unity Asset Store](https://assetstore.unity.com/packages/tools/utilities/character-stats-106351)
