using ProtoBuf;
using Shared.Core.Network;
using System.Collections.Generic;

namespace Shared.Common.Protos
{

    [ProtoContract]
    [Message("Task.QueryTaskListReq")]
    public class QueryTaskListReq : IMessage
    {
    }

    [ProtoContract]
    [Message("Task.QueryTaskListAck")]
    public class QueryTaskListAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public List<TaskInfo> Tasks { get; set; } = new List<TaskInfo>();
    }



    [ProtoContract]
    [Message("Task.TaskAcceptReq")]
    public class TaskAcceptReq : IMessage
    {
        [ProtoMember(1)] public int TaskId { get; set; }
    }

    [ProtoContract]
    [Message("Task.TaskAcceptAck")]
    public class TaskAcceptAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public TaskInfo Task { get; set; } = new TaskInfo();
    }



    [ProtoContract]
    [Message("Task.TaskSubmitReq")]
    public class TaskSubmitReq : IMessage
    {
        [ProtoMember(1)] public int TaskId { get; set; }
    }

    [ProtoContract]
    [Message("Task.TaskSubmitAck")]
    public class TaskSubmitAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public int TaskId { get; set; }
        [ProtoMember(3)] public List<ItemTipsData> Rewards { get; set; } = new List<ItemTipsData>();
    }



    [ProtoContract]
    [Message("Task.TaskGiveUpReq")]
    public class TaskGiveUpReq : IMessage
    {
        [ProtoMember(1)] public int TaskId { get; set; }
    }

    [ProtoContract]
    [Message("Task.TaskGiveUpAck")]
    public class TaskGiveUpAck : IMessage
    {
        [ProtoMember(1)] public string ErrorMessage { get; set; } = "";
        [ProtoMember(2)] public int TaskId { get; set; }
    }



    [ProtoContract]
    [Message("Task.TaskChangedNtf")]
    public class TaskChangedNtf : IMessage
    {
        [ProtoMember(1)] public List<TaskInfo> Tasks { get; set; } = new List<TaskInfo>();
    }



    [ProtoContract]
    public class TaskInfo
    {
        [ProtoMember(1)] public int TaskId { get; set; }
        [ProtoMember(2)] public int State { get; set; }
        [ProtoMember(3)] public long AcceptTime { get; set; }
        [ProtoMember(4)] public long CompleteTime { get; set; }
        [ProtoMember(5)] public List<TaskGoalInfo> Goals { get; set; } = new List<TaskGoalInfo>();
    }

    [ProtoContract]
    public class TaskGoalInfo
    {
        [ProtoMember(1)] public int GoalType { get; set; }
        [ProtoMember(2)] public int GoalParam { get; set; }
        [ProtoMember(3)] public int CurrentCount { get; set; }
        [ProtoMember(4)] public int TargetCount { get; set; }
    }

}
