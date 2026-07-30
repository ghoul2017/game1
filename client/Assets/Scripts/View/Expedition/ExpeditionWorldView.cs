using System.Collections.Generic;
using GameplayCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Expedition
{
    /// <summary>
    /// 创建并同步 T1 灰盒世界、敌人、资源、出口、目标标记和 Boss 危险区。
    /// </summary>
    public sealed class ExpeditionWorldView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera _camera;
        private GameObject[] _enemyObjects;
        private Renderer[] _enemyRenderers;
        private GameObject _exitObject;
        private Renderer _exitRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private int _bossViewCount;
        private int _normalEnemyViewCount;
        private GameObject[] _resourceObjects;
        private Renderer[] _resourceRenderers;
        private SquadView _squad;
        private GameObject _targetMarker;
        private GameObject _warning;

        public SquadView Squad => _squad;

        public int EnemyViewCount => _enemyObjects.Length;

        public int NormalEnemyViewCount => _normalEnemyViewCount;

        public int BossViewCount => _bossViewCount;

        public int ResourceViewCount => _resourceObjects.Length;

        public bool WarningVisible => _warning.activeSelf;

        public void Initialize(ExpeditionRun run)
        {
            _propertyBlock = new MaterialPropertyBlock();
            CreateLighting();
            CreateGround(run);
            CreateSquad(run);
            CreateEnemies(run);
            CreateResources(run);
            CreateExit(run);
            CreateMarkers();
            CreateCamera(run);
        }

        public void Synchronize(ExpeditionRun run, IReadOnlyList<ExpeditionEvent> events)
        {
            _squad.Synchronize(run.Members);
            SynchronizeEnemies(run);
            SynchronizeResources(run);
            SynchronizeExit(run);
            SynchronizeTarget(run);
            ConsumeWarningEvents(run, events);
        }

        public void Render(ExpeditionRun run, float deltaTime)
        {
            _squad.Render(run, deltaTime);
            Vector3 cameraTarget = _squad.LeaderPosition + new Vector3(0.0f, 15.0f, -11.0f);
            float blend = deltaTime <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-6.0f * deltaTime);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, cameraTarget, blend);
        }

        private void CreateGround(ExpeditionRun run)
        {
            float width = run.MaximumX - run.MinimumX;
            float depth = run.MaximumY - run.MinimumY;
            Vector3 center = new Vector3(
                (run.MinimumX + run.MaximumX) * 0.5f,
                -0.2f,
                (run.MinimumY + run.MaximumY) * 0.5f);
            CreatePrimitive(
                PrimitiveType.Cube,
                "ExpeditionGround",
                center,
                new Vector3(width, 0.3f, depth),
                new Color(0.12f, 0.16f, 0.2f));
        }

        private void CreateLighting()
        {
            GameObject lightObject = new GameObject("ExpeditionLight", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);

            Light directionalLight = lightObject.GetComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.color = new Color(1.0f, 0.94f, 0.84f);
            directionalLight.intensity = 1.1f;
            directionalLight.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.25f, 0.3f);
            RenderSettings.skybox = null;
        }

        private void CreateSquad(ExpeditionRun run)
        {
            GameObject squadObject = new GameObject("SquadView");
            squadObject.transform.SetParent(transform, false);
            _squad = squadObject.AddComponent<SquadView>();
            _squad.Initialize(run.Members, run.SquadX, run.SquadY);
        }

        private void CreateEnemies(ExpeditionRun run)
        {
            _enemyObjects = new GameObject[run.Enemies.Count];
            _enemyRenderers = new Renderer[run.Enemies.Count];

            for (int index = 0; index < run.Enemies.Count; index++)
            {
                ExpeditionEnemy enemy = run.Enemies[index];
                bool isBoss = enemy.Kind == ExpeditionEnemyKind.Boss;
                if (isBoss)
                {
                    _bossViewCount++;
                }
                else
                {
                    _normalEnemyViewCount++;
                }

                GameObject enemyObject = CreatePrimitive(
                    isBoss ? PrimitiveType.Cube : PrimitiveType.Sphere,
                    isBoss ? $"Boss_{enemy.Id}" : $"Enemy_{enemy.Id}",
                    new Vector3(enemy.X, isBoss ? 1.25f : 0.7f, enemy.Y),
                    isBoss ? new Vector3(2.5f, 2.5f, 2.5f) : new Vector3(1.4f, 1.4f, 1.4f),
                    isBoss ? new Color(0.75f, 0.08f, 0.15f) : new Color(1.0f, 0.35f, 0.2f));
                _enemyObjects[index] = enemyObject;
                _enemyRenderers[index] = enemyObject.GetComponent<Renderer>();
            }
        }

        private void CreateResources(ExpeditionRun run)
        {
            _resourceObjects = new GameObject[run.Resources.Count];
            _resourceRenderers = new Renderer[run.Resources.Count];

            for (int index = 0; index < run.Resources.Count; index++)
            {
                ExpeditionResource resource = run.Resources[index];
                GameObject resourceObject = CreatePrimitive(
                    PrimitiveType.Cylinder,
                    $"Resource_{resource.Id}",
                    new Vector3(resource.X, 0.4f, resource.Y),
                    new Vector3(1.3f, 0.4f, 1.3f),
                    new Color(1.0f, 0.78f, 0.12f));
                _resourceObjects[index] = resourceObject;
                _resourceRenderers[index] = resourceObject.GetComponent<Renderer>();
            }
        }

        private void CreateExit(ExpeditionRun run)
        {
            _exitObject = CreatePrimitive(
                PrimitiveType.Cylinder,
                "ExpeditionExit",
                new Vector3(run.ExitX, 0.08f, run.ExitY),
                new Vector3(run.ExitRadius * 2.0f, 0.08f, run.ExitRadius * 2.0f),
                new Color(0.18f, 0.2f, 0.22f));
            _exitRenderer = _exitObject.GetComponent<Renderer>();
        }

        private void CreateMarkers()
        {
            _targetMarker = CreatePrimitive(
                PrimitiveType.Cylinder,
                "TargetMarker",
                Vector3.zero,
                new Vector3(2.0f, 0.03f, 2.0f),
                new Color(0.2f, 0.9f, 1.0f));
            _targetMarker.SetActive(false);

            _warning = CreatePrimitive(
                PrimitiveType.Cylinder,
                "BossWarning",
                Vector3.zero,
                new Vector3(1.0f, 0.04f, 1.0f),
                new Color(1.0f, 0.05f, 0.05f));
            _warning.SetActive(false);
        }

        private void CreateCamera(ExpeditionRun run)
        {
            GameObject cameraObject = new GameObject("ExpeditionCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(55.0f, 0.0f, 0.0f);
            cameraObject.transform.position = new Vector3(run.SquadX, 16.0f, run.SquadY - 11.0f);

            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 9.5f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100.0f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);
        }

        private void SynchronizeEnemies(ExpeditionRun run)
        {
            for (int index = 0; index < run.Enemies.Count; index++)
            {
                ExpeditionEnemy enemy = run.Enemies[index];
                _enemyObjects[index].SetActive(enemy.IsAlive);
                if (!enemy.IsAlive)
                {
                    continue;
                }

                float lostHealth = 1.0f - (float)enemy.Health / enemy.MaxHealth;
                Color baseColor = enemy.Kind == ExpeditionEnemyKind.Boss
                    ? new Color(0.75f, 0.08f, 0.15f)
                    : new Color(1.0f, 0.35f, 0.2f);
                SetColor(_enemyRenderers[index], Color.Lerp(baseColor, Color.black, lostHealth * 0.65f));
            }
        }

        private void SynchronizeResources(ExpeditionRun run)
        {
            for (int index = 0; index < run.Resources.Count; index++)
            {
                ExpeditionResource resource = run.Resources[index];
                _resourceObjects[index].SetActive(!resource.IsDepleted);
                if (resource.IsDepleted)
                {
                    continue;
                }

                float remaining = (float)resource.RemainingAmount / resource.InitialAmount;
                SetColor(
                    _resourceRenderers[index],
                    Color.Lerp(new Color(0.45f, 0.3f, 0.05f), new Color(1.0f, 0.78f, 0.12f), remaining));
            }
        }

        private void SynchronizeExit(ExpeditionRun run)
        {
            Color color = run.ExitOpen
                ? new Color(0.1f, 1.0f, 0.35f)
                : new Color(0.18f, 0.2f, 0.22f);
            SetColor(_exitRenderer, color);
        }

        private void SynchronizeTarget(ExpeditionRun run)
        {
            if (run.TargetKind == ExpeditionTargetKind.None)
            {
                _targetMarker.SetActive(false);
                return;
            }

            float x = 0.0f;
            float y = 0.0f;
            if (run.TargetKind == ExpeditionTargetKind.Enemy)
            {
                ExpeditionEnemy enemy = FindEnemy(run, run.TargetId);
                x = enemy.X;
                y = enemy.Y;
            }
            else
            {
                ExpeditionResource resource = FindResource(run, run.TargetId);
                x = resource.X;
                y = resource.Y;
            }

            _targetMarker.transform.position = new Vector3(x, 0.05f, y);
            _targetMarker.SetActive(true);
        }

        private void ConsumeWarningEvents(ExpeditionRun run, IReadOnlyList<ExpeditionEvent> events)
        {
            for (int index = 0; index < events.Count; index++)
            {
                ExpeditionEvent current = events[index];
                if (current.Kind == ExpeditionEventKind.BossWarningStarted)
                {
                    ExpeditionEnemy boss = FindEnemy(run, current.SourceId);
                    _warning.transform.position = new Vector3(current.X, 0.07f, current.Y);
                    _warning.transform.localScale = new Vector3(
                        boss.BossWarningRadius * 2.0f,
                        0.04f,
                        boss.BossWarningRadius * 2.0f);
                    _warning.SetActive(true);
                }
                else if (current.Kind == ExpeditionEventKind.BossWarningHit ||
                    current.Kind == ExpeditionEventKind.BossWarningDodged ||
                    IsBossDeath(run, current))
                {
                    _warning.SetActive(false);
                }
            }
        }

        private GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject result = GameObject.CreatePrimitive(primitiveType);
            result.name = objectName;
            result.transform.SetParent(transform, false);
            result.transform.position = position;
            result.transform.localScale = scale;
            Collider collider = result.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            SetColor(result.GetComponent<Renderer>(), color);
            return result;
        }

        private void SetColor(Renderer target, Color color)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_propertyBlock);
        }

        private static ExpeditionEnemy FindEnemy(ExpeditionRun run, int id)
        {
            for (int index = 0; index < run.Enemies.Count; index++)
            {
                if (run.Enemies[index].Id == id)
                {
                    return run.Enemies[index];
                }
            }

            return null;
        }

        private static ExpeditionResource FindResource(ExpeditionRun run, int id)
        {
            for (int index = 0; index < run.Resources.Count; index++)
            {
                if (run.Resources[index].Id == id)
                {
                    return run.Resources[index];
                }
            }

            return null;
        }

        private static bool IsBossDeath(ExpeditionRun run, ExpeditionEvent current)
        {
            if (current.Kind != ExpeditionEventKind.EnemyDied ||
                current.TargetKind != ExpeditionObjectKind.Enemy)
            {
                return false;
            }

            return FindEnemy(run, current.TargetId).Kind == ExpeditionEnemyKind.Boss;
        }
    }
}
