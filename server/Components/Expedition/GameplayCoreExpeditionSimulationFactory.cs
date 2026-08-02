using System;
using System.Collections.Generic;
using GameplayCore;
using ProductExpedition;
using Shared.Common.Protos;

namespace Game.Components.Expedition;

/// <summary>
/// GameplayCore 的服务端权威适配器。内容创建、规则推进和协议投影均不接受客户端结果数据。
/// </summary>
public sealed class GameplayCoreExpeditionSimulationFactory : IExpeditionSimulationFactory
{
    public const int TicksPerSecond = 10;
    public const int RoomCount = ProductExpeditionFactory.DefaultRoomCount;
    public const int UpgradeCost = ProductExpeditionFactory.DefaultUpgradeCost;
    public const int UpgradeDamagePercent = ProductExpeditionFactory.DefaultUpgradeDamagePercent;

    private readonly string _productId;

    public GameplayCoreExpeditionSimulationFactory()
        : this(ProductExpeditionRun.DefaultProductId)
    {
    }

    public GameplayCoreExpeditionSimulationFactory(string productId)
    {
        ProductExpeditionRun.ValidateProductId(productId);
        _productId = productId;
    }

    public string ProductId => _productId;

    public IAuthoritativeExpeditionSimulation Create(ExpeditionSimulationCreateContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.RunId <= 0L || context.RunIndex <= 0L || context.Seed == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "远征标识、局序号和种子必须有效");
        }

        if (context.FacilityLevel < 0 || context.FacilityLevel > PlayerExpeditionSystem.MaximumFacilityLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(context.FacilityLevel));
        }

        ProductExpeditionRun productRun = ProductExpeditionFactory.CreateRun(
            _productId,
            TicksPerSecond,
            context.RunId,
            context.Seed,
            context.FacilityLevel);
        return new GameplayCoreExpeditionSimulation(productRun);
    }

    public static int GetRepresentativeDamage(int facilityLevel)
    {
        if (facilityLevel < 0 || facilityLevel > PlayerExpeditionSystem.MaximumFacilityLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(facilityLevel));
        }

        return ProductExpeditionFactory.GetRepresentativeDamage(
            ProductExpeditionRun.DefaultProductId,
            facilityLevel);
    }
}

internal sealed class GameplayCoreExpeditionSimulation : IAuthoritativeExpeditionSimulation
{
    private readonly List<ExpeditionEvent> _events = new List<ExpeditionEvent>();
    private readonly ProductExpeditionRun _productRun;
    private readonly ExpeditionRun _run;

    internal GameplayCoreExpeditionSimulation(ProductExpeditionRun productRun)
    {
        _productRun = productRun ?? throw new ArgumentNullException(nameof(productRun));
        _run = productRun.Run;
    }

    internal GameplayCoreExpeditionSimulation(ExpeditionRun run)
        : this(new ProductExpeditionRun(run, ProductExpeditionRun.DefaultProductId))
    {
    }

    public long Tick => _run.Tick;

    public bool HasEnded => _run.HasEnded;

    public int Phase => (int)_run.Phase;

    public long GrossResource => _run.Result == null ? _run.CollectedResource : _run.Result.CollectedResource;

    public void Advance(
        float directionX,
        float directionY,
        int interactionTargetId,
        ICollection<ExpeditionEventData> outputEvents)
    {
        if (outputEvents == null)
        {
            throw new ArgumentNullException(nameof(outputEvents));
        }

        _events.Clear();
        _productRun.Advance(directionX, directionY, interactionTargetId, _events);
        for (int index = 0; index < _events.Count; index++)
        {
            outputEvents.Add(ToProtocol(_events[index]));
        }
    }

    public ExpeditionSnapshotData CaptureSnapshot(
        long runId,
        long runIndex,
        long lastProcessedInputSequence,
        IReadOnlyList<ExpeditionEventData> events)
    {
        ExpeditionSnapshotData snapshot = new ExpeditionSnapshotData
        {
            RunId = runId,
            RunIndex = runIndex,
            ServerTick = _run.Tick,
            LastProcessedInputSequence = lastProcessedInputSequence,
            Phase = (int)_run.Phase,
            SquadX = _run.SquadX,
            SquadY = _run.SquadY,
            CurrentRoomId = _run.CurrentRoomId,
            TargetKind = (int)_run.TargetKind,
            TargetId = _run.TargetId,
            Kills = _run.Kills,
            CollectedResource = _run.CollectedResource,
            ExitOpen = _run.ExitOpen,
            TicksPerSecond = _run.TicksPerSecond,
            MoveSpeedPerSecond = _run.MoveSpeedPerSecond,
            MinimumX = _run.MinimumX,
            MaximumX = _run.MaximumX,
            MinimumY = _run.MinimumY,
            MaximumY = _run.MaximumY
        };

        AddMembers(snapshot.Members);
        AddEnemies(snapshot.Enemies);
        AddResources(snapshot.Resources);
        AddRevealedRooms(snapshot.RevealedRooms);
        for (int index = 0; index < events.Count; index++)
        {
            snapshot.Events.Add(events[index]);
        }

        return snapshot;
    }

    private void AddMembers(ICollection<ExpeditionMemberStateData> output)
    {
        for (int index = 0; index < _run.Members.Count; index++)
        {
            ExpeditionMember member = _run.Members[index];
            output.Add(new ExpeditionMemberStateData
            {
                Id = member.Id,
                Health = member.Health,
                MaxHealth = member.MaxHealth,
                AttackCooldownTicksRemaining = member.AttackCooldownTicksRemaining
            });
        }
    }

    private void AddEnemies(ICollection<ExpeditionEnemyStateData> output)
    {
        for (int index = 0; index < _run.Enemies.Count; index++)
        {
            ExpeditionEnemy enemy = _run.Enemies[index];
            output.Add(new ExpeditionEnemyStateData
            {
                Id = enemy.Id,
                Kind = (int)enemy.Kind,
                X = enemy.X,
                Y = enemy.Y,
                Health = enemy.Health,
                MaxHealth = enemy.MaxHealth,
                ActionCooldownTicksRemaining = enemy.ActionCooldownTicksRemaining,
                WarningActive = enemy.WarningActive,
                WarningResolveTick = enemy.WarningResolveTick,
                WarningX = enemy.WarningX,
                WarningY = enemy.WarningY,
                WarningRadius = enemy.BossWarningRadius
            });
        }
    }

    private void AddResources(ICollection<ExpeditionResourceStateData> output)
    {
        for (int index = 0; index < _run.Resources.Count; index++)
        {
            ExpeditionResource resource = _run.Resources[index];
            output.Add(new ExpeditionResourceStateData
            {
                Id = resource.Id,
                X = resource.X,
                Y = resource.Y,
                RemainingAmount = resource.RemainingAmount,
                InitialAmount = resource.InitialAmount,
                CollectionCooldownTicksRemaining = resource.CollectionCooldownTicksRemaining
            });
        }
    }

    private void AddRevealedRooms(ICollection<ExpeditionRoomStateData> output)
    {
        for (int index = 0; index < _run.DiscoveredRooms.Count; index++)
        {
            ExpeditionRoomPublicState room = _run.DiscoveredRooms[index];
            ExpeditionRoomStateData roomData = new ExpeditionRoomStateData
            {
                RoomId = room.Id,
                Kind = (int)room.Kind,
                State = (int)room.Progress,
                IsCurrent = room.IsCurrent
            };

            if (room.IsCurrent)
            {
                for (int exitIndex = 0; exitIndex < _run.VisibleExits.Count; exitIndex++)
                {
                    ExpeditionVisibleExit exit = _run.VisibleExits[exitIndex];
                    roomData.VisibleConnectedRoomIds.Add(exit.TargetRoomId);
                    roomData.VisibleExits.Add(new ExpeditionVisibleExitData
                    {
                        Direction = (int)exit.Direction,
                        TargetRoomId = exit.TargetRoomId,
                        IsOpen = exit.IsOpen,
                        HasDangerHint = exit.HasDangerHint,
                        X = exit.X,
                        Y = exit.Y
                    });
                }
            }

            output.Add(roomData);
        }
    }

    private static ExpeditionEventData ToProtocol(ExpeditionEvent value)
    {
        return new ExpeditionEventData
        {
            Tick = value.Tick,
            Kind = (int)value.Kind,
            SourceKind = (int)value.SourceKind,
            SourceId = value.SourceId,
            TargetKind = (int)value.TargetKind,
            TargetId = value.TargetId,
            Value = value.Value,
            X = value.X,
            Y = value.Y
        };
    }
}
