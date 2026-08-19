using UnityEngine;
using UnityEngine.UI;

// 左上角 Boss 状态栏：忍杀提示灯(2红点) + Boss 血条 + 名称
// 掉命：按 Dot1 → Dot0 顺序把活点换成 DeadDot 的图
public class BossStatusView : UIView
{
    [Header("血条")]
    [SerializeField] private Slider hpBar;

    [Header("忍杀提示灯")]
    [SerializeField] private Transform[] lifeSlots; // Dot0、Dot1（先灭后面的 Dot1）
    [SerializeField] private Image deadDot;         // 死点模板，运行时隐藏

    [Header("名称")]
    [SerializeField] private TMPro.TextMeshProUGUI nameText;

    private Sprite deadSprite;
    private Color deadColor = Color.white;
    private Sprite[][] liveSprites;
    private Color[][] liveColors;

    public override void OnViewInit()
    {
        ResolveSlots();
        CacheLiveLooks();
        if (deadDot != null)
        {
            deadSprite = deadDot.sprite;
            deadColor = deadDot.color;
            deadDot.gameObject.SetActive(false);
        }
    }

    // remaining：还剩几条命。2→全活；1→替换 Dot1；0→再替换 Dot0
    public void SetLifeDots(int remaining)
    {
        ResolveSlots();
        if (lifeSlots == null) return;

        int total = lifeSlots.Length;
        remaining = Mathf.Clamp(remaining, 0, total);
        for (int i = 0; i < total; i++)
        {
            bool dead = i >= remaining;
            ApplySlot(i, dead);
        }
    }

    public void SetHP(float ratio)
    {
        if (hpBar != null) hpBar.value = Mathf.Clamp01(ratio);
    }

    public void SetName(string name)
    {
        if (nameText != null) nameText.text = name;
    }

    private void ApplySlot(int index, bool dead)
    {
        if (lifeSlots == null || index < 0 || index >= lifeSlots.Length) return;
        Transform slot = lifeSlots[index];
        if (slot == null) return;

        Image[] images = slot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            if (dead && deadSprite != null)
            {
                images[i].sprite = deadSprite;
                images[i].color = deadColor;
            }
            else if (liveSprites != null && index < liveSprites.Length
                     && liveSprites[index] != null && i < liveSprites[index].Length)
            {
                images[i].sprite = liveSprites[index][i];
                images[i].color = liveColors[index][i];
            }
        }
    }

    private void CacheLiveLooks()
    {
        if (lifeSlots == null) return;
        liveSprites = new Sprite[lifeSlots.Length][];
        liveColors = new Color[lifeSlots.Length][];
        for (int i = 0; i < lifeSlots.Length; i++)
        {
            if (lifeSlots[i] == null) continue;
            Image[] images = lifeSlots[i].GetComponentsInChildren<Image>(true);
            liveSprites[i] = new Sprite[images.Length];
            liveColors[i] = new Color[images.Length];
            for (int j = 0; j < images.Length; j++)
            {
                if (images[j] == null) continue;
                liveSprites[i][j] = images[j].sprite;
                liveColors[i][j] = images[j].color;
            }
        }
    }

    private void ResolveSlots()
    {
        Transform root = transform.Find("LifeDots");
        if (root == null) root = transform;

        if (lifeSlots == null || lifeSlots.Length == 0 || SlotMissing())
        {
            Transform dot0 = root.Find("Dot0");
            Transform dot1 = root.Find("Dot1");
            if (dot0 != null && dot1 != null)
                lifeSlots = new[] { dot0, dot1 };
        }

        if (deadDot == null)
        {
            Transform dead = root.Find("DeadDot");
            if (dead != null) deadDot = dead.GetComponent<Image>();
        }
    }

    private bool SlotMissing()
    {
        if (lifeSlots == null) return true;
        for (int i = 0; i < lifeSlots.Length; i++)
        {
            if (lifeSlots[i] == null) return true;
        }
        return false;
    }
}
