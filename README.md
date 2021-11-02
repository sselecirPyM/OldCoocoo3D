# Coocoo3D
一个CPU要求极低的MMD渲染器，支持自定义渲染，支持DirectX12 ~~和DXR实时光线追踪~~ 项目全面转向C#，正在重新设计。

你可以配置渲染文件来修改渲染Pass，增加所需纹理，使用自定义着色器等。

软件运行时点击菜单栏的 帮助->保存示例着色器文件 来查看示例文件，打开示例文件里的samplePasses.coocoox就可以查看自定义渲染的效果。

(远古版本)视频[https://www.bilibili.com/video/BV1p54y127ig/](https://www.bilibili.com/video/BV1p54y127ig/)

## 基本功能
* 加载pmx模型
* 加载vmd动作
* 播放动画
* 录制图像序列
## 图形功能
* 自定义着色器
* 烘焙天空盒
* 后处理
