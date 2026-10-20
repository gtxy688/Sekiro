using System.Collections.Generic;
using UnityEngine;

namespace ARPG.FrameWork
{

    // 从 Ground 层体积最大的碰撞体采样主地面范围，生成四面静态空气墙。
    // 按落点高度排除塔楼等上层结构，避免直接使用碰撞体包围盒把边界扩到悬空区。
    // 这是场景环境辅助组件，不接收玩家命令；生成的 BoxCollider 在场地四周阻挡角色。
    public class ArenaBoundary : MonoBehaviour
    {
        [Tooltip("墙内收边距（≈胶囊半径），防止角色贴墙时半个身子悬出场外")]
        public float padding = 0.45f;
        [Tooltip("墙高出地面的部分，拦住跳跃/空中位移")]
        public float heightAboveFloor = 4f;
        [Tooltip("墙埋入地面以下的部分，拦住贴边下坠")]
        public float depthBelowFloor = 2f;
        [Tooltip("地面采样过滤带宽：与主地面高差超过它的结构（塔楼/上层）不算边界")]
        public float floorBand = 2f;

        private const float WallThickness = 1f;
        private const string ContainerName = "BoundaryWalls";

        // 供场景管理器调用：保证擂台上有一份 ArenaBoundary
        public static void Ensure()
        {
            if (FindObjectOfType<ArenaBoundary>() != null) return;

            Collider arena = FindArenaCollider();
            if (arena == null)
            {
                Debug.LogWarning("[ArenaBoundary] Ground 层没找到擂台碰撞体，空气墙未生成");
                return;
            }
            arena.gameObject.AddComponent<ArenaBoundary>();
        }

        private static Collider FindArenaCollider()
        {
            int ground = LayerMask.NameToLayer("Ground");
            Collider best = null;
            float bestVolume = 0f;
            foreach (Collider c in FindObjectsOfType<Collider>())
            {
                if (c.gameObject.layer != ground) continue;
                Bounds b = c.bounds;
                float volume = b.size.x * b.size.y * b.size.z;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = c;
                }
            }
            return best;
        }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            int ground = LayerMask.NameToLayer("Ground");
            Transform old = transform.Find(ContainerName);
            if (old != null) Destroy(old.gameObject);

            if (!TryComputeFloorBounds(ground, out float floorY, out float minX, out float maxX,
                    out float minZ, out float maxZ))
            {
                Debug.LogWarning("[ArenaBoundary] 地面采样失败，空气墙未生成");
                return;
            }

            Transform container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);

            // 根据可行走范围和上下延伸量，计算四面墙的世界坐标与尺寸。
            float centerY = floorY - depthBelowFloor + (heightAboveFloor + depthBelowFloor) * 0.5f;
            float wallHeight = heightAboveFloor + depthBelowFloor;
            float extentX = maxX - minX + WallThickness * 2f;
            float extentZ = maxZ - minZ + WallThickness * 2f;
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            float sideX = maxX + padding + WallThickness * 0.5f;
            float sideMinX = minX - padding - WallThickness * 0.5f;
            float sideMaxZ = maxZ + padding + WallThickness * 0.5f;
            float sideMinZ = minZ - padding - WallThickness * 0.5f;

            MakeWall(container, ground, new Vector3(centerX, centerY, sideMaxZ), new Vector3(extentX, wallHeight, WallThickness));
            MakeWall(container, ground, new Vector3(centerX, centerY, sideMinZ), new Vector3(extentX, wallHeight, WallThickness));
            MakeWall(container, ground, new Vector3(sideX, centerY, centerZ), new Vector3(WallThickness, wallHeight, extentZ));
            MakeWall(container, ground, new Vector3(sideMinX, centerY, centerZ), new Vector3(WallThickness, wallHeight, extentZ));
        }

        private static void MakeWall(Transform container, int layer, Vector3 center, Vector3 size)
        {
            // 只创建碰撞体，没有 Renderer，所以是看不见的墙；未加 Rigidbody 的碰撞体作为静态障碍。
            GameObject go = new GameObject("AirWall");
            go.layer = layer;
            go.transform.SetParent(container, false);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            go.transform.position = center;
        }

        // 从上方向下扫网格射线：先求主地面高度（落点中位数），再统计与主地面同层的
        // 落点范围。塔楼/上层结构因高差超带宽被排除，边界不会外扩到悬空区。
        private bool TryComputeFloorBounds(int ground, out float floorY,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            floorY = 0f;
            minX = maxX = minZ = maxZ = 0f;

            Collider self = GetComponent<Collider>();
            Bounds area = self.bounds;

            float step = Mathf.Max(0.5f, Mathf.Max(area.size.x, area.size.z) / 64f);
            int mask = 1 << ground;
            // NameToLayer 得到层号，1 << ground 转成只选该层的位掩码，供 Raycast 过滤检测对象。
            float rayOriginY = area.max.y + 1f;
            float rayDist = area.size.y + 2f;

            // 第一轮取落点高度中位数，确定主地面所在高度。
            List<float> ys = new List<float>();
            for (float x = area.min.x; x <= area.max.x; x += step)
            {
                for (float z = area.min.z; z <= area.max.z; z += step)
                {
                    if (Physics.Raycast(new Vector3(x, rayOriginY, z), Vector3.down,
                            out RaycastHit hit, rayDist, mask))
                        ys.Add(hit.point.y);
                }
            }
            if (ys.Count == 0) return false;

            ys.Sort();
            floorY = ys[ys.Count / 2];

            // 第二轮只统计主地面高度带内的落点，求出水平边界。
            bool any = false;
            for (float x = area.min.x; x <= area.max.x; x += step)
            {
                for (float z = area.min.z; z <= area.max.z; z += step)
                {
                    if (!Physics.Raycast(new Vector3(x, rayOriginY, z), Vector3.down,
                            out RaycastHit hit, rayDist, mask)) continue;
                    if (Mathf.Abs(hit.point.y - floorY) > floorBand) continue;

                    if (!any)
                    {
                        minX = maxX = x;
                        minZ = maxZ = z;
                        any = true;
                    }
                    else
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (z < minZ) minZ = z;
                        if (z > maxZ) maxZ = z;
                    }
                }
            }
            return any;
        }
    }

}
