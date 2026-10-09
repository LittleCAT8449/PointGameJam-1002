# Unity World Map Draft

本目录把 `WorldMapDraft` 的 moremap 版本导入 Unity 6.3，生成可在 Scene 中编辑的银河恶魔城世界总地图。

## 数据来源

- `Data/world_map_moremap_source.json` 是原始 39 房间、5 区域、53 条连接地图 JSON 的副本。
- 房间坐标来自原 JSON 中的 `position.drawio_px`。原 draw.io 与 JSON 的 39 个房间位置、53 条连接端点已核对一致；运行时不需要 draw.io 文件。
- 每 100 个来源坐标像素映射到 1 个 Unity 地图单位。该单位只服务于总地图排布，不代表实际平台关卡尺寸。

## 使用方法

1. 等 Unity 导入并编译 `Runtime` 与 `Editor` 下的脚本。
2. 选择 **Tools > World Map Draft > Generate Moremap Scene (39 Rooms)**，或打开 **Tools > World Map Draft > Open Map Editor**。
3. 生成器会创建独立场景并使用新文件名。若当前只有 Unity 新建的空白 Untitled 场景，会直接将它替换为空地图场景；若已有已保存场景，则以 Additive 方式保留它。遇到含内容的未保存场景时，会先要求保存或取消，避免丢失场景内容。
4. 在 Scene 中选择房间 GameObject，使用 Unity 的 Move 工具（W）拖动、Scale 工具（R）缩放。区域颜色、房间 ID/名称和邻接线由 Scene 视图绘制。
5. 在房间 Inspector 修改区域、功能、知识、相邻房间、连接类型、知识锁、隐藏通道和捷径。
6. 在 World Map Draft 窗口导入或导出 JSON，或校验当前地图。

## 目录约定

- `Runtime/`：保存到 Scene 中的地图数据组件，不包含玩法代码。
- `Editor/`：菜单、Scene 绘制、Inspector、JSON 导入导出和生成器。
- `Data/Generated/`：每次生成的 JSON 快照与拓扑校验报告。
- `Scenes/`：每次生成的新地图场景。

首次生成的场景是 `Scenes/WorldMapDraft_Moremap_39R.unity`。生成后也可在 Project 窗口中双击该场景重新打开。

本工具不创建美术素材、碰撞体、玩家控制器或房间切换逻辑。校验范围是房间拓扑、知识门顺序、探索回环和抽象房间矩形布局，不代表可玩关卡的跳跃几何验证。
