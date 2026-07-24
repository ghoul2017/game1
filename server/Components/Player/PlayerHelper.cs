namespace Game.Components.Player;

public static class PlayerHelper
{
    /// <summary>根据经验计算等级（每级需 100 × level 点经验）</summary>
    public static int CalculateLevel(int exp)
    {
        var level = 1;
        var totalExp = 0;

        while (true)
        {
            var required = 100 * level;
            if (totalExp + required > exp)
                return level;

            totalExp += required;
            level++;
            if (level >= 100)
                return level;
        }
    }
}
