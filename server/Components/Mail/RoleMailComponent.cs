using System.Collections.Generic;
using Game.Components.Player;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Shared.Core.Database;
using Shared.Core.ECS;

namespace Game.Components.Mail;

/// <summary>
/// 角色邮件组件。持久化到 MongoDB（集合名 role_mail_component）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleMailComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>邮件字典：mailId → RoleMail</summary>
    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public Dictionary<long, RoleMail> Mails { get; set; } = new();
}
