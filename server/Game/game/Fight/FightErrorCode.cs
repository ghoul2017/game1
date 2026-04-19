namespace Game.Game.Fight;

/// <summary>
/// 战斗错误码。
/// </summary>
public static class FightErrorCode
{
    public const int Ok = 0;
    public const int NotInFight = 1;
    public const int AlreadyDead = 2;
    public const int SkillNotFound = 3;
    public const int SkillConfigNotFound = 4;
    public const int SkillInCd = 5;
    public const int TargetNotFound = 6;
    public const int TargetDead = 7;
    public const int OutOfRange = 8;
    public const int NotEnoughMp = 9;
}
