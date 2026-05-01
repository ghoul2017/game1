namespace Game.Components.User;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public DateTime CreateTime { get; set; }
}