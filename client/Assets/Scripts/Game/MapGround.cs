using UnityEngine;
using Shared.Common.Config;

namespace Client.Game
{
    public class MapGround : MonoBehaviour
    {
        private MapConfig config;

        public void Generate(MapConfig mapConfig)
        {
            config = mapConfig;
            CreateGround();
            CreateBoundary();
            CreateBirthMark();
            CreateDecorations();
        }

        private Material CreateMat(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            return mat;
        }

        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.parent = transform;
            ground.transform.localScale = new Vector3(config.Width / 10f, 1f, config.Height / 10f);
            ground.transform.position = new Vector3(config.Width / 2f, 0, config.Height / 2f);
            ground.GetComponent<Renderer>().material = CreateMat(new Color(0.35f, 0.55f, 0.3f));

            // 移除默认碰撞器（Plane自带），后面用边界碰撞
            Destroy(ground.GetComponent<Collider>());
        }

        private void CreateBoundary()
        {
            float wallHeight = 1.5f;
            float wallThickness = 0.3f;
            var wallColor = new Color(0.5f, 0.45f, 0.35f);
            float w = config.Width;
            float h = config.Height;

            // 四面墙：前(z=0)、后(z=h)、左(x=0)、右(x=w)
            CreateWall("Wall_Front", new Vector3(w / 2f, wallHeight / 2f, -wallThickness / 2f),
                new Vector3(w + wallThickness * 2, wallHeight, wallThickness), wallColor);
            CreateWall("Wall_Back", new Vector3(w / 2f, wallHeight / 2f, h + wallThickness / 2f),
                new Vector3(w + wallThickness * 2, wallHeight, wallThickness), wallColor);
            CreateWall("Wall_Left", new Vector3(-wallThickness / 2f, wallHeight / 2f, h / 2f),
                new Vector3(wallThickness, wallHeight, h + wallThickness * 2), wallColor);
            CreateWall("Wall_Right", new Vector3(w + wallThickness / 2f, wallHeight / 2f, h / 2f),
                new Vector3(wallThickness, wallHeight, h + wallThickness * 2), wallColor);
        }

        private void CreateWall(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.parent = transform;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = CreateMat(color);
        }

        private void CreateBirthMark()
        {
            var pos = MapData.GridToWorld(config.BirthX, config.BirthY);

            // 地面标记：一个发光的小方块
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "BirthMark";
            mark.transform.parent = transform;
            mark.transform.position = pos + new Vector3(0, 0.05f, 0);
            mark.transform.localScale = new Vector3(3f, 0.1f, 3f);
            mark.GetComponent<Renderer>().material = CreateMat(new Color(1f, 0.85f, 0.2f, 0.6f));
            Destroy(mark.GetComponent<Collider>());
        }

        private void CreateDecorations()
        {
            // 使用固定随机种子确保每次生成一致
            var rng = new System.Random(config.Id * 1000 + 42);

            // 在出生点附近（半径20格内）不放置装饰
            float birthCenterX = config.BirthX;
            float birthCenterZ = config.BirthY;

            // 树木
            int treeCount = Mathf.Min(config.Width * config.Height / 200, 200);
            for (int i = 0; i < treeCount; i++)
            {
                float x = (float)(rng.NextDouble() * (config.Width - 10) + 5);
                float z = (float)(rng.NextDouble() * (config.Height - 10) + 5);

                // 避开出生点附近
                if (Mathf.Abs(x - birthCenterX) < 15 && Mathf.Abs(z - birthCenterZ) < 15)
                    continue;

                CreateTree(x, z, rng);
            }

            // 石头
            int rockCount = Mathf.Min(config.Width * config.Height / 400, 100);
            for (int i = 0; i < rockCount; i++)
            {
                float x = (float)(rng.NextDouble() * (config.Width - 10) + 5);
                float z = (float)(rng.NextDouble() * (config.Height - 10) + 5);

                if (Mathf.Abs(x - birthCenterX) < 12 && Mathf.Abs(z - birthCenterZ) < 12)
                    continue;

                CreateRock(x, z, rng);
            }
        }

        private void CreateTree(float x, float z, System.Random rng)
        {
            var parent = new GameObject("Tree");
            parent.transform.parent = transform;
            parent.transform.position = new Vector3(x, 0, z);

            float height = 1.5f + (float)(rng.NextDouble() * 1.5f);

            // 树干：圆柱（用Capsule近似）
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            trunk.name = "Trunk";
            trunk.transform.parent = parent.transform;
            trunk.transform.localPosition = new Vector3(0, height * 0.4f, 0);
            trunk.transform.localScale = new Vector3(0.3f, height * 0.4f, 0.3f);
            trunk.GetComponent<Renderer>().material = CreateMat(new Color(0.45f, 0.3f, 0.15f));
            Destroy(trunk.GetComponent<CapsuleCollider>());

            // 树冠：球体
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.parent = parent.transform;
            crown.transform.localPosition = new Vector3(0, height * 0.85f, 0);
            float crownSize = 0.8f + (float)(rng.NextDouble() * 0.6f);
            crown.transform.localScale = new Vector3(crownSize, crownSize * 0.8f, crownSize);
            float greenVar = 0.3f + (float)(rng.NextDouble() * 0.25f);
            crown.GetComponent<Renderer>().material = CreateMat(new Color(0.15f, greenVar + 0.2f, 0.1f));
            Destroy(crown.GetComponent<SphereCollider>());

            // 碰撞体：用一个简单的BoxCollider覆盖树干区域
            var col = parent.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, height, 0.5f);
            col.center = new Vector3(0, height * 0.4f, 0);
        }

        private void CreateRock(float x, float z, System.Random rng)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.parent = transform;
            float scale = 0.3f + (float)(rng.NextDouble() * 0.5f);
            rock.transform.localScale = new Vector3(scale, scale * 0.6f, scale);
            rock.transform.position = new Vector3(x, scale * 0.3f, z);
            float gray = 0.35f + (float)(rng.NextDouble() * 0.2f);
            rock.GetComponent<Renderer>().material = CreateMat(new Color(gray, gray, gray * 0.9f));
        }

        public MapConfig GetConfig() => config;
    }
}
