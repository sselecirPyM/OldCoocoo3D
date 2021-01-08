# Coocoo3D
一个CPU要求极低的MMD渲染器，支持DirectX12和DXR实时光线追踪。

视频及演示下载[https://www.bilibili.com/video/BV1p54y127ig/](https://www.bilibili.com/video/BV1p54y127ig/)

光线追踪全局光照演示[https://www.bilibili.com/video/bv13Z4y1V7a2](https://www.bilibili.com/video/bv13Z4y1V7a2)

支持自定义着色器。支持光线追踪全局光照

已经不打算维持较高的更新频率了，希望有人能接手一下。这个软件绝大部分逻辑使用C#写成，维护较为方便。

部分Shader代码仍然不正确，请接手的人更正他们。
## 基本功能
* 加载pmx模型
* 加载vmd动作
* 播放动画
* 录制图像序列
## 图形功能
* 自定义着色器
* GPU粒子系统
* 烘焙天空盒
* 后处理
## 高效架构
Coocoo3D的开发使用了一种新的软件开发模式。组件间的联系不从加载时开始，不在卸载时结束，这样各个组件的引用的生存周期可以变长，可以简化组件管理代码的编写。

Coocoo3D从不让用户等待太久，因此Coocoo3D比较耗时的加载过程都是渐进的。
