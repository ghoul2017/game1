using System;

namespace Game.Database;

/// <summary>玩家账号数据（MongoDB 文档映射）</summary>
public class PlayerAccount
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public DateTime CreateTime { get; set; }
}
