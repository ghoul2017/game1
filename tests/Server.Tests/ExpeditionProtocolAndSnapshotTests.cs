using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Expedition;
using GameplayCore;
using ProductExpedition;
using Shared.Common.Protos;
using Xunit;

namespace Server.Tests;

public sealed class ExpeditionProtocolAndSnapshotTests
{
    [Fact]
    public void InputProtocolContainsOnlyQuantizedIntentAndSequence()
    {
        string[] actualProperties = typeof(ExpeditionInputNtf)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expectedProperties =
        {
            nameof(ExpeditionInputNtf.DirectionX),
            nameof(ExpeditionInputNtf.DirectionY),
            nameof(ExpeditionInputNtf.InputSequence),
            nameof(ExpeditionInputNtf.InteractionTargetId),
            nameof(ExpeditionInputNtf.RunId)
        };
        Array.Sort(expectedProperties, StringComparer.Ordinal);

        Assert.Equal(expectedProperties, actualProperties);
    }

    [Fact]
    public void InitialSnapshotExposesCurrentAndDiscoveredRoomsButNotFullGraphOrSeed()
    {
        GameplayCoreExpeditionSimulationFactory factory = new GameplayCoreExpeditionSimulationFactory();
        IAuthoritativeExpeditionSimulation simulation = factory.Create(new ExpeditionSimulationCreateContext
        {
            UserId = 1L,
            RoleId = 2L,
            RunId = 3L,
            RunIndex = 1L,
            Seed = 123456u,
            FacilityLevel = 0
        });

        ExpeditionSnapshotData snapshot = simulation.CaptureSnapshot(
            3L,
            1L,
            0L,
            Array.Empty<ExpeditionEventData>());

        Assert.Equal(3L, snapshot.RunId);
        Assert.Equal(GameplayCoreExpeditionSimulationFactory.TicksPerSecond, snapshot.TicksPerSecond);
        Assert.Equal(5.5f, snapshot.MoveSpeedPerSecond);
        Assert.Equal(-6.0f, snapshot.MinimumX);
        Assert.Equal(6.0f, snapshot.MaximumX);
        Assert.Equal(-6.0f, snapshot.MinimumY);
        Assert.Equal(6.0f, snapshot.MaximumY);
        Assert.Equal(ProductExpeditionFactory.DefaultUpgradeCost, ExpeditionComponent.DefaultUpgradeCost);
        Assert.InRange(snapshot.RevealedRooms.Count, 1, GameplayCoreExpeditionSimulationFactory.RoomCount - 1);
        Assert.Single(snapshot.RevealedRooms, room => room.IsCurrent);
        Assert.DoesNotContain(
            typeof(ExpeditionSnapshotData).GetProperties(),
            property => property.Name.Contains("Seed", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Graph", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnemyProjectionIncludesAuthoritativeWarningRadius()
    {
        ExpeditionSetup setup = new ExpeditionSetup
        {
            RunId = 4L,
            Seed = 123u,
            TicksPerSecond = 10,
            MoveSpeedPerSecond = 4.0f,
            MinimumX = -10.0f,
            MaximumX = 10.0f,
            MinimumY = -10.0f,
            MaximumY = 10.0f,
            InitialSquadX = 0.0f,
            InitialSquadY = -5.0f,
            TargetAcquireRadius = 3.0f,
            TargetReleaseMargin = 1.0f,
            EnemyThreatRadius = 3.0f,
            ExitX = 0.0f,
            ExitY = 8.0f,
            ExitRadius = 1.0f,
            Members = new[]
            {
                new ExpeditionMemberSetup
                {
                    Id = 1,
                    MaxHealth = 100,
                    AttackDamage = 8,
                    AttackRange = 2.0f,
                    AttackIntervalTicks = 5
                }
            },
            Enemies = new[]
            {
                new ExpeditionEnemySetup
                {
                    Id = 900,
                    Kind = ExpeditionEnemyKind.Boss,
                    X = 0.0f,
                    Y = 2.0f,
                    MaxHealth = 100,
                    AttackDamage = 10,
                    AttackRange = 3.0f,
                    ActionIntervalTicks = 10,
                    BossWarningDelayTicks = 5,
                    BossWarningRadius = 2.25f
                }
            },
            Resources = new[]
            {
                new ExpeditionResourceSetup
                {
                    Id = 200,
                    X = 0.0f,
                    Y = 0.0f,
                    Amount = 10,
                    CollectionAmount = 1,
                    InteractionRange = 2.0f,
                    CollectionIntervalTicks = 5
                }
            }
        };
        GameplayCoreExpeditionSimulation simulation = new GameplayCoreExpeditionSimulation(
            ExpeditionRun.Start(setup));

        ExpeditionSnapshotData snapshot = simulation.CaptureSnapshot(
            4L,
            1L,
            0L,
            Array.Empty<ExpeditionEventData>());

        ExpeditionEnemyStateData boss = Assert.Single(snapshot.Enemies);
        Assert.Equal(2.25f, boss.WarningRadius);
    }

    [Fact]
    public void TimedProductCanBeSelectedWithoutChangingServerHost()
    {
        GameplayCoreExpeditionSimulationFactory factory = new GameplayCoreExpeditionSimulationFactory(
            ProductExpeditionRun.TimedPlaceholderProductId);
        IAuthoritativeExpeditionSimulation simulation = factory.Create(new ExpeditionSimulationCreateContext
        {
            UserId = 1L,
            RoleId = 2L,
            RunId = 3L,
            RunIndex = 1L,
            Seed = 123456u,
            FacilityLevel = 0
        });
        List<ExpeditionEventData> events = new List<ExpeditionEventData>();

        for (int tick = 0; tick < 1_800; tick++)
        {
            events.Clear();
            simulation.Advance(0.0f, 0.0f, 0, events);
        }

        Assert.Equal(ProductExpeditionRun.TimedPlaceholderProductId, factory.ProductId);
        Assert.True(simulation.HasEnded);
        Assert.Equal(2, simulation.Phase);
        Assert.Equal(1_800L, simulation.Tick);
    }
}
