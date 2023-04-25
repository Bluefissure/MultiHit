# MultiHit

[![GitHub all releases](https://img.shields.io/github/downloads/Bluefissure/MultiHit/total?color=green)](https://github.com/Bluefissure/MultiHit/releases)

**将动作伤害的飘字分成多段。**

通过添加第三方仓库进行安装：
- 国服：`https://raw.githubusercontent.com/Bluefissure/MultiHit/CN/repo.json`
- 国服(镜像)：`https://dalamud_cn_3rd.otters.cloud/plugins/MultiHit`
- 国际服：`https://raw.githubusercontent.com/Bluefissure/MultiHit/master/repo.json`

通过命令 `/mhit` 打开配置窗口。

多数代码的来源是来自于 [DamageInfoPlugin](https://github.com/lmcintyre/DamageInfoPlugin) 的复制粘贴。

## Q&A

### 1. 是否会改变实际应用的伤害值？
当然不会，所有的更改仅限于客户端，并仅影响飘字，它不会对你发送到/接收自游戏服务器的数据造成任何影响，也不会影响ACT/FFLogs分析的任何DPS数据。

### 2. 如果我使用其他插件更改了飘字中的动作名称怎么办？
飘字是通过其文本来获取的（因为它没有动作信息），因此如果更改动作的名称，则无法通过游戏数据中未更改的动作名称匹配到飘字。

同样的原因，如果你为同一动作启用了多个 MultiHit，那么只有一个会生效。

### 3. 是否支持不同的客户端？
是的。我使用中文客户端来开发和测试最新功能（因为订阅问题），我将为国际客户端制作另一个仓库来分发。

### 4. 既然游戏动作和判定依托答辩，这插件有什么意义吗？
Multhit 旨在为那些追求打击感的玩家，提升游戏的战斗反馈。当然，这将不可避免地牺牲一些实用性。如果你不关心这些，或更关心观察每个技能的确切伤害值，那么 Multhit 将毫无意义（有关更多用户用例，请查看 **技能动作の唯一神** [ Papachin](https://www.youtube.com/c/papapachin) 的 mod 的详细信息）。

### 5. 我的网络延迟很高，使用这个插件后效果非常差...
不推荐在高网络延迟的环境下使用 Multhit。

### 6. 为什么我制作了自己的预设，但是飘字的出现时间和我的输入的延迟不一致？
分割后的飘字是通过游戏内的飘字触发的，而大多数动作都会有一个延迟，它在 TMB 文件中定义。
在大多数情况下，最好与 TMB 修改一起使用 Multhit。推荐将 TMB 中的飘字开始时间设置为 0（AOE 技能推荐为 3）。

### 7. 为什么我制作任何修改后，并没有效果（包括启用/禁用预设，添加/删除/修改数值）？
请确保你修改后点击“应用更改”来应用修改。

### 8. 为什么我修改了飘字的颜色但是实际没有变化？
颜色仅在 alpha（透明度）不为 0 时才有效。在修改颜色时，请同时修改右侧栏的透明度值。

### 9. 在我开启“打断”选项后，技能的飘字还没显示完，就被后续技能打断了，我会亏伤害吗？
如 1 中所述，所有修改仅影响你的飘字显示，所以你实际伤害永远不会变。除非你安装了一些可以被打断的 VFX mod（例如 Papachin 的 NERO-GNB 的 Keen Edge 系列技能），否则大多数情况下不推荐打开打断选项。

### 10. Finalhit 有什么用途？
Finalhit 可以在最后一个 hit 后再显示本次攻击的总伤害。如果你想直观地看到每个技能的真实总伤，可以尝试一下（如果同时开启打断选项，它也会被打断）。
