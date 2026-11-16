---
name: rimod-project-work
description: Route Crimson RimWorld mod feature requests to the matching MOD_PROJECTS folder and work with bounded context. Use for furniture, recreation, sanitary, medical, ship, equipment, production, and decoration work in this repository.
---

# 프로젝트 범위를 먼저 좁히기

분류에는 작은 프로젝트 색인만 사용한다. 아래 명령은 저장소 루트 기준이다.

```powershell
python Tools/Route-Project.py --query "사용자의 작업 대상"
```

- 사용자가 프로젝트나 파일을 지정하면 `--project FURNITURE` 또는 `--path MOD_PROJECTS/...`로 그 범위를 우선한다. 여러 기능 요청이면 반환된 프로젝트들을 각각 다룬다.
- 분류되지 않으면 소스 전체를 읽어 추측하지 말고 대상 이름이나 프로젝트를 확인한다. 저장소 구조·빌드 도구·스킬 자체 작업은 기능 프로젝트로 억지 분류하지 않는다.
- 선택된 폴더에 `PROJECT.md`가 있으면 그 프로젝트의 확정 사항과 현재 작업만 먼저 읽는다. 이어 `rg --files`와 심볼 검색으로 필요한 파일을 찾는다. 다른 프로젝트는 실제 참조가 필요할 때 해당 파일만 확인한다. 읽기와 수정 권한의 구분은 기존 지침을 따른다.
- `CrimsonSOS2`는 생성 패키지 링크다. 편집 원본은 `MOD_PROJECTS`에 있다. 일반 기능 탐색에서 `.artifacts`, `Artwork`, 생성 패키지와 과거 검사 폴더를 재귀 검색하지 않는다.
- 미완료 작업 재개 요청일 때만 `.artifacts/active-work.json`을 먼저 확인한다. 과거 로그는 필요한 기록의 해당 구간만 읽는다.

# 출력과 실행 범위

큰 출력이 예상되는 빌드·Git 검사·검증 명령은 전체 출력을 대화로 보내지 않고 로그에 저장한다.

```powershell
python Tools/Run-Logged.py --name furniture-build --project FURNITURE -- powershell -NoProfile -ExecutionPolicy Bypass -File Build.ps1
```

요약은 기본 3,000자로 제한되고 전체 로그 경로를 돌려준다. 실패 원인을 더 읽을 때도 해당 로그의 오류 구간으로 한정한다. 작업에 필요하면 범위를 명시해 확장한다.

통합 빌드가 다른 프로젝트를 컴파일하는 것과 에이전트가 그 소스를 모두 읽는 것은 별개다. 빌드 때문에 전체 소스를 출력하지 않는다. 게임 실행·검증 한도·배포 승인은 기존 지침을 유지하며, 이전 검사 게임의 종료를 확인하고 다음 검사를 시작한다.

상세 도구: [프로젝트 분류](../../../Tools/Route-Project.py), [출력 제한 실행](../../../Tools/Run-Logged.py), [프로젝트 색인](../../../MOD_PROJECTS/projects.json).
