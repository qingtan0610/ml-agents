# 相机系统调试指南

## 双击检测问题排查

### 1. 检查AI和敌人的设置
- **AI预制体**：确保有AIBrain组件
- **敌人预制体**：确保有Enemy2D组件
- **Collider设置**：两者都需要有Collider2D组件

### 2. Layer设置
确保AI和敌人的Layer设置正确：
- AI通常在"Player"层（Layer 11）
- 敌人在"Enemy"层（Layer 12）

### 3. 调试信息
运行游戏后，点击AI或敌人时查看Console：
- "Click at world position" - 显示点击的世界坐标
- "Found X colliders" - 显示检测到的碰撞体数量
- "Found AI/Enemy" - 显示找到的对象
- "First click" / "Double-clicked" - 显示点击状态

### 4. 可视化调试
在Scene视图中可以看到：
- **黄色圆圈**：点击检测范围（1.5单位）
- **红色十字**：精确点击位置
- **绿色线条**：相机到跟随目标的连线

### 5. 常见问题

#### 问题：点击没有反应
- 检查AI/敌人是否有Collider2D
- 检查Collider2D是否启用
- 查看Console是否有"No AI or Enemy found"消息

#### 问题：双击不触发
- 确保两次点击间隔小于0.3秒
- 查看Console的"First click"消息
- 确保点击的是同一个对象

#### 问题：跟随不流畅
- 检查LateUpdate中的跟随逻辑
- 调整cameraSmooth参数（默认0.1）

### 6. 测试步骤
1. 运行游戏
2. 单击AI - 应该看到"First click"消息
3. 快速再次点击 - 应该看到"Double-clicked"和"Now following"消息
4. 按ESC退出跟随模式
5. 对敌人重复相同步骤