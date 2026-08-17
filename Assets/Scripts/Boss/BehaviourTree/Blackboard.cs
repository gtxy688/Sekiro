using System.Collections.Generic;

// 行为树黑板：节点间共享数据的键值字典，挂在树根，所有节点共享
public class Blackboard
{
    private readonly Dictionary<string, object> data = new Dictionary<string, object>();

    public void Set(string key, object value)
    {
        data[key] = value;
    }

    public T Get<T>(string key)
    {
        if (data.TryGetValue(key, out object value) && value is T typed)
        {
            return typed;
        }
        return default;
    }

    public bool Has(string key)
    {
        return data.ContainsKey(key);
    }

    // 冷却工具：招式冷却（M7，参考 sekiro 逆向的 SetCoolTime）
    public bool IsOnCooldown(string key, float cooldown)
    {
        float last = Get<float>("cd_" + key);
        return UnityEngine.Time.time - last < cooldown;
    }

    public void SetCooldown(string key)
    {
        Set("cd_" + key, UnityEngine.Time.time);
    }
}
