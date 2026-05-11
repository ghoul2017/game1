using System.Collections.Generic;
using Game.Components.Player;
using Shared.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.ECS;

namespace Game.Components.Mail;

/// <summary>
/// 角色邮件组件。持久化到 MongoDB（集合名 role_mail_list）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleMailComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>邮件列表</summary>
    public List<RoleMail> Mails { get; set; } = [];

    /// <summary>邮件ID生成器</summary>
    [BsonIgnore]
    public long NextMailId = 1;

    /// <summary>运行时索引：mailId → RoleMail（不序列化）</summary>
    [BsonIgnore]
    private Dictionary<long, RoleMail>? _mailMap;

    internal void AfterDeserialize() { RebuildIndex(); }

    internal void RebuildIndex()
    {
        _mailMap = new Dictionary<long, RoleMail>(Mails.Count);
        foreach (var mail in Mails)
        {
            _mailMap[mail.Id] = mail;
        }
    }

    internal Dictionary<long, RoleMail> GetMailMap()
    {
        _mailMap ??= new Dictionary<long, RoleMail>(Mails.Count);
        return _mailMap;
    }
}
