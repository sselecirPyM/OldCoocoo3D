using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Core
{
    public enum InputType
    {
        MouseMove,
        MouseLeftDown,
        MouseRightDown,
        MouseMiddleDown,
        MouseWheelChanged,
        MouseMoveDelta,
    }
    public struct InputData
    {
        public Vector2 point;
        public float mouseWheelDelta;
        public bool mouseDown;
        public InputType inputType;
    }
    public enum KeyEventType
    {
        KeyDown,
        KeyUp,
    }
    public struct KeyInputData
    {
        public int key;
        public KeyEventType keyEventType;
    }
    public static class Input
    {
        public static ConcurrentQueue<InputData> inputDatas = new ConcurrentQueue<InputData>();
        public static ConcurrentQueue<KeyInputData> keyInputDatas = new ConcurrentQueue<KeyInputData>();
        public static bool textInput = false;
        public static void EnqueueMouseClick(Vector2 point, bool click, InputType inputType)
        {
            inputDatas.Enqueue(new InputData() { point = point, mouseDown = click, inputType = inputType }); ;
        }
        public static void EnqueueMouseMove(Vector2 point)
        {
            inputDatas.Enqueue(new InputData() { point = point, inputType = InputType.MouseMove });
        }
        public static void EnqueueMouseMoveDelta(Vector2 point)
        {
            inputDatas.Enqueue(new InputData() { point = point, inputType = InputType.MouseMoveDelta });
        }
        public static void EnqueueMouseWheel(Vector2 point, float delta)
        {
            inputDatas.Enqueue(new InputData() { point = point, mouseWheelDelta = delta, inputType = InputType.MouseWheelChanged });
        }

        public static void KeyDown(int key)
        {
            keyInputDatas.Enqueue(new KeyInputData { key = key, keyEventType = KeyEventType.KeyDown });
        }

        public static void KeyUp(int key)
        {
            keyInputDatas.Enqueue(new KeyInputData { key = key, keyEventType = KeyEventType.KeyUp });
        }
    }
}
