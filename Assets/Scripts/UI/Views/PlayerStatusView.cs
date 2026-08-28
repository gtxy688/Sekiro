using UnityEngine;
using UnityEngine.UI;

// 玩家状态栏：回生节点 + 血条。用完的节点换成 EndDot，不要把图标直接关掉。
public class PlayerStatusView : UIView
{
    [Header("回生节点")]
    [SerializeField] private Image[] reviveDots;
    [SerializeField] private Image[] endDots;

    [Header("血条")]
    [SerializeField] private Slider hpBar;

    public override void OnViewInit()
    {
        ResolveDots();
        if (endDots != null)
        {
            for (int i = 0; i < endDots.Length; i++)
            {
                if (endDots[i] != null)
                    endDots[i].gameObject.SetActive(false);
            }
        }
    }

    public void SetReviveDots(int remaining)
    {
        ResolveDots();
        int total = 0;
        if (reviveDots != null) total = reviveDots.Length;
        if (endDots != null) total = Mathf.Max(total, endDots.Length);
        remaining = Mathf.Clamp(remaining, 0, total);

        for (int i = 0; i < total; i++)
        {
            // 从左到右用掉：剩 1 次时左边已换成 EndDot，右边还亮
            bool used = i < total - remaining;
            Image live = reviveDots != null && i < reviveDots.Length ? reviveDots[i] : null;
            Image spent = endDots != null && i < endDots.Length ? endDots[i] : null;
            if (live != null)
            {
                live.enabled = true;
                live.gameObject.SetActive(!used || spent == null);
            }

            if (spent != null)
            {
                spent.enabled = true;
                spent.gameObject.SetActive(used);
            }
        }
    }

    public void SetHP(float ratio)
    {
        if (hpBar != null) hpBar.value = Mathf.Clamp01(ratio);
    }

    private void ResolveDots()
    {
        if (reviveDots == null || reviveDots.Length == 0 || SlotMissing(reviveDots))
        {
            Image first = FindImage("ReviveDot");
            Image second = FindImage("ReviveDot 2");
            if (second == null) second = FindImage("ReviveDot2");
            if (first != null && second != null)
                reviveDots = new[] { first, second };
            else if (first != null)
                reviveDots = new[] { first };
        }

        if (endDots == null || endDots.Length == 0 || SlotMissing(endDots))
        {
            Image first = FindImage("EndDot");
            Image second = FindImage("EndDot2");
            if (second == null) second = FindImage("EndDot 2");
            if (first != null && second != null)
                endDots = new[] { first, second };
            else if (first != null)
                endDots = new[] { first };
        }
    }

    private Image FindImage(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static bool SlotMissing(Image[] slots)
    {
        if (slots == null || slots.Length == 0) return true;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) return true;
        }
        return false;
    }
}
