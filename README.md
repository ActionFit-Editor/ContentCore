# ActionFit Content Core (`com.actionfit.content-core`)

프로젝트 전용 `DataStore`, 재화 시스템, Firebase에 의존하지 않는 콘텐츠 저장·보상 코어입니다. 패키지만 설치해도 PlayerPrefs 기반 기본 구현을 사용할 수 있고, 실제 게임에서는 같은 인터페이스를 구현해 운영 저장소와 보상 시스템으로 교체할 수 있습니다.

## 주요 기능

- 콘텐츠 상태 JSON을 저장하는 `IContentStateStore`
- 중요한 상태 전이를 즉시 내보내는 선택적 `IFlushableContentStateStore`
- 두 개의 독립 슬롯, 단조 증가 revision, SHA-256 envelope 검증을 사용하는 `PlayerPrefsContentStateStore`
- 최신 슬롯이 손상되어도 이전 유효 슬롯을 읽는 fallback
- 프로젝트 재화 타입과 분리된 `ContentReward`
- transaction ID 단위 멱등 지급을 정의하는 `IContentRewardService`
- 프로젝트 보상 어댑터의 안전한 지급 가능 여부를 사전 확인하는 `IContentRewardService.IsAvailable`
- 하나의 ledger JSON에 지급 완료 transaction과 로컬 잔액을 함께 저장하는 `PlayerPrefsContentRewardService`

## 기본 사용

```csharp
using System.Collections.Generic;
using ActionFit.Content;

IContentStateStore stateStore = new PlayerPrefsContentStateStore();
stateStore.Save("ice-cream-race", "{\"score\":120}");

if (stateStore.TryLoad("ice-cream-race", out string stateJson))
{
    // Deserialize the project or content-owned state DTO.
}

IContentRewardService rewards = new PlayerPrefsContentRewardService();
bool granted = rewards.GrantOnce(
    "ice-cream-race:season-12:result-001",
    new List<ContentReward>
    {
        new ContentReward("coin", 100),
        new ContentReward("gem", 5)
    });
```

`GrantOnce`는 처음 지급했을 때만 `true`, 같은 transaction ID가 이미 ledger에 있으면 `false`를 반환합니다. 기본 구현의 로컬 잔액은 `PlayerPrefsContentRewardService.GetBalance`로 확인할 수 있습니다.

## 프로젝트 어댑터

운영 프로젝트에서는 다음 경계를 교체하세요.

- `IContentStateStore`: 클라우드 저장, 프로젝트 `DataStore`, 버전 마이그레이션
- `IFlushableContentStateStore`: 시작·결과·보상 transaction처럼 즉시 내구성이 필요한 전이의 명시적 flush
- `IContentRewardService`: 실제 재화 지급과 영속 멱등 transaction. 원자적 지급을 제공하지 못하면 `IsAvailable`을 `false`로 반환

패키지 콘텐츠는 구체 저장소나 재화 시스템 대신 두 인터페이스만 참조해야 합니다. 상태 DTO의 버전과 마이그레이션은 각 콘텐츠 패키지가 소유합니다.

## PlayerPrefs 안전 계약

- 상태 키는 UTF-16 code unit을 가역 hex 문자열로 바꿔 서로 다른 콘텐츠 ID가 같은 PlayerPrefs 슬롯 키를 공유하지 않게 합니다.
- 저장할 때 이전 유효 슬롯을 유지한 채 반대 슬롯에 다음 revision을 기록합니다.
- envelope의 schema, 콘텐츠 ID, revision, payload를 SHA-256으로 검증합니다.
- `PlayerPrefsContentStateStore`의 `Save`, `Delete`, `Flush`와 reward ledger 변경은 반환 전에 `PlayerPrefs.Save()`를 호출합니다.
- reward ledger가 손상되면 중복 지급으로 복구하지 않고 `InvalidOperationException`을 발생시킵니다.

PlayerPrefs는 로컬 기본 동작과 샘플에 적합하지만 서버 권위 transaction이나 여러 장치 간 동기화를 보장하지 않습니다. 실제 결제·운영 보상은 프로젝트 어댑터로 교체해야 합니다. Unity `PlayerPrefs`를 사용하므로 기본 구현은 Unity 메인 스레드에서 호출하세요.

## 설치

현재 Cat Merge Cafe에서는 embedded package로 사용합니다. 수동 게시 후 다른 프로젝트의 `Packages/manifest.json`에는 다음 Git UPM 주소를 사용합니다.

```json
"com.actionfit.content-core": "https://github.com/ActionFit-Editor/ContentCore.git#0.2.3"
```

## Unity 메뉴

- Package root: `Tools > Package > Content Core`
- README: `Tools > Package > Content Core > README`

## 테스트

Unity Test Framework의 EditMode에서 `com.actionfit.content-core.Editor.Tests`를 실행합니다. 최신 revision 선택, 손상된 최신 슬롯 fallback, 삭제, 중복 transaction 멱등성, reward 잔액 합산을 검증합니다.

## Agent Skill 안내

- `$content-core-help`: 설치된 스킬 목록을 기준으로 opaque state store, 명시적 flush, two-slot fallback, 멱등 보상과 프로젝트 어댑터 경계를 설명합니다.
- `$content-core-audit`: PlayerPrefs나 저장값을 읽지 않고 소스에서 revision/hash fallback, flush, ledger reload, checked 합산, `GrantOnce` 멱등성과 원자적 어댑터 조건을 점검합니다.

두 스킬은 Codex와 Claude에 `read-only`로 등록됩니다. Custom Package Manager가 설치 대상의 `PACKAGE_SKILLS.md`를 생성하므로 패키지 소스에는 해당 파일을 직접 추가하지 않습니다.
