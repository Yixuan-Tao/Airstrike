# Airstrike: Rocket Defense

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Airstrike是一款基于Godot引擎开发的2D火箭防御游戏，玩家需要控制角色发射火箭，摧毁来袭的敌机，保护地面安全。

## 游戏特色

- **经典防御玩法**：控制角色左右移动，发射火箭摧毁敌机
- **多样化敌人**：四种不同类型的敌机（蓝色、绿色、红色、黄色）和友好飞机
- **波次系统**：20个递增难度的波次，每波敌人数量和类型不同
- **道具系统**：跳跃靴、连锁火箭、穿透能力等多种道具
- **地形破坏**：真实的地形破坏效果，炸弹会对地形造成损伤
- **地图编辑器**：内置地图编辑器，可创建自定义地图
- **视觉效果**：动态背景、粒子效果和流畅的动画

## 游戏玩法

### 基本操作
- **左右移动**：A/左箭头键 和 D/右箭头键
- **跳跃**：W/上箭头键 或 空格键
- **发射火箭**：鼠标点击目标位置

### 游戏目标
- 摧毁尽可能多的敌机，获得高分
- 保护地面，避免被敌机投下的炸弹击中
- 完成所有20波敌人的攻击

### 道具说明
- **跳跃靴**：增加角色的跳跃能力
- **连锁火箭**：每次点击发射多枚火箭
- **穿透能力**：火箭可以穿透多个敌人
- **EMP**：暂时瘫痪所有敌机
- **POW**：增加得分

## 技术实现

- **游戏引擎**：Godot 4.2
- **语言**：GDScript
- **渲染**：GL Compatibility 模式
- **分辨率**：1280x720

## 项目结构

```
rocket_defense/
├── assets/              # 游戏资源
│   ├── sprites/         # 精灵图片
│   └── wave_config.csv  # 波次配置文件
├── autoload/            # 自动加载脚本
│   └── game_manager.gd  # 游戏状态管理器
├── scenes/              # 游戏场景
│   ├── destructible_terrain.tscn  # 可破坏地形
│   ├── hud.tscn         # 游戏界面
│   ├── main.tscn        # 主游戏场景
│   ├── map_editor.tscn  # 地图编辑器
│   ├── map_select.tscn  # 地图选择
│   └── player.tscn      # 玩家角色
├── scripts/             # 游戏脚本
│   ├── bomb.gd          # 炸弹逻辑
│   ├── crosshair.gd     # 准星逻辑
│   ├── destructible_terrain.gd  # 地形破坏逻辑
│   ├── enemy_soldier.gd # 敌人士兵逻辑
│   ├── hud.gd           # 界面逻辑
│   ├── main.gd          # 主游戏逻辑
│   ├── map_editor.gd    # 地图编辑器逻辑
│   ├── map_select.gd    # 地图选择逻辑
│   ├── pickup.gd        # 道具逻辑
│   ├── plane_base.gd    # 飞机基础逻辑
│   ├── player.gd        # 玩家逻辑
│   ├── poison_cloud.gd  # 毒云逻辑
│   ├── rocket.gd        # 火箭逻辑
│   └── shrapnel.gd      # 弹片逻辑
├── icon.svg             # 游戏图标
└── project.godot        # 项目配置文件
```

## 如何运行

1. 确保你已经安装了 [Godot Engine 4.2](https://godotengine.org/download)
2. 克隆或下载本项目到本地
3. 启动Godot引擎，点击"Import"按钮导入项目
4. 选择项目文件夹中的 `project.godot` 文件
5. 点击"Run"按钮开始游戏

## 自定义地图

1. 在游戏主菜单中选择"Custom Map"
2. 使用地图编辑器创建自定义地形
3. 保存地图后开始游戏，使用你创建的地图进行游戏

## 波次配置

游戏的波次配置存储在 `assets/wave_config.csv` 文件中，你可以编辑此文件来自定义每波敌人的数量和类型。

配置格式：
```csv
Wave,Blue,Green,Red,Yellow,Friendly
1,2,0,0,0,1
2,2,1,0,0,1
...
```

## 开发说明

### 主要脚本说明
- `game_manager.gd`：全局游戏状态管理，包括分数、生命值、波次等
- `main.gd`：主游戏场景逻辑，负责敌人生成、碰撞检测等
- `player.gd`：玩家角色控制和火箭发射逻辑
- `plane_base.gd`：飞机基础逻辑，包括移动、投弹等
- `destructible_terrain.gd`：可破坏地形系统

### 扩展游戏

- **添加新敌人类型**：在 `plane_base.gd` 中添加新的飞机类型
- **添加新道具**：在 `pickup.gd` 中添加新的道具类型
- **修改游戏平衡**：调整 `game_manager.gd` 中的波次配置和难度参数

## 贡献

欢迎提交问题和拉取请求，帮助改进这个游戏！

## 许可证

本项目采用 MIT 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

---

**享受游戏！** 🚀