using System;
using Game.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.Network;

namespace Game.Components.User;

public class User : IEntity
{
    [BsonId]
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public DateTime CreateTime { get; set; }

    [BsonIgnore]
    public TcpSession? Session { get; set; }
}
