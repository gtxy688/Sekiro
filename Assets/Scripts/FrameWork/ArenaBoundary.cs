using System.Collections.Generic;
using UnityEngine;

// 擂台空气墙：自动找 Ground 层最大的 MeshCollider（擂台），在可行走范围外圈生成
// 四面静态盒墙，玩家/Boss 的走位、垫步、击退、跳跃都出不了场。墙挂在擂台根下，跟随擂台变换。
// 边界不用网格 AABB 直接框：AABB 会把塔楼/栏杆外沿也算进去，四角留出大段悬空照样掉。
// 做法是从上方按网格往下打射线，只统计"与主地面同层"的落点，求出真实可行走范围。
// 由 CombatManager.Start 调 Ensure() 装配，场景里不用手工挂。
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
        Bounds area = self != null
            ? self.bounds
            : new Bounds(transform.position, Vector3.one * 40f);

        float step = Mathf.Max(0.5f, Mathf.Max(area.size.x, area.size.z) / 64f);
        int mask = 1 << ground;
        float rayOriginY = area.max.y + 1f;
        float rayDist = area.size.y + 2f;

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
