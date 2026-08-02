using System;
using GameplayCore;
using Game.Components.Player;

namespace Game.Components.Arena;

public static class ArenaSnapshotMapper
{
    private const int RulesVersion = 1;
    private const int ContentVersion = 1;

    public static ArenaSquadSnapshot CreateAttacker(PlayerEntity player)
    {
        PlayerExpeditionComponent expedition = player.GetComponent<PlayerExpeditionComponent>();
        int facilityLevel = expedition.FacilityLevel;
        if (facilityLevel < 0 || facilityLevel > 1)
        {
            throw new InvalidOperationException("远征设施等级无法映射为竞技场快照");
        }

        return new ArenaSquadSnapshot(
            $"role-{player.Id}-facility-{facilityLevel}-arena-v1",
            RulesVersion,
            ContentVersion,
            new[]
            {
                new ArenaUnitSnapshot(1, 1001, 0, 120, ApplyFacilityDamage(18, facilityLevel), 2),
                new ArenaUnitSnapshot(2, 1002, 1, 95, ApplyFacilityDamage(22, facilityLevel), 3),
                new ArenaUnitSnapshot(3, 1003, 2, 105, ApplyFacilityDamage(16, facilityLevel), 2)
            });
    }

    public static ArenaSquadSnapshot CreateFixedDefender()
    {
        return new ArenaSquadSnapshot(
            "fixed-defender-v1",
            RulesVersion,
            ContentVersion,
            new[]
            {
                new ArenaUnitSnapshot(101, 2001, 0, 130, 17, 2),
                new ArenaUnitSnapshot(102, 2002, 1, 100, 20, 3),
                new ArenaUnitSnapshot(103, 2003, 2, 90, 15, 2)
            });
    }

    public static ArenaBattleSetup CreateBattleSetup(
        uint seed,
        ArenaSquadSnapshot attacker,
        ArenaSquadSnapshot defender)
    {
        return new ArenaBattleSetup(
            seed,
            10,
            1_200,
            1_024,
            20,
            attacker,
            defender);
    }

    private static int ApplyFacilityDamage(int baseDamage, int facilityLevel)
    {
        return baseDamage * (100 + facilityLevel * 20) / 100;
    }
}
