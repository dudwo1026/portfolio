// 정예몹의 특수 패턴이 "지금 나가는 중"인지를 스프라이트 쪽에 알려 주는 창구.
//
// 패턴마다 스크립트가 다르고 내부 상태 이름도 제각각이라(돌진은 charging, 넉백은 casting),
// EnemySpriteAnimator가 각 스크립트를 일일이 알면 정예를 하나 추가할 때마다 애니메이터를
// 고쳐야 한다. 그래서 이 인터페이스만 보게 하고, 패턴 쪽에서 두 줄 구현하게 한다.
public interface IElitePatternVisual
{
    bool PatternActive { get; }

    // 패턴 전체 길이 대비 진행도(0~1).
    // 길이를 미리 알 수 없는 패턴 — 돌진처럼 벽에 박을 때까지 계속되는 것 — 은 -1을 돌려준다.
    // 그 경우 애니메이터가 고정 속도로 1회 재생한 뒤 마지막 장에서 멈춰 유지한다.
    float PatternProgress { get; }
}
