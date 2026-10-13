// 보스 패턴이 지금 자기 모션을 재생 중인지 알린다.
// EnemySpriteAnimator가 스프라이트 셋의 motions에서 같은 이름을 찾아 그린다.
//
// 진행도: 0~1 = 타격 전(선딜), 1~2 = 타격 후(후딜).
// 셋의 Motion.hitFrame부터의 장은 진행도가 1을 넘은 뒤(= 판정이 생긴 뒤)에만 나온다
public interface IBossMotion
{
    string MotionName { get; }
    bool MotionActive { get; }
    float MotionProgress { get; }
}
