# Unity Editor 扩展：从零到攻击时间轴

这篇不假设你会写 Editor 代码。目标不是背完整 API，而是理解项目里的编辑器工具怎样运行，并能应对面试官从“做了时间轴”继续追问到“你是怎么保存、撤销和构建拦截的”。

## 1. Editor 代码和运行时代码有什么区别

`UnityEditor` 命名空间只存在于 Unity 编辑器，不会进入 Player Build。

项目把编辑器脚本放在 `Assets/Editor` 下。Unity 会把这个目录编译进 Editor 程序集，因此里面可以使用 `EditorWindow`、`AssetDatabase`、`Undo` 等 API；运行时代码不能反向引用它们。

面试基础题：

> 为什么不能在普通 Runtime 脚本里直接引用 UnityEditor？

回答：Player Build 中没有 UnityEditor 程序集，运行时代码引用它会导致构建失败。可以使用 Editor 目录或 `#if UNITY_EDITOR` 隔离，但完整工具类最好从程序集边界上隔离。

## 2. 项目用了三种 Editor 扩展形式

### CustomEditor：在现有 Inspector 上加入口

`AttackConfigEditor` 与 `BossMoveTableEditor` 继承 `UnityEditor.Editor`：

```csharp
[CustomEditor(typeof(AttackConfig))]
public class AttackConfigEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("打开攻击时间轴"))
            AttackTimelineWindow.Open((AttackConfig)target);
    }
}
```

需要记住：

- `[CustomEditor(typeof(T))]`：声明 Inspector 服务于哪种对象。
- `target`：当前 Inspector 正在编辑的对象。
- `OnInspectorGUI()`：绘制 Inspector。
- `DrawDefaultInspector()`：保留默认字段，再追加按钮。

这里没有重写所有字段，只给 SO 增加工具入口，代码量和维护成本都较低。

### EditorWindow：独立工具窗口

`AttackTimelineWindow` 和 `ArpgValidatorWindow` 继承 `EditorWindow`：

```csharp
[MenuItem("ARPG/攻击时间轴")]
static void OpenFromMenu()
{
    GetWindow<AttackTimelineWindow>("攻击时间轴");
}
```

需要记住：

- `[MenuItem]`：给 Unity 顶部菜单增加命令。
- `GetWindow<T>(title)`：取得或创建某类窗口。
- `Show()`：显示窗口。
- `OnEnable / OnDisable`：初始化和释放编辑器资源。
- `OnGUI()`：绘制窗口并处理 IMGUI 事件。
- `Repaint()`：请求窗口重绘。

时间轴需要轨道、动画预览和拖拽交互，不适合全部塞进普通 Inspector，所以使用独立窗口。

### 构建回调：在 Build 前执行校验

`ArpgBuildValidator` 实现 `IPreprocessBuildWithReport`：

```csharp
public int callbackOrder => 0;

public void OnPreprocessBuild(BuildReport report)
{
    List<ValidationIssue> issues = ArpgValidationRules.RunAll();
    if (ArpgValidationRules.CountErrors(issues) > 0)
        throw new BuildFailedException("战斗配置校验失败");
}
```

它会在构建开始前执行。抛出 `BuildFailedException` 会终止构建，避免动画名错误、非法窗口等静默问题进入 Player。

## 3. IMGUI 的基本心智模型

项目使用 IMGUI，而不是 UI Toolkit。IMGUI 是“立即模式 GUI”：Unity 会反复调用 `OnGUI()`，当前事件需要什么控件，就重新声明什么控件。

常用 API：

- `EditorGUILayout.LabelField / FloatField / Toggle / ObjectField / Popup`
- `GUILayout.Button`
- `EditorGUILayout.BeginHorizontal / EndHorizontal`
- `EditorGUILayout.BeginScrollView / EndScrollView`
- `GUILayoutUtility.GetRect`：申请一块自绘区域。
- `EditorGUI.DrawRect`：绘制时间轴条。
- `Event.current`：取得鼠标、滚轮、布局和重绘事件。

重要陷阱：`OnGUI()` 一帧可能因为 Layout、Repaint 和输入事件执行多次。不要在没有变更判断的情况下，每次 OnGUI 都写回资产。

项目通过 `EditorGUI.BeginChangeCheck / EndChangeCheck`、按钮结果和待处理标志，只在真实交互发生时修改数据。

## 4. 一次时间轴编辑如何发生

```text
AttackConfig Inspector 点击“打开攻击时间轴”
→ AttackTimelineWindow.Open(config)
→ GetWindow 取得窗口并保存当前目标
→ ReloadWorking 把命中、音效、出箭数据复制到工作副本
→ OnGUI 绘制动画预览和轨道
→ 鼠标拖拽只修改工作副本
→ 点击保存
→ Undo.RecordObject
→ 工作副本写回 SO
→ EditorUtility.SetDirty
```

使用工作副本的好处：

- 拖动过程中不会每个像素都直接污染资产。
- 可以集中校正数组、窗口顺序和时长。
- 用户点击保存时才形成明确修改事务。

代价是要维护工作副本与真实资产之间的同步。

## 5. Undo.RecordObject 与 SetDirty

项目直接修改 ScriptableObject 字段，因此保存前调用：

```csharp
Undo.RecordObject(asset, "Attack Timeline");
// 修改 asset 字段
EditorUtility.SetDirty(asset);
```

- `Undo.RecordObject` 在修改前记录快照，让 Ctrl+Z 可以恢复。
- `EditorUtility.SetDirty` 标记资产已修改，需要写回磁盘。

顺序必须是“先 Record，再修改”。修改完成后才 Record，Undo 记录到的已经是新值。

项目编辑的是 SO 资产，不是场景中的 Prefab 实例，所以这些位置不需要处理 `PrefabUtility.RecordPrefabInstancePropertyModifications`。

## 6. 为什么项目没有主要使用 SerializedObject

Unity 更推荐普通自定义 Inspector 使用：

```csharp
serializedObject.Update();
SerializedProperty damage = serializedObject.FindProperty("BaseDamage");
EditorGUILayout.PropertyField(damage);
serializedObject.ApplyModifiedProperties();
```

它自动处理：

- Undo。
- Dirty 标记。
- 多对象编辑。
- Prefab Override。
- Unity 自身的序列化规则。

项目时间轴选择直接字段修改，因为它不是简单地一一绘制属性，而是在维护多条自定义轨道、工作副本、拖拽和统一保存。这样更直接，但 Undo、Dirty、多对象编辑和同步都要自己负责。

面试时不能说“SerializedObject 没必要”。更好的回答是：

> 普通 Inspector 字段优先使用 SerializedObject；当前时间轴是单资产、强交互、工作副本式编辑，所以采用显式 Undo 和 SetDirty。若要支持多选编辑、Prefab 属性或通用属性面板，我会改用 SerializedProperty。

## 7. AssetDatabase 管什么

`AssetDatabase` 操作 Project 中的资产，不是运行时对象。

项目中主要使用：

- `FindAssets("t:AttackConfig")`：找到某类全部资产。
- `GUIDToAssetPath`：把 GUID 转成项目路径。
- `LoadAssetAtPath<T>`：按路径加载资产。
- `CreateFolder / CreateAsset / SaveAssets`：创建并保存招式表。
- `GetAssetPath / AssetPathToGUID`：在路径与 GUID 间转换。

`ScriptableObject.CreateInstance<T>()` 只在内存中创建对象；只有再调用 `AssetDatabase.CreateAsset`，它才成为磁盘上的 `.asset` 文件。

## 8. 动画预览和拖拽用了什么

时间轴窗口的复杂部分不是输入框，而是：

- 从预览 Prefab 取得 Animator 和 AnimationClip。
- 按当前秒数采样动画姿态。
- 在窗口 Rect 中绘制预览。
- 用 `Event.current` 处理鼠标按下、拖动、抬起和滚轮。
- 用 `GUIUtility.hotControl` 保证一次拖拽期间控件持续拥有鼠标。
- 用 `EditorPrefs` 保存预览高度等个人编辑器偏好。

你不需要先背预览实现的每一行。面试中先解释“目标选择、轨道编辑、预览采样、工作副本、保存事务”五个阶段。

## 9. 校验为什么分成三层

`ArpgValidationRules.RunAll()` 是共享规则层：

- 查找所有 AttackConfig 与 BossMoveTable。
- 校验动画状态名、窗口顺序、假红条、脉冲、音效和出箭点。
- 返回 Error 或 Warning，不负责展示。

`ArpgValidatorWindow` 是交互展示层：

- 手动重新校验。
- 只看错误。
- 点击定位并 Ping 对应资产。

`ArpgBuildValidator` 是自动门禁：

- 构建前执行同一套规则。
- Warning 允许构建。
- Error 抛异常并中止构建。
- BatchMode 下也依靠日志和异常工作，不依赖弹窗。

这种拆分避免“窗口一套规则、构建又复制一套规则”导致漂移。

## 10. 需要记住的 API

第一阶段先记这些：

```text
[CustomEditor] / Editor / target / OnInspectorGUI
[MenuItem] / EditorWindow / GetWindow / OnGUI
EditorGUILayout / GUILayout / Event.current / Repaint
Undo.RecordObject / EditorUtility.SetDirty
SerializedObject.Update / FindProperty / ApplyModifiedProperties
AssetDatabase.FindAssets / LoadAssetAtPath / CreateAsset
IPreprocessBuildWithReport / BuildFailedException
```

其它 API 在读到对应交互时再查，不需要脱离项目背完整手册。

## 11. 最小练习

### 练习一：给 AttackConfig Inspector 加按钮

独立写出 `CustomEditor + DrawDefaultInspector + GUILayout.Button`，按钮只打印当前 `target.name`。

### 练习二：做一个只读窗口

用 `MenuItem + EditorWindow + OnGUI` 显示当前选择的 AttackConfig 和 BaseDamage。

### 练习三：让修改支持撤销

点击按钮前调用 `Undo.RecordObject`，修改伤害后调用 `SetDirty`，验证 Ctrl+Z 能恢复。

### 练习四：改成 SerializedProperty

把练习三改成 `serializedObject.Update → PropertyField → ApplyModifiedProperties`，观察普通字段不再需要手动管理 Undo 和 Dirty。

## 12. 面试追问梯度

### 基础概念

1. Editor 脚本为什么要放在 Editor 目录？
2. CustomEditor 和 EditorWindow 有什么区别？
3. IMGUI 的 OnGUI 为什么会反复执行？

### 项目实现

4. AttackTimelineWindow 从打开到保存经历哪些阶段？
5. 命中条、音效点和出箭点如何共用时间轴？
6. 配置体检窗口与构建校验如何复用规则？

### 方案取舍

7. 为什么选择 EditorWindow，而不是把时间轴塞进 Inspector？
8. 为什么项目直接改 SO 字段，没有主要使用 SerializedObject？
9. 为什么 Error 要阻止构建，而 Warning 只提示？
10. 为什么当前使用 IMGUI，而不是 UI Toolkit？

当前是 Unity 2022 的小型内部工具，IMGUI 直接且现有 API 足够；如果工具继续复杂化，需要样式复用、响应式布局和长期维护，可以考虑 UI Toolkit。

### 扩展设计

11. 如果支持多选修改多个 AttackConfig，要改什么？
12. 如果招式数据换成 JSON 或表格，Editor 工具怎样调整？
13. 如果预览窗口支持播放、暂停和逐帧，状态放在哪里？

### 故障排查

14. 为什么数值改了但重启 Unity 后丢失？
15. 为什么 Ctrl+Z 无法撤销？
16. 为什么 Animator 明明有动画，校验却说找不到？
17. 为什么 OnGUI 中拖一次却执行多次写入？

## 回答骨架

> 我用 EditorWindow + IMGUI 做独立时间轴，CustomEditor 只负责从 AttackConfig 和 BossMoveTable 的 Inspector 打开它。窗口加载目标后把命中脉冲、音效和出箭点复制为工作数据，动画预览按当前秒数采样；保存时先 Undo.RecordObject，再写回 ScriptableObject 并 SetDirty。校验规则独立成规则层，同时供手动体检窗口和 IPreprocessBuildWithReport 构建门禁复用。

回答完先停下，让面试官选择继续追问预览、序列化、保存还是构建校验，不要第一句话就堆 API。

## 官方 API 入口

- [Editor 与 CustomEditor](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Editor.html)
- [SerializedObject](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SerializedObject.html)
- [EditorUtility](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorUtility.html)
- [IPreprocessBuildWithReport](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Build.IPreprocessBuildWithReport.html)
