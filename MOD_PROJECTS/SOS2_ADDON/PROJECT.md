# SOS2_ADDON 작업 기준

## 지휘관방 벽형 수납 확정 사항

- `Crimson_CommanderWallRefrigerator`와 `Crimson_CommanderWallMedicineCabinet`은 이 프로젝트 소속이다.
- 두 가구는 선체벽을 사용한다. 가로 1칸·세로 2칸, 단일 방향이며 회전하지 않는다.
- 원본 선체벽의 그림 크기와 연결 형태를 그대로 유지한다.
- 벽 좌우에 별도의 구조물이 아주 미세하게 돌출되는 히든 수납문 느낌으로 표현한다.
- 벽 이미지를 확대해서 돌출시키는 방식은 사용하지 않는다.
- 기존 벽·수납 동작과 수납물 숨김을 유지한다. 냉장고 300W·-5°C 보관, 약품장 50W 설정과 기존 필터를 유지한다.
- 일반 냉장고는 FURNITURE, 일반 약품장은 MEDICAL 소속으로 유지한다.

## 현재 작업

- 두 가구와 공통 벽형 수납 기반·렌더러를 이 프로젝트로 모은다.
- 벽은 원래 크기로 복원하고 별도의 얇은 측면 부품으로 외형을 재작업한다.
- 현재 단계: 0.12.33.0 격리 게임 검사와 합성 저장/재로드를 통과하여 적용본에 반영했다. 기존 게임과 격리 검사 게임은 모두 종료되어 있으며, 다음 적용본 실행부터 반영된다.

소스: `Source/Main/CrimsonCommanderWallStorage.cs`와 `Content/1.6/Defs/CrimsonCommanderWallStorage.xml`.
실제 적용 버전과 검사 결과 경로는 재개할 때 저장소 루트의 `.artifacts/active-work.json`만 먼저 확인한다.
