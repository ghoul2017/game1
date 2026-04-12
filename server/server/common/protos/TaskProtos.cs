using ProtoBuf;
using Server.Network;

namespace server.common.protos;

#region 查询任务列表

[ProtoContract]
public class QueryTaskListReq : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.QueryTaskListReq;
}

[ProtoContract]
public class QueryTaskListAck : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.QueryTaskListAck;
    [ProtoMember(1)] public int Code { get; set; }
    [ProtoMember(2)] public List<TaskInfo> Tasks { get; set; } = new();
}

#endregion

#region 接取任务

[ProtoContract]
public class TaskAcceptReq : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskAcceptReq;
    [ProtoMember(1)] public int TaskId { get; set; }
}

[ProtoContract]
public class TaskAcceptAck : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskAcceptAck;
    [ProtoMember(1)] public int Code { get; set; }
    [ProtoMember(2)] public TaskInfo Task { get; set; } = new();
}

#endregion

#region 提交任务

[ProtoContract]
public class TaskSubmitReq : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskSubmitReq;
    [ProtoMember(1)] public int TaskId { get; set; }
}

[ProtoContract]
public class TaskSubmitAck : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskSubmitAck;
    [ProtoMember(1)] public int Code { get; set; }
    [ProtoMember(2)] public int TaskId { get; set; }
    [ProtoMember(3)] public List<TaskRewardInfo> Rewards { get; set; } = new();
}

#endregion

#region 放弃任务

[ProtoContract]
public class TaskGiveUpReq : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskGiveUpReq;
    [ProtoMember(1)] public int TaskId { get; set; }
}

[ProtoContract]
public class TaskGiveUpAck : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskGiveUpAck;
    [ProtoMember(1)] public int Code { get; set; }
    [ProtoMember(2)] public int TaskId { get; set; }
}

#endregion

#region 任务变更通知

[ProtoContract]
public class TaskChangedNtf : IMessage
{
    public ushort MsgId => server.common.protos.MsgId.TaskChangedNtf;
    [ProtoMember(1)] public List<TaskInfo> Tasks { get; set; } = new();
}

#endregion

#region 通用任务信息

[ProtoContract]
public class TaskInfo
{
    [ProtoMember(1)] public int TaskId { get; set; }
    [ProtoMember(2)] public int State { get; set; }
    [ProtoMember(3)] public long AcceptTime { get; set; }
    [ProtoMember(4)] public long CompleteTime { get; set; }
    [ProtoMember(5)] public List<TaskGoalInfo> Goals { get; set; } = [];
}

[ProtoContract]
public class TaskGoalInfo
{
    [ProtoMember(1)] public int GoalType { get; set; }
    [ProtoMember(2)] public int GoalParam { get; set; }
    [ProtoMember(3)] public int CurrentCount { get; set; }
    [ProtoMember(4)] public int TargetCount { get; set; }
}

[ProtoContract]
public class TaskRewardInfo
{
    [ProtoMember(1)] public int ConfigId { get; set; }
    [ProtoMember(2)] public int Count { get; set; }
}

#endregion