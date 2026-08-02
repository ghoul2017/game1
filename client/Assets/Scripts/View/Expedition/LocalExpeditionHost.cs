using System;
using System.Collections.Generic;
using GameplayCore;
using ProductExpedition;
using UnityEngine;

namespace Client.Expedition
{
    /// <summary>
    /// 把 Unity 帧时间转换为有追赶上限的固定 Tick，并把结果交给表现层。
    /// </summary>
    public sealed class LocalExpeditionHost : MonoBehaviour
    {
        private const int MaximumTicksPerFrame = 5;

        private readonly List<ExpeditionEvent> _events = new List<ExpeditionEvent>();
        private float _accumulatedSeconds;
        private bool _droppedTickWarningIssued;
        private ExpeditionHud _hud;
        private ExpeditionInputView _input;
        private ProductExpeditionRun _productRun;
        private ExpeditionRun _run;
        private float _secondsPerTick;
        private ExpeditionWorldView _world;

        public ExpeditionRun Run => _run;

        public ExpeditionWorldView World => _world;

        public ExpeditionHud Hud => _hud;

        public ExpeditionInputView InputView => _input;

        public ProductExpeditionRun ProductRun => _productRun;

        public void Initialize(
            ExpeditionRun run,
            ExpeditionInputView input,
            ExpeditionWorldView world,
            ExpeditionHud hud)
        {
            Initialize(run, input, world, hud, ProductExpeditionRun.DefaultProductId);
        }

        public void Initialize(
            ExpeditionRun run,
            ExpeditionInputView input,
            ExpeditionWorldView world,
            ExpeditionHud hud,
            string productId)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _productRun = new ProductExpeditionRun(_run, productId);
            _secondsPerTick = 1.0f / run.TicksPerSecond;
            _accumulatedSeconds = 0.0f;
            _droppedTickWarningIssued = false;
            _events.Clear();
            _world.Synchronize(_run, _events);
            _hud.Synchronize(_run, _events);
            _world.Render(_run, 0.0f);
        }

        public void AdvanceFrame(float frameDeltaTime, Vector2 direction)
        {
            if (_run == null)
            {
                throw new InvalidOperationException("本地远征宿主尚未初始化");
            }

            if (float.IsNaN(frameDeltaTime) || float.IsInfinity(frameDeltaTime) || frameDeltaTime < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(frameDeltaTime), "帧时间必须是有限非负数");
            }

            if (!_run.HasEnded)
            {
                _accumulatedSeconds += frameDeltaTime;
                AdvancePendingTicks(direction);
            }

            _world.Render(_run, frameDeltaTime);
        }

        private void Update()
        {
            if (_run == null)
            {
                return;
            }

            AdvanceFrame(Time.unscaledDeltaTime, _input.Direction);
        }

        private void AdvancePendingTicks(Vector2 direction)
        {
            int advancedTicks = 0;
            while (_accumulatedSeconds >= _secondsPerTick && advancedTicks < MaximumTicksPerFrame)
            {
                _accumulatedSeconds -= _secondsPerTick;
                _events.Clear();
                _productRun.Advance(direction.x, direction.y, _events);
                _world.Synchronize(_run, _events);
                _hud.Synchronize(_run, _events);
                advancedTicks++;

                if (_run.HasEnded)
                {
                    _accumulatedSeconds = 0.0f;
                    return;
                }
            }

            if (_accumulatedSeconds < _secondsPerTick)
            {
                return;
            }

            _accumulatedSeconds = 0.0f;
            if (!_droppedTickWarningIssued)
            {
                Debug.LogWarning("[Expedition] 单帧固定 Tick 追赶超过 5 次，已丢弃本地积压");
                _droppedTickWarningIssued = true;
            }
        }
    }
}
