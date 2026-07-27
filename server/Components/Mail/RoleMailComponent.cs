using System.Collections.Generic;
using Game.Components.Player;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Shared.Core.ECS;

namespace Game.Components.Mail;

/// <summary>
/// 角色邮件组件。作为 PlayerEntity 完整文档的嵌入数据保存。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleMailComponent : Component
{
    /// <summary>邮件字典：mailId → RoleMail</summary>
    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public Dictionary<long, RoleMail> Mails { get; set; } = new();
}
