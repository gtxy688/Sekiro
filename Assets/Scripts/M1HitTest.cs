using UnityEngine;
using UnityEngine.InputSystem;

// 【临时验收脚本 - M1 受击路由 + M3 命中判定】验收完删除
// 挂到角色上：
//   H = 单次受击      J = 连按测二次受击拦截
//   K = 开启武器判定(用 LightAttack 配置)   L = 关闭武器判定
public class M1HitTest : MonoBehaviour
{
    private CharacterBody body;

    private void Awake()
    {
        body = GetComponent<CharacterBody>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // H：单次受击（地面/空中取决于当前是否 IsGrounded）
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            body.ReceiveHit(body, 10, 15f, body.transform.position);
        }

        // J：连按两次/按住快速触发，测 StunnedState 二次受击拦截
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            body.ReceiveHit(body, 10, 15f, body.transform.position);
        }

        // K：开启武器判定（M3）——扫到敌人 Hurtbox 才会结算
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            body.EnableWeaponHit(body.LightAttack);
        }

        // L：关闭武器判定（M3）
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            body.DisableWeaponHit();
        }
    }
}
