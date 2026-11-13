# Crimson — SOS2 Addon

RimWorld 1.6용 Save Our Ship 2 애드온 개발 저장소입니다. 검은 금속과 심홍색 발광을 사용하는 모노리스 선체, 함선 장비, 생활·왕실 가구, 의복·무기, 로봇과 비행선을 추가합니다.

현재 업로드는 **V180 개발 스냅샷**입니다. 정식 배포 릴리스는 아직 만들지 않았습니다. 이후 수정은 이 저장소에 계속 반영하며, 검증된 배포본은 별도 릴리스로 구분합니다.

## 설치

1. RimWorld 1.6과 아래 필수 모드를 설치합니다.
2. 이 저장소의 `CrimsonSOS2` 폴더를 게임의 `Mods` 폴더 안에 넣습니다. `Mods/CrimsonSOS2/About/About.xml` 경로가 되어야 합니다.
3. 모드 목록에서 필수 모드 뒤에 **Crimson - SOS2 Addon**을 활성화합니다. 선택 호환 모드를 사용하면 해당 모드도 Crimson보다 먼저 불러옵니다.
4. 게임을 재시작합니다. 업데이트 전에는 사용 중인 세이브를 백업합니다.

기존 버전을 갱신할 때는 게임을 종료한 상태에서 같은 모드 폴더를 교체합니다. `packageId`는 `rimod.crimson.sos2`입니다. 세이브와 사용자 설정은 이 저장소에 포함하지 않습니다.

## 의존성

필수:

- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [Vehicle Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404)
- [Save Our Ship 2](https://steamcommunity.com/sharedfiles/filedetails/?id=1909914131)

선택 호환:

- [Dubs Bad Hygiene](https://steamcommunity.com/sharedfiles/filedetails/?id=836308268): 위생·배관 관련 기능
- [Automatic Hydroponics](https://steamcommunity.com/sharedfiles/filedetails/?id=3528490718) 및 [Vanilla Expanded Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013): 자동 수경재배 호환
- [Adaptive Storage Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3033901359): 수납 호환 패치

기능에 따라 Royalty·Biotech DLC가 사용됩니다. 현재까지의 게임 검증은 두 DLC와 위 호환 모드가 설치된 환경을 포함하며, 모든 DLC·모드 조합의 호환성을 보장하지는 않습니다. 외부 모드와 게임 파일은 이 저장소에 동봉하지 않습니다.

## 저장소 구성

| 경로 | 내용 |
| --- | --- |
| `CrimsonSOS2/` | 설치 가능한 모드: 정의, 텍스처, 자체 DLL, 조건부 호환 파일 |
| `Source/CrimsonSOS2/` | 메인 모듈 C# 소스 |
| `Source/CrimsonSOS2.DubsBadHygiene/` | 위생 호환 소스 |
| `Source/CrimsonSOS2.AutomaticHydroponics/` | 자동 수경재배 호환 소스 |
| `Source/CrimsonSOS2.WallReports/` | 벽면 보고 화면 소스 |
| `Build.ps1` | 기존 설치본의 어셈블리를 참조하는 Windows 빌드 스크립트 |

개인 세이브, 시험 실행본, 로그, 내부 작업 문서, 미사용 시안과 참고 이미지 원본은 업로드하지 않습니다. `.gitignore`는 위 배포·개발 경로만 추적하도록 구성되어 있습니다.

## 빌드

Windows의 기존 .NET Framework C# 컴파일러와 설치된 게임·필수 모드 어셈블리를 사용합니다. 새 도구나 게임 파일을 다운로드하지 않습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Steam 기본 설치 경로가 다르면 스크립트의 `-RimWorldRoot`, `-SOS2Root`, `-HarmonyRoot`, `-VehicleFrameworkRoot` 인자를 지정합니다. 선택 호환 모듈에는 `-DubsBadHygieneRoot`, `-VanillaExpandedFrameworkRoot`, `-AutomaticHydroponicsRoot`를 지정할 수 있습니다.

메인 출력은 `CrimsonSOS2/1.6/Assemblies/CrimsonSOS2.dll`입니다. 선택 호환 모듈은 해당 외부 모드의 설치 여부에 따라 빌드됩니다. 빌드 스크립트는 게임에 자동 적용하거나 게임을 실행하지 않습니다.

## 현재 상태

- V180에는 전술 투영 테이블 3종의 화면 11종 선택·유지와 부유 장식 위치 조정이 포함됩니다.
- 관련 기능은 별도 시험 게임에서 동작·저장 복원·외형을 확인했습니다. 이 업로드 작업에서는 게임 검증을 다시 실행하지 않았습니다.
- DLL의 현재 어셈블리 버전은 `0.12.9.0`입니다. 내부 작업 번호 V180과 어셈블리 버전은 서로 다른 표기입니다.
- `About.xml`의 소개문에는 이전 개발 단계의 버전·명칭이 남아 있습니다. 이번 업로드에서는 기존 모드 동작과 파일을 보존했으며, 소개문 정비는 후속 작업입니다.
- 현재 개발 상태를 공유하는 저장소이며, 정식 릴리스 번호·소개 이미지·전체 호환성 정리는 후속 배포 준비에 포함됩니다.

## 출처와 라이선스

SOS2에서 파생한 부분은 원본의 CC BY-NC-SA 4.0 조건을 따릅니다. 출처·변경 범위와 포함된 라이선스 전문은 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 확인해 주세요. 자체 제작 부분 전체에 적용할 별도 라이선스는 아직 지정하지 않았습니다. 이 저장소의 게시를 모든 파일에 대한 포괄적 재사용 허가로 해석하지 마세요.
