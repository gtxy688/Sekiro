> **参考来源**：只狼游戏原始 Lua 行为分析（反编译）。这是 Boss AI 设计的**素材来源**，不是实现规格。实现以 `Docs/specs/boss/boss-ai-decision.md` 为准。
>

# 天守阁弦一郎AI


## 完整行为分析

### 1. Goal.Activate（arg0, arg1, arg2） 主动计划与交锋计划

```lua
Goal.Activate = function (arg0, arg1, arg2)
Init_Pseudo_Global(arg1, arg2)
local local0 = {}
local local1 = {}
local local2 = {}
Common_Clear_Param(local0, local1, local2)
local local3 = arg1:GetDist(TARGET_ENE_0) --距离
local local4 = arg1:GetExcelParam(AI_EXCEL_THINK_PARAM_TYPE__thinkAttr_doAdmirer)
local local5 = arg1:GetHpRate(TARGET_SELF) --血量
local local6 = arg1:GetSp(TARGET_SELF) --架势条
local local7 = arg1:GetNinsatsuNum() --第几条命？
arg1:AddObserveSpecialEffectAttribute(TARGET_SELF, 5025)
·····
arg1:AddObserveSpecialEffectAttribute(TARGET_ENE_0, 110620)
Set_ConsecutiveGuardCount_Interrupt(arg1) --更新连续防御的次数
arg1:DeleteObserve(0)
if arg0:Kengeki_Activate(arg1, arg2) then --若有交锋计划则返回
return
elseif not not arg1:HasSpecialEffectId(TARGET_ENE_0, 110060) or arg1:HasSpecialEffectId(TARGET_ENE_0, 110010) then
if arg1:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 90) then --若敌人在自身前方90度范围内
local0[21] = 1 --转身 arg1:AddSubGoal(GOAL_COMMON_Turn, 3, TA
local0[28] = 100 --SidewayMove或ApproachTarget接近目标与对峙
else
local0[21] = 100 --转身
end
elseif Common_ActivateAct(arg1, arg2, 0, 1) then --如果可以执行主动计划（开发者应该是在这控制可否Attac
if arg1:GetNumber(7) == 0 and arg1:HasSpecialEffectId(TARGET_SELF, 200050) then
local0[15] = 600 --3014、3015 射箭并向前砍一刀
elseif arg1:HasSpecialEffectId(TARGET_ENE_0, 110030) then
local0[28] = 100 --SidewayMove或ApproachTarget接近目标与对峙
elseif arg1:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 180) then --若敌人在自己身后180度内
local0[21] = 100 --转身
local0[22] = 1 --侧向跑步（左侧或右侧）
elseif not not arg1:HasSpecialEffectId(TARGET_ENE_0, 110060) or arg1:HasSpecialEffectId(TARGET_ENE_0, 110010) then
local0[27] = 100 --SidewayMove或ApproachTarget接近目标与对峙，于目标
elseif 7 <= local3 then --若距离大于7米
local0[10] = 300 --3006 快速接近后 砍两刀
local0[15] = 600 --3014、3015 射一箭 快速接近 砍一刀
elseif 5 <= local3 then --若距离大于5米 且小于 7米
local0[10] = 300 --3006 快速接近后 砍两刀
local0[34] = 100 --3007、3011 快速接近后 横砍，接着射箭
local0[23] = 100 --SidewayMove
if local6 <= 360 then --若自身架势条 <= 360 则
local0[9] = 300 --3040、3041 飞渡符舟
end
elseif 3 < local3 then --若距离大于3米 且小于 5米
local0[1] = 5 --[3000、3001、3002、3003]/[3000、3001、3010、30
local0[2] = 10 --3004 横二连砍
local0[6] = 30 --3016 横重砍
local0[11] = 15 --3037、3020 旋转横砍、肘击
local0[23] = 15 --SidewayMove
if local6 <= 360 then
local0[9] = 300 --3040、3041 飞渡符舟
end
else
local0[3] = 15 --3005 位移横砍+转身
local0[11] = 15 --3037、3020 旋转横砍、肘击
local0[23] = 10 --SidewayMove
local0[31] = 30 --3003、3045 横砍、踢一脚
if arg1:IsFinishTimer(7) == true then
local0[24] = 30 --[5201]、3044 [垫步]、跳跃五连射箭 3044在躯干条 <
end
end
end
--距离 <= 5m
if (not not arg1:HasSpecialEffectId(TARGET_ENE_0, 109031) or arg1:HasSpecialEffectId(TARGET_ENE_0, 110125)) and local3 <= 5 then
local0[16] = 100 --3022 跳跃下刺
end
if arg1:HasSpecialEffectId(TARGET_ENE_0, 109900) then
local0[1] = 5 --[3000、3001、3002、3003]/[3000、3001、3010、30
local0[2] = 5 --3004 横二连砍
local0[3] = 5 --3005 位移横砍+转身
local0[9] = 0 --禁止 3040、3041 飞度符舟
local0[11] = 5 --3037、3020 旋转横砍、肘击
local0[10] = 30 --3006 快速接近后 砍两刀
local0[15] = 0 --3014、3015 射一箭 快速接近 砍一刀
local0[31] = 0 --3003、3045 横砍、踢一脚
local0[48] = 30 --3013、3015 射一箭往前翻滚砍一刀
end
if arg1:GetNumber(2) == 1 then
local0[23] = 6000 --SidewayMove
arg1:SetNumber(2, 0)
end
if arg1:IsFinishTimer(0) == false then
local0[3] = 0 --禁止 3005 位移横砍+转身
local0[6] = 1 --3016 横重砍
end
if arg1:IsFinishTimer(1) == false then
local0[2] = 0 --禁止 3004 横二连砍
end
if arg1:IsFinishTimer(3) == false then
local0[24] = 0 --禁止 [5201]、3044 [垫步]、跳跃五连射箭 3044在躯干
end
if arg1:IsFinishTimer(6) == false then
local0[9] = 0 --禁止 3040、3041 飞度符舟
end
if arg1:HasSpecialEffectId(TARGET_SELF, 200051) then
local0[15] = 0 --禁止 3014、3015 射一箭 快速接近 砍一刀
local0[18] = 0 --没有该Act
local0[19] = 0 --没有该Act
local0[34] = 0 --禁止 3007、3011 快速接近后 横砍，接着射箭
local0[48] = 0 --禁止 3013、3015 射一箭往前翻滚砍一刀
end
if SpaceCheck(arg1, arg2, 45, 2) == false and SpaceCheck(arg1, arg2, -45, 2) == false then --自身[左/右]前方2m内有阻挡
local0[22] = 0 --禁止 侧向跑步（左侧或右侧）
end
if SpaceCheck(arg1, arg2, 90, 1) == false and SpaceCheck(arg1, arg2, -90, 1) == false then --自身正[左/右]侧1m有阻挡
local0[23] = 0 --禁止 SidewayMove
end
if SpaceCheck(arg1, arg2, 180, 2) == false then --正后方2米内有阻挡
local0[24] = 0 --禁止 [5201]、3044 [垫步]、跳跃五连射箭 3044在躯干
end
if SpaceCheck(arg1, arg2, 180, 1) == false then --正后方1米内有阻挡
local0[25] = 0 --禁止 LeaveTarget 缓慢往后走
end
if arg1:HasSpecialEffectId(TARGET_ENE_0, 110621) then
local0[23] = 0 --禁止SidewayMove
local0[24] = 0 --禁止 [5201]、3044 [垫步]、跳跃五连射箭 3044在躯干
local0[31] = 10 --禁止 3003、3045 横砍、踢一脚
end
local0[1] = SetCoolTime(arg1, arg2, 3000, 15, local0[1], 1)
····· 冷却
local0[48] = SetCoolTime(arg1, arg2, 3013, 5, local0[48], 1)
local1[1] = REGIST_FUNC(arg1, arg2, arg0["Act01"])
····· 注册函数
local1[48] = REGIST_FUNC(arg1, arg2, arg0["Act48"])
Common_Battle_Activate(arg1, arg2, local0, local1, REGIST_FUNC(arg1, arg2, arg0["ActAfter_AdjustSpace"]), local2)
return
end

//飞渡符舟
Goal.Act09 = function (arg0, arg1, arg2)
--移动到4.5米范围内
Approach_Act_Flex(arg0, arg1, 4.5 - arg0:GetMapHitRadius(TARGET_SELF), 4.5 - arg0:GetMapHitRadius(TARGET_SELF), 4.5 - arg0:GetMapHi
arg1:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3040, TARGET_ENE_0, 4, 0, 0, 0, 0)
arg1:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3041, TARGET_ENE_0, 3.5, 0)
arg0:SetTimer(6, 30)
GetWellSpace_Odds = 100
return GetWellSpace_Odds
end

Goal.Act10 = function (arg0, arg1, arg2)
--移动到距目标4.8-攻击距离内
Approach_Act_Flex(arg0, arg1, 4.8 - arg0:GetMapHitRadius(TARGET_SELF), 4.8 - arg0:GetMapHitRadius(TARGET_SELF), 4.8 - arg0:GetMapHi
arg1:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, 3006, TARGET_ENE_0, 9999, 0, 0, 0, 0) --快速接近后 砍两刀
arg0:SetTimer(3, 10)
GetWellSpace_Odds = 100
return GetWellSpace_Odds
end

Goal.Act15 = function (arg0, arg1, arg2)
--接近到距离目标8.9-攻击距离内
Approach_Act_Flex(arg0, arg1, 8.9 - arg0:GetMapHitRadius(TARGET_SELF), 8.9 - arg0:GetMapHitRadius(TARGET_SELF) - 2, 8.9 - arg0:GetM
arg1:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3014, TARGET_ENE_0, 7 - arg0:GetMapHitRadius(TARGET_SELF), 0, 0, 0, 0)
arg1:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3015, TARGET_ENE_0, 9999, 0, 0)
arg0:SetNumber(7, 1)
GetWellSpace_Odds = 100
return GetWellSpace_Odds
end

Goal.Act22 = function (arg0, arg1, arg2)
local local0 = 3
local local1 = 0
if SpaceCheck(arg0, arg1, -45, 2) == true then --自身左前方45度2米无阻挡
if SpaceCheck(arg0, arg1, 45, 2) == true then --自身右前方45度2米无阻挡
if arg0:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_R, 180) then --敌人在自身右侧180度范围内
arg1:AddSubGoal(GOAL_COMMON_SpinStep, local0, 5202, TARGET_ENE_0, local1, AI_DIR_TYPE_L, 0) --向左跑
else
arg1:AddSubGoal(GOAL_COMMON_SpinStep, local0, 5203, TARGET_ENE_0, local1, AI_DIR_TYPE_R, 0) --向右跑
end
else
arg1:AddSubGoal(GOAL_COMMON_SpinStep, local0, 5202, TARGET_ENE_0, local1, AI_DIR_TYPE_L, 0) --若右前方45度有阻挡则向左跑
end
elseif SpaceCheck(arg0, arg1, 45, 2) == true then --自身右前方45度2米无阻挡
arg1:AddSubGoal(GOAL_COMMON_SpinStep, local0, 5203, TARGET_ENE_0, local1, AI_DIR_TYPE_R, 0) --向右跑
end
GetWellSpace_Odds = 0
return GetWellSpace_Odds
end

Goal.Act23 = function (arg0, arg1, arg2)
local local0 = arg0:GetDist(TARGET_ENE_0) --距离
local local1 = arg0:GetSp(TARGET_SELF) --架势条
local local2 = 20
local local3 = arg0:GetRandam_Int(1, 100) --随机数
local local4 = 0
if SpaceCheck(arg0, arg1, -90, 1) == true then --左侧90度1米内无阻挡
if SpaceCheck(arg0, arg1, 90, 1) == true then --右侧90度1米内无阻挡
if arg0:IsInsideTargetEx(TARGET_ENE_0, TARGET_SELF, AI_DIR_TYPE_R, 180, 5) then --自己在敌人的右侧180度5米范围内
local4 = 1 --向左SidewayMove
else
local4 = 0 --向右SidewayMove
end
else
local4 = 0
end
elseif SpaceCheck(arg0, arg1, 90, 1) == true then --若右侧90度1米内无阻挡
local4 = 1 --向左SidewayMove
end
arg0:SetNumber(10, local4)
arg1:AddSubGoal(GOAL_COMMON_SidewayMove, arg0:GetRandam_Int(1.5, 3), TARGET_ENE_0, local4, arg0:GetRandam_Int(30, 45), true, true,
GetWellSpace_Odds = 100
return GetWellSpace_Odds
end

Goal.Act24 = function (arg0, arg1, arg2)
local local0 = 0
local local1 = 5201
--若正后方4米范围内无阻挡且与玩家距离小于4m
if SpaceCheck(arg0, arg1, 180, 2) == true and SpaceCheck(arg0, arg1, 180, 4) == true and 4 >= arg0:GetDist(TARGET_ENE_0) then
local1 = 5201 --垫步
end
arg0:SetNumber(2, 1)
local local2 = arg1:AddSubGoal(GOAL_COMMON_SpinStep, 3, local1, TARGET_ENE_0, local0, AI_DIR_TYPE_B, 0)
local2:TimingSetTimer(3, 30, AI_TIMING_SET__UPDATE_SUCCESS)
if arg0:GetSpRate(TARGET_SELF) <= 0.7 and arg0:HasSpecialEffectId(TARGET_SELF, 200050) then --若自己的躯干值 < 0.7
arg1:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3044, TARGET_ENE_0, DistToAtt1, local0, FrontAngle, 0, 0) --跳到空中五连射箭
end
GetWellSpace_Odds = 100
return GetWellSpace_Odds
end

Goal.Act27 = function (arg0, arg1, arg2)
local local0 = arg0:GetDist(TARGET_ENE_0) --距离
local local1 = arg0:GetRandam_Int(1, 100) --随机数
local local2 = arg0:GetRandam_Float(2, 4) --随机浮点
if 8 <= local0 then --若距离大于8米则
arg1:AddSubGoal(GOAL_COMMON_ApproachTarget, local2, TARGET_ENE_0, 8, TARGET_ENE_0, true, -1) --接近敌人到8m范围内
elseif local0 <= 5 then --若距离小于5米则
arg1:AddSubGoal(GOAL_COMMON_LeaveTarget, local2, TARGET_ENE_0, 5, TARGET_ENE_0, true, -1) --远离敌人到5米处
end
arg1:AddSubGoal(GOAL_COMMON_SidewayMove, local2, TARGET_ENE_0, arg0:GetRandam_Int(0, 1), arg0:GetRandam_Int(30, 45), true, true, -1
GetWellSpace_Odds = 0
return GetWellSpace_Odds
end

Goal.Act28 = function (arg0, arg1, arg2)
local local0 = arg0:GetDist(TARGET_ENE_0)
local local1 = 1.5
if local0 <= 3 then --如果距离小于3m则SidewayMove[1.5秒]，GetRandam_Int(0, 1)则是方向
arg1:AddSubGoal(GOAL_COMMON_SidewayMove, 1.5, TARGET_ENE_0, arg0:GetRandam_Int(0, 1), arg0:GetRandam_Int(30, 45), true, true, -
elseif local0 <= 8 then --如果距离大于8米则跑步接近到3米内 true则为走
arg1:AddSubGoal(GOAL_COMMON_ApproachTarget, local1, TARGET_ENE_0, 3, TARGET_SELF, true, -1)
else --如果距离大于3米小于8米则走路接近到8米内 false则为跑
arg1:AddSubGoal(GOAL_COMMON_ApproachTarget, local1, TARGET_ENE_0, 8, TARGET_SELF, false, -1)
end
GetWellSpace_Odds = 0
return GetWellSpace_Odds
end
```

### Goal.Kengeki_Activate 交锋计划

```lua
Goal.Kengeki_Activate = function (arg0, arg1, arg2, arg3)
local local0 = ReturnKengekiSpecialEffect(arg1) --检查有没有交锋计划的ID
if local0 == 0 then
return false
end
local local1 = {}
local local2 = {}
local local3 = {}
Common_Clear_Param(local1, local2, local3)
local local4 = arg1:GetDist(TARGET_ENE_0) --距离
local local5 = arg1:GetSpRate(TARGET_SELF) --躯干值
if local0 == 200200 then --右手弹开
arg1:SetNumber(0, arg1:GetNumber(0) + 1)
if 2.5 <= local4 then --若距离 > 2.5m
local1[50] = 100 --NoAction
elseif 2 <= arg1:GetNumber(0) then
local1[3] = 60 --SidewayMove
local1[20] = 60 --5202、3007 侧向垫步、横向重砍
local1[30] = 5 --3063 横向二连砍
local1[38] = 50 --5202、3007 侧向垫步、横向重砍
local1[15] = 30 --3031、[3019、3029]/[3036] 射箭、砍两刀后重砍 或 射箭、二连射箭
local1[43] = 50 --3062 突刺
local1[39] = 30 --3034、3036、3015 跳起射一箭、落地射两箭、砍一刀
if arg1:GetNumber(6) == 0 then
local1[32] = 20 --3018、3015 射两箭、砍一刀
else
local1[33] = 20 --3018、[3019、3029]/[3019] 射两箭、向前跑重砍一刀、[跳跃砍两刀]
end
elseif arg1:GetNumber(3) == 0 then
local1[1] = 50 --3050 砍一刀
else
local1[4] = 100 --3055 砍一刀
end
elseif local0 == 200201 then --左手弹开
if 2.5 <= local4 then --若距离 > 2.5m
local1[50] = 1000 --NoAction
elseif 2 <= arg1:GetNumber(0) then
local1[3] = 60 --SidewayMove
local1[20] = 60 --5202、3007 侧向垫步、横向重砍
local1[38] = 50 --5202、3007 侧向垫步、横向重砍
local1[15] = 30 --3031、[3019、3029]/[3036] 射箭、砍两刀后重砍 或 射箭、二连射箭
local1[43] = 50 --3062 突刺
local1[39] = 30 --3034、3036、3015 跳起射一箭、落地射两箭、砍一刀
if arg1:GetNumber(6) == 0 then
local1[32] = 50 --3018、3015 射两箭、砍一刀
else
local1[33] = 50 --3018、[3019、3029]/[3019] 射两箭、向前跑重砍一刀、[跳跃砍两刀]
end
elseif arg1:GetNumber(3) == 0 then
local1[1] = 50 --3050 砍一刀
else
local1[4] = 100 --3055 砍一刀
end
elseif local0 == 200210 then --右手弹开
local1[2] = 100 --5201、3044 后退、空中五连射箭
local1[17] = 100 --3071 砍一刀
local1[38] = 50 --5202、3007 侧向垫步、横向重砍
local1[31] = 50 --3068 二连砍
elseif local0 == 200211 then --左手弹开
local1[2] = 100 --5201、3044 后退、空中五连射箭
local1[10] = 50 --3065 砍一刀
local1[31] = 50 --3068 二连砍
local1[38] = 50 --5202、3007 侧向垫步、横向重砍
elseif local0 == 200216 then --左手弹开
if 2 <= local4 then --若距离 > 2m
local1[50] = 10 --NoAction
else
arg1:SetNumber(0, arg1:GetNumber(0) + 1)
if 3 <= arg1:GetNumber(0) then
local1[3] = 60 --SidewayMove
local1[20] = 20 --5202、3007 侧向垫步、横向重砍
local1[38] = 100 --5202、3007 侧向垫步、横向重砍
local1[43] = 50 --3062 突刺
if arg1:GetNumber(6) == 0 then
local1[32] = 50 --3018、3015 射两箭、砍一刀
else
local1[33] = 50 --3018、[3019、3029]/[3019] 射两箭、向前跑重砍一刀、[跳跃砍两刀]
end
else
local1[20] = 50 --5202、3007 侧向垫步、横向重砍
if arg1:GetNumber(3) == 0 then
local1[1] = 50 --3050 砍一刀
else
local1[14] = 50 --3076 砍一刀
end
end
end
elseif local0 == 200215 then
if 2 <= local4 then
local1[50] = 10 --NoAction
else
arg1:SetNumber(0, arg1:GetNumber(0) + 1)
if 3 <= arg1:GetNumber(0) then
local1[20] = 20 --5202、3007 侧向垫步、横向重砍
local1[38] = 30 --5202、3007 侧向垫步、横向重砍
local1[31] = 15 --3068 二连砍
local1[43] = 10 --3062 突刺
local1[39] = 10 --3034、3036、3015 跳起射一箭、落地射两箭、砍一刀
if arg1:GetNumber(6) == 0 then
local1[32] = 50 --3018、3015 射两箭、砍一刀
else
local1[33] = 50 --3018、[3019、3029]/[3019] 射两箭、向前跑重砍一刀、[跳跃砍两刀]
end
else
local1[20] = 50 --5202、3007 侧向垫步、横向重砍
if arg1:GetNumber(3) == 0 then
local1[1] = 50 --3050 砍一刀
else
local1[14] = 50 --3076 砍一刀
end
end
end
end
if arg1:HasSpecialEffectId(TARGET_SELF, 200051) then
local1[3] = 0 --禁止 SidewayMove
local1[9] = 0 --禁止 3018、3015 射两箭、砍一刀
local1[15] = 0 --禁止 3031、[3019、3029]/[3036] 射箭、砍两刀后重砍 或 射箭、二连射箭
local1[32] = 0 --禁止 3018、3015 射两箭、砍一刀
local1[33] = 0 --禁止 3018、[3019、3029]/[3019] 射两箭、向前跑重砍一刀、[跳跃砍两刀]
local1[39] = 0 --禁止 3034、3036、3015 跳起射一箭、落地射两箭、砍一刀
local1[46] = 0 --禁止 3039 跳起射一重箭
elseif arg1:HasSpecialEffectId(TARGET_SELF, 200050) then
local1[20] = 0 --禁止 5202、3007 侧向垫步、横向重砍
end
if SpaceCheck(arg1, arg2, 45, 2) == false and SpaceCheck(arg1, arg2, -45, 2) == false then --左前方或右前方有阻挡
local1[20] = 0 --禁止 5202、3007 侧向垫步、横向重砍
end
if arg1:IsFinishTimer(6) == false then
local1[40] = 0 --禁止 3028 飞渡符舟完整
elseif arg1:IsFinishTimer(6) == true and arg1:GetHpRate(TARGET_SELF) <= 0.75 then --若血量百分比 < 0.75
local1[40] = 50 -- 3028 飞渡符舟完整
end
local1[1] = SetCoolTime(arg1, arg2, 3050, 8, local1[1], 1)
·····冷却
local1[45] = SetCoolTime(arg1, arg2, 3032, 15, local1[45], 1)
····· 注册函数
local2[46] = REGIST_FUNC(arg1, arg2, arg0["Kengeki46"])
local2[50] = REGIST_FUNC(arg1, arg2, arg0["NoAction"])
return Common_Kengeki_Activate(arg1, arg2, local1, local2, REGIST_FUNC(arg1, arg2, arg0["ActAfter_AdjustSpace"]), local3)
end
```

### 2.Goal.Interrupt (arg0, arg1, arg2) 变招计划与防御

```lua
Goal.Interrupt = function (arg0, arg1, arg2)
local local0 = arg1:GetSpecialEffectActivateInterruptType(0)
local local1 = arg1:GetSpecialEffectInactivateInterruptType(0)
local local2 = arg1:GetNinsatsuNum() --第几条命？
if arg1:IsLadderAct(TARGET_SELF) then --正在爬楼梯
return false
elseif not arg1:HasSpecialEffectId(TARGET_SELF, 200004) then
return false
elseif arg1:IsInterupt(INTERUPT_ParryTiming) then --防御变招
return arg0.Parry(arg1, arg2, 100, 0) --执行防御
elseif arg1:IsInterupt(INTERUPT_ShootImpact) and arg0.ShootReaction(arg1, arg2) then --防御远程攻击
return true
elseif arg1:IsInterupt(INTERUPT_ActivateSpecialEffect) then
if local0 == 3710020 then --霸体状态，比如飞渡符舟过程中不可防御
arg1:SetNumber(0, 0)
return true
elseif local0 == 3710030 and arg1:HasSpecialEffectId(TARGET_SELF, 3710032) then --二连砍 带上此id
arg2:ClearSubGoal()
arg2:AddSubGoal(GOAL_COMMON_EndureAttack, 5, 3092, TARGET_ENE_0, 9999, 0) --飞渡符舟
arg1:SetTimer(6, 50)
return true
elseif local0 == 5029 then
return arg0.Damaged(arg1, arg2)
elseif local0 == 5031 then
if local2 <= 1 and 4.1 <= arg1:GetDist(TARGET_ENE_0) then --如果还剩1条命 且距离大于4.1m
arg2:ClearSubGoal()
arg2:AddSubGoal(GOAL_COMMON_ComboRepeat, 5, 3017, TARGET_ENE_0, 9999, 0) --射一箭
end
elseif local0 == 3710050 then --空中五连射箭 带上此id
if arg1:GetRandam_Int(1, 100) <= 50 and arg1:HasSpecialEffectId(TARGET_SELF, 200050) then
arg2:ClearSubGoal()
arg2:AddSubGoal(GOAL_COMMON_ComboRepeat, 5, 3023, TARGET_ENE_0, 9999, 0) --蓄力重箭
else
arg2:ClearSubGoal()
arg2:AddSubGoal(GOAL_COMMON_SidewayMove, 4, TARGET_ENE_0, arg1:GetRandam_Int(0, 1), arg1:GetRandam_Int(30, 45), true, t
end
elseif local0 == 110620 then
arg1:Replanning()
return true
end
end
if Interupt_Use_Item(arg1, 8, 5) and (not arg1:HasSpecialEffectId(TARGET_SELF, 200051) or 2 > local2) then --使用物品
arg2:ClearSubGoal()
arg2:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 1, 3023, TARGET_ENE_0, 9999, 0, 0, 0, 0) --蓄力重箭
return true
else
return false
end
end

Goal.Parry = function (arg0, arg1, arg2, arg3)
local local0 = arg0:GetDist(TARGET_ENE_0) --距离
local local1 = GetDist_Parry(arg0) --获取防御距离
local local2 = arg0:GetRandam_Int(1, 100) --随机数
local local3 = 2
if arg0:HasSpecialEffectId(TARGET_SELF, 221000) then
local3 = 0
elseif arg0:HasSpecialEffectId(TARGET_SELF, 221001) then
local3 = 1
end
if arg0:IsFinishTimer(AI_TIMER_PARRY_INTERVAL) == false then --如果防御冷却还没结束则直接返回
return false
elseif not not arg0:HasSpecialEffectId(TARGET_ENE_0, 110450) or not not arg0:HasSpecialEffectId(TARGET_ENE_0, 110501) or arg0:HasSp
return false
end
arg0:SetTimer(AI_TIMER_PARRY_INTERVAL, 0.1) --防御冷却0.1秒
if arg2 == nil then
arg2 = 50
end
if arg3 == nil then
arg3 = 0
end
--敌人在自己前方90度范围内 且 自己在敌人前方180度可防御范围内
if arg0:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 90) and arg0:IsInsideTargetEx(TARGET_ENE_0, TARGET_SELF, AI_DIR_TYPE_F, 180, lo
if arg0:HasSpecialEffectId(TARGET_SELF, 3710040) then --如果正在肘击或反身砍一刀
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_EndureAttack, 0.3, 3102, TARGET_ENE_0, 9999, 0) --马上招架
arg0:SetTimer(5, 60)
return true
elseif arg0:HasSpecialEffectId(TARGET_ENE_0, COMMON_SP_EFFECT_PC_ATTACK_RUSH) then
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_EndureAttack, 0.3, 3103, TARGET_ENE_0, 9999, 0) --马上招架
return true
elseif arg0:HasSpecialEffectId(TARGET_ENE_0, 109970) then
if arg0:IsTargetGuard(TARGET_SELF) and ReturnKengekiSpecialEffect(arg0) == false then
return false
elseif local3 == 2 then
return false
elseif local3 == 1 then
if local2 <= 50 then
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_SpinStep, 1, 5211, TARGET_ENE_0, 0, AI_DIR_TYPE_B, 0)
return true
end
elseif local3 == 0 and local2 <= 100 then
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_EndureAttack, 0.3, 3101, TARGET_ENE_0, 9999, 0) --马上招架
return true
end
return
elseif arg0:HasSpecialEffectId(TARGET_ENE_0, 109980) then
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_SpinStep, 1, 5201, TARGET_ENE_0, 0, AI_DIR_TYPE_B, 0) --向后垫步
return true
elseif arg0:GetRandam_Int(1, 100) <= Get_ConsecutiveGuardCount(arg0) * arg2 then
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_EndureAttack, 0.3, 3101, TARGET_ENE_0, 9999, 0) --马上招架
return true
else
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_EndureAttack, 0.3, 3100, TARGET_ENE_0, 9999, 0) --马上招架
return true
end
elseif arg0:IsInsideTargetEx(TARGET_ENE_0, TARGET_SELF, AI_DIR_TYPE_F, 90, local1 + 1) then --如果自己在敌人前方90度可防御范围内
if arg0:GetRandam_Int(1, 100) <= arg3 then
arg1:ClearSubGoal()
arg1:AddSubGoal(GOAL_COMMON_SpinStep, 1, 5211, TARGET_ENE_0, 0, AI_DIR_TYPE_B, 0)
return true
else
return false
end
else
return false
end
end
```
