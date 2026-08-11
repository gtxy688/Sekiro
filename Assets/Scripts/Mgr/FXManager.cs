using UnityEngine;

public class FXManager : MonoBehaviour
{
    [Header("特效预制体")]
    public GameObject normalBlockSparks;  // 浅黄色小火花预制体
    public GameObject perfectParrySparks; // 深黄色大火花预制体

    // 订阅事件
    private void OnEnable()
    {
        CombatEventBus.OnWeaponDeflected += SpawnDeflectFX;
    }

    // 取消订阅
    private void OnDisable()
    {
        CombatEventBus.OnWeaponDeflected -= SpawnDeflectFX;
    }

    // 处理事件
    private void SpawnDeflectFX(Vector3 hitPoint, DeflectType type)
    {
        GameObject prefabToSpawn = null;

        // 根据传入的枚举类型，决定使用哪个特效
        switch (type)
        {
            case DeflectType.Normal:
                prefabToSpawn = normalBlockSparks;
                // 这里还可以顺便呼叫 AudioManager 播放沉闷的“笃”声
                break;
                
            case DeflectType.Perfect:
                prefabToSpawn = perfectParrySparks;
                // 这里呼叫 AudioManager 播放清脆的“叮”声
                break;
        }

        if (prefabToSpawn != null)
        {
            Instantiate(prefabToSpawn, hitPoint, Quaternion.identity);
        }
    }

    
}