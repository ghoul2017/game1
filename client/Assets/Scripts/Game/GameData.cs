using UnityEngine;

namespace Client.Game
{
    public class RoleData
    {
        public long RoleId;
        public string Name;
        public int Level;
        public string Job;
    }

    public static class GameData
    {
        private static readonly string[] JobNames = { "战士", "法师", "弓手", "刺客" };

        public static string GetJobName(int jobId)
        {
            if (jobId >= 0 && jobId < JobNames.Length)
                return JobNames[jobId];
            return "未知";
        }

        public static int GetJobId(string jobName)
        {
            for (int i = 0; i < JobNames.Length; i++)
            {
                if (JobNames[i] == jobName)
                    return i;
            }
            return 0;
        }

        public static RoleData CurrentRole { get; set; }
        public static long PlayerId { get; set; }
        public static string Token { get; set; } = "";
    }
}
