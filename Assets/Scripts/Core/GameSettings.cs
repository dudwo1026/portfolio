// 씬을 넘어 들고 가는 선택값. 타이틀의 난이도·무기 선택이 채우고, 게임 씬이 읽는다
public enum PrimaryWeapon { Rifle, Laser, Shotgun }
public enum Difficulty { Normal, Hard }

public static class GameSettings
{
    // 타이틀을 거치지 않고 게임 씬을 바로 플레이하면 false — 그때는 인스펙터 값을 그대로 쓴다
    public static bool primaryChosen;
    public static PrimaryWeapon primary = PrimaryWeapon.Rifle;

    // 하드 = 씬·프리팹에 저장된 값 그대로. 게임 씬을 바로 플레이하면(테스트) 하드다
    public static Difficulty difficulty = Difficulty.Hard;
}
