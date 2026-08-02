using Client.Logic;
using Shared.Common.Protos;
using Xunit;

namespace Server.Tests;

public sealed class ExpeditionControllerTests
{
    [Fact]
    public void SnapshotConsumerIgnoresNonNewerStateAndHardCorrectsRoomChange()
    {
        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900001L;
        controller.ApplySnapshot(new ExpeditionSnapshotData
        {
            RunId = runId,
            ServerTick = 10L,
            CurrentRoomId = 1,
            Phase = 0,
            SquadX = 2.0f,
            SquadY = 3.0f
        });
        controller.ApplySnapshot(new ExpeditionSnapshotData
        {
            RunId = runId,
            ServerTick = 10L,
            CurrentRoomId = 1,
            Phase = 0,
            SquadX = 88.0f,
            SquadY = 88.0f
        });
        controller.ApplySnapshot(new ExpeditionSnapshotData
        {
            RunId = runId,
            ServerTick = 9L,
            CurrentRoomId = 1,
            Phase = 0,
            SquadX = 99.0f,
            SquadY = 99.0f
        });

        Assert.Equal(10L, controller.LatestSnapshot.ServerTick);
        Assert.Equal(2.0f, controller.PredictedSquadX);
        Assert.Equal(3.0f, controller.PredictedSquadY);

        controller.ApplySnapshot(new ExpeditionSnapshotData
        {
            RunId = runId,
            ServerTick = 11L,
            CurrentRoomId = 2,
            Phase = 0,
            SquadX = -4.0f,
            SquadY = 5.0f
        });
        Assert.Equal(-4.0f, controller.PredictedSquadX);
        Assert.Equal(5.0f, controller.PredictedSquadY);
    }

    [Fact]
    public void SuccessfulResumeWithoutActiveRunClearsStalePredictionAndInputs()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        controller.ApplySnapshot(new ExpeditionSnapshotData
        {
            RunId = 900002L,
            ServerTick = 20L,
            CurrentRoomId = 1,
            Phase = 0,
            SquadX = 5.0f,
            SquadY = 6.0f
        });
        controller.SendInput(1.0f, 0.0f);

        controller.ApplyResume(new ExpeditionResumeRes
        {
            HasActiveRun = false,
            Progress = new ExpeditionProgressData()
        });

        Assert.Null(controller.LatestSnapshot);
        Assert.Equal(0, controller.PendingInputCount);
        Assert.Equal(0.0f, controller.PredictedSquadX);
        Assert.Equal(0.0f, controller.PredictedSquadY);
    }

    [Fact]
    public void PredictionKeepsCurrentIntentAfterServerAcknowledgesPendingInput()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900003L;
        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 1L, 0L));
        controller.SendInput(1.0f, 0.0f);
        Assert.Equal(1, controller.PendingInputCount);

        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 2L, 1L));
        Assert.Equal(0, controller.PendingInputCount);
        controller.AdvanceRootPrediction(1.0f);

        Assert.Equal(4.5f, controller.PredictedSquadX);
        Assert.Equal(0.0f, controller.PredictedSquadY);
    }

    [Fact]
    public void LaggingSnapshotDoesNotPullPredictionBackwardAlongCurrentIntent()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900005L;
        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 1L, 0L));
        controller.SendInput(1.0f, 0.0f);
        controller.AdvanceRootPrediction(0.3f);
        float predictedBeforeSnapshot = controller.PredictedSquadX;

        ExpeditionSnapshotData laggingSnapshot = CreatePredictionSnapshot(runId, 2L, 1L);
        laggingSnapshot.SquadX = 0.9f;
        controller.ApplySnapshot(laggingSnapshot);

        Assert.Equal(0, controller.PendingInputCount);
        Assert.True(laggingSnapshot.SquadX < predictedBeforeSnapshot);
        Assert.True(controller.PredictedSquadX >= predictedBeforeSnapshot);
        Assert.Equal(0.0f, controller.PredictedSquadY);
    }

    [Fact]
    public void AuthorityStoppingStillCorrectsPredictionWhileInputRemainsActive()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900008L;
        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 1L, 0L));
        controller.SendInput(1.0f, 0.0f);
        controller.AdvanceRootPrediction(0.3f);

        ExpeditionSnapshotData movingSnapshot = CreatePredictionSnapshot(runId, 3L, 1L);
        movingSnapshot.SquadX = 0.9f;
        controller.ApplySnapshot(movingSnapshot);
        controller.AdvanceRootPrediction(0.2f);
        float predictedBeforeStoppedSnapshot = controller.PredictedSquadX;

        ExpeditionSnapshotData stoppedSnapshot = CreatePredictionSnapshot(runId, 5L, 1L);
        stoppedSnapshot.SquadX = movingSnapshot.SquadX;
        controller.ApplySnapshot(stoppedSnapshot);

        Assert.True(controller.PredictedSquadX < predictedBeforeStoppedSnapshot);
        Assert.True(controller.PredictedSquadX > stoppedSnapshot.SquadX);
    }

    [Fact]
    public void LargeAuthorityErrorHardCorrectsEvenWhileInputRemainsActive()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900009L;
        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 1L, 0L));
        controller.SendInput(1.0f, 0.0f);
        controller.AdvanceRootPrediction(1.0f);

        ExpeditionSnapshotData laggingSnapshot = CreatePredictionSnapshot(runId, 3L, 1L);
        laggingSnapshot.SquadX = 0.9f;
        controller.ApplySnapshot(laggingSnapshot);

        Assert.Equal(laggingSnapshot.SquadX, controller.PredictedSquadX);
        Assert.Equal(laggingSnapshot.SquadY, controller.PredictedSquadY);
    }

    [Fact]
    public void SnapshotLagSuppressionKeepsSubthresholdAuthorityErrorBounded()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900010L;
        ExpeditionSnapshotData initialSnapshot = CreatePredictionSnapshot(runId, 1L, 0L);
        initialSnapshot.SquadX = -2.0f;
        controller.ApplySnapshot(initialSnapshot);
        controller.SendInput(1.0f, 0.0f);
        controller.AdvanceRootPrediction(3.9f / initialSnapshot.MoveSpeedPerSecond);
        float predictedBeforeSnapshot = controller.PredictedSquadX;

        ExpeditionSnapshotData laggingSnapshot = CreatePredictionSnapshot(runId, 5L, 1L);
        controller.ApplySnapshot(laggingSnapshot);

        Assert.True(predictedBeforeSnapshot - laggingSnapshot.SquadX < 2.0f);
        Assert.True(controller.PredictedSquadX < predictedBeforeSnapshot);
        Assert.True(controller.PredictedSquadX > laggingSnapshot.SquadX);
    }

    [Fact]
    public void PredictionConvergesToAuthorityAfterMovementStops()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900006L;
        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 1L, 0L));
        controller.SendInput(1.0f, 0.0f);
        controller.AdvanceRootPrediction(0.3f);
        float predictedBeforeSnapshot = controller.PredictedSquadX;
        controller.SendInput(0.0f, 0.0f);

        ExpeditionSnapshotData stoppedSnapshot = CreatePredictionSnapshot(runId, 2L, 2L);
        stoppedSnapshot.SquadX = 0.9f;
        controller.ApplySnapshot(stoppedSnapshot);

        Assert.True(controller.PredictedSquadX < predictedBeforeSnapshot);
        Assert.True(controller.PredictedSquadX > stoppedSnapshot.SquadX);
        Assert.Equal(0.0f, controller.PredictedSquadY);

        float previousPrediction = controller.PredictedSquadX;
        for (long tick = 3L; tick <= 12L; tick++)
        {
            ExpeditionSnapshotData settlingSnapshot = CreatePredictionSnapshot(runId, tick, 2L);
            settlingSnapshot.SquadX = stoppedSnapshot.SquadX;
            controller.ApplySnapshot(settlingSnapshot);
            Assert.True(controller.PredictedSquadX < previousPrediction);
            Assert.True(controller.PredictedSquadX > settlingSnapshot.SquadX);
            previousPrediction = controller.PredictedSquadX;
        }

        Assert.InRange(controller.PredictedSquadX, stoppedSnapshot.SquadX, stoppedSnapshot.SquadX + 0.01f);
    }

    [Fact]
    public void RapidAnalogChangesAreCoalescedButStoppingIsImmediate()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        controller.ApplySnapshot(CreatePredictionSnapshot(900007L, 1L, 0L));

        controller.SendInput(1.0f, 0.0f);
        controller.SendInput(0.9f, 0.1f);
        Assert.Equal(1, controller.PendingInputCount);

        controller.SendInput(0.0f, 0.0f);
        Assert.Equal(2, controller.PendingInputCount);
    }

    [Fact]
    public void AlternatingMovementAndStopsStayBelowServerInputLimit()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        controller.ApplySnapshot(CreatePredictionSnapshot(900011L, 1L, 0L));

        for (int index = 0; index < 100; index++)
        {
            float directionX = index % 2 == 0 ? 1.0f : 0.0f;
            controller.SendInput(directionX, 0.0f);
        }

        Assert.Equal(28, controller.PendingInputCount);
    }

    [Fact]
    public void InputBudgetReservesImmediateStopAfterNonStopBurst()
    {
        if (NetClient.Instance == null)
        {
            Context.NetClient.Initialize();
        }

        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        controller.ApplySnapshot(CreatePredictionSnapshot(900012L, 1L, 0L));

        for (int interactionTargetId = 1; interactionTargetId <= 28; interactionTargetId++)
        {
            controller.SendInput(1.0f, 0.0f, interactionTargetId);
        }

        Assert.Equal(28, controller.PendingInputCount);
        controller.SendInput(0.0f, 0.0f, 28);
        Assert.Equal(29, controller.PendingInputCount);
        controller.SendInput(1.0f, 0.0f, 28);
        Assert.Equal(29, controller.PendingInputCount);
    }

    [Fact]
    public void EventBatchConsumerRejectsDuplicatesAndOldRuns()
    {
        ExpeditionController controller = ExpeditionController.Instance;
        controller.ResetForRole();
        long runId = 900004L;
        controller.ApplySnapshot(CreatePredictionSnapshot(runId, 1L, 0L));
        int receivedCount = 0;
        System.Action<ExpeditionEventBatchNtf> handler = batch => receivedCount += batch.Events.Count;
        controller.EventBatchReceived += handler;
        try
        {
            controller.ApplyEventBatch(CreateBatch(runId, 1L));
            controller.ApplyEventBatch(CreateBatch(runId, 1L));
            controller.ApplyEventBatch(CreateBatch(runId - 1L, 2L));
            controller.ApplyEventBatch(CreateBatch(runId, 2L));
        }
        finally
        {
            controller.EventBatchReceived -= handler;
        }

        Assert.Equal(2, receivedCount);
        Assert.Equal(2L, controller.LastEventSequence);
    }

    private static ExpeditionSnapshotData CreatePredictionSnapshot(
        long runId,
        long tick,
        long acknowledgedSequence)
    {
        return new ExpeditionSnapshotData
        {
            RunId = runId,
            ServerTick = tick,
            LastProcessedInputSequence = acknowledgedSequence,
            CurrentRoomId = 1,
            Phase = 0,
            SquadX = 0.0f,
            SquadY = 0.0f,
            TicksPerSecond = 10,
            MoveSpeedPerSecond = 4.5f,
            MinimumX = -10.0f,
            MaximumX = 10.0f,
            MinimumY = -25.0f,
            MaximumY = 28.0f
        };
    }

    private static ExpeditionEventBatchNtf CreateBatch(long runId, long sequence)
    {
        ExpeditionEventBatchNtf batch = new ExpeditionEventBatchNtf
        {
            RunId = runId,
            EventSequence = sequence
        };
        batch.Events.Add(new ExpeditionEventData { Tick = sequence, Kind = 1 });
        return batch;
    }
}
