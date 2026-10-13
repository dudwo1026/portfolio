using UnityEngine;

// 잡몹 한 종류의 그림 묶음. 고스트·버섯·슬라임처럼 "뼈대는 같고 그림만 다른" 몹을
// 만들기 위한 에셋이다. 몹 프리팹은 Enemy 하나뿐이고, 스폰할 때 이 셋 중 하나를 뽑아 쓴다.
// 새 종류를 추가하려면 스크립트를 건드릴 필요 없이 이 에셋만 하나 더 만들면 된다.
//
// 그림마다 해상도도, 그 안에서 몹이 차지하는 영역도 제각각이라
// 프레임을 바꿀 때 쿼드 크기와 위치를 같이 보정해야 키가 들쭉날쭉하거나 발이 뜨지 않는다.
[CreateAssetMenu(menuName = "Game/Enemy Sprite Set", fileName = "EnemySpriteSet")]
public class EnemySpriteSet : ScriptableObject
{
    [System.Serializable]
    public class Frame
    {
        public Texture2D texture;
        [Tooltip("그림 속 몹의 높이(px)")]
        public float charHeightPx = 1f;
        [Tooltip("그림 아래 끝에서 몹의 발까지의 여백(px)")]
        public float charBottomPx;
        [Tooltip("몹 발의 가로 중심(px)")]
        public float footCenterPx;
        [Tooltip("미세 보정. 포즈마다 실루엣 높이가 달라 체감 크기가 어긋날 때 이걸로 맞춘다")]
        public float sizeScale = 1f;
    }

    public Frame idle;
    [Tooltip("여러 장으로 된 대기 모션. 넣으면 idle 대신 이걸 돌린다 (보스)")]
    public Frame[] idleFrames;
    public float idleFramesPerSecond = 4f;
    [Tooltip("1→N→1로 왕복한다. 장끼리 미세하게 다른 그림이라 N→1로 바로 넘어가면 툭 튄다")]
    public bool idlePingPong = true;
    [Tooltip("대기 중 숨쉬기. 세로로 이 비율만큼 늘었다 줄어든다(가로는 절반만큼 반대로). 0이면 없음. 발은 제자리")]
    public float breatheAmount = 0f;
    public float breathePeriod = 2.4f;
    [Tooltip("이동 중 순환. 한 장만 넣으면 그 자세로 고정된다")]
    public Frame[] moveFrames;
    [Tooltip("공격 모션. 선딜 전체에 걸쳐 고르게 펼쳐 재생하므로 장수는 자유다")]
    public Frame[] attackFrames;

    [Header("정예 전용 (없으면 비워 둔다)")]
    [Tooltip("돌진 모션. 순서대로 한 번 재생한 뒤 마지막 장에서 멈춰 돌진이 끝날 때까지 유지한다. " +
             "돌진 길이가 매번 다르기 때문에 순환시키지 않는다")]
    public Frame[] chargeFrames;
    [Tooltip("돌진 모션 전환 속도. 마지막 장까지 가는 데 (장수-1)/이 값 만큼 걸린다")]
    public float chargeFramesPerSecond = 10f;
    [Tooltip("길이가 정해진 패턴에서 장 간격을 고르지 않게 만든다. " +
             "1 = 등간격, 1보다 크면 뒤로 갈수록 간격이 짧아진다(도화선이 타들어가듯 가속). " +
             "1보다 작으면 뒤로 갈수록 느려진다")]
    public float chargeFrameEasing = 1f;
    [Tooltip("기절 자세. 돌진이 벽에 박힌 뒤 3~5초 동안 쓰인다")]
    public Frame stun;

    [System.Serializable]
    public class Motion
    {
        [Tooltip("IBossMotion.MotionName과 같은 이름")]
        public string name;
        public Frame[] frames;
        [Tooltip("이 장(0부터)부터는 타격 판정이 생긴 뒤에만 나온다. 0이면 구분 없이 진행도대로 펼친다")]
        public int hitFrame;
        [Tooltip("이 장(0부터)을 진행도 1~2 동안 붙잡아 둔다 (레이저 발사 유지). " +
                 "0~1은 그 앞 장들, 2~3은 그 뒤 장들에 펼친다. 0이면 안 쓴다(hitFrame 방식)")]
        public int holdFrame;
    }

    [Header("보스 패턴 모션 (없으면 비워 둔다)")]
    public Motion[] motions;

    [Header("크기")]
    [Tooltip("그려진 몹의 기준 키(월드 단위). 캡슐 높이 2와 맞춰 두면 콜라이더와 겹친다")]
    public float characterHeight = 2f;
    [Tooltip("카메라를 마주 보는 판은 기울어 있어서, 세로로 긴 장(빛기둥 등)일수록 발이 카메라 쪽 아래로 밀린다. " +
             "켜면 대기 첫 장의 높이를 기준으로 그 차이를 되돌려 모든 장의 발을 같은 자리에 둔다 (보스)")]
    public bool anchorFeetToIdle = false;

    [Header("이동 애니메이션")]
    [Tooltip("걸을 때 프레임 전환 속도")]
    public float moveFramesPerSecond = 6f;
    [Tooltip("켜면 실제 이동 속도에 비례해 전환이 빨라진다. 배회(2.5)와 추격(5)의 발놀림이 달라진다")]
    public bool scaleFpsBySpeed = true;
    [Tooltip("위 비례의 기준 속도. 이 속도일 때 moveFramesPerSecond 그대로 돈다")]
    public float referenceSpeed = 2.5f;

    [Header("좌우 반전")]
    [Tooltip("원본 그림이 화면 왼쪽을 보고 있으면 켠다. 반대로 나오면 끄면 된다")]
    public bool artFacesScreenLeft = true;
    [Tooltip("켜면 이동·조준과 상관없이 모든 모션이 항상 화면 왼쪽을 본다 (보스)")]
    public bool alwaysFaceScreenLeft = false;
    [Tooltip("넉백 중 밀려나는 쪽을 보게 한다. 슬라임의 궤적, 고스트의 불꽃처럼 " +
             "진행 방향이 그림에 드러나는 몹에 켠다. 끄면 맞기 직전 방향(= 플레이어 쪽)을 그대로 유지한다")]
    public bool faceKnockbackDirection = false;
    [Tooltip("공격 그림만 idle·move와 좌우가 반대로 그려졌을 때 켠다. " +
             "그릴 때만 뒤집으므로 보는 방향 자체는 그대로다")]
    public bool attackArtFlipped = false;
}
