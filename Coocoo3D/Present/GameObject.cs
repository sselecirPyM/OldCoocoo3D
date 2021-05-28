using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;

namespace Coocoo3D.Present
{
    public class GameObject : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public void PropChange(System.ComponentModel.PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public string Name = "GameObject";
        public string Description = string.Empty;
        public int layoutMask;

        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;

        public Vector3 PositionNextFrame;
        public Quaternion RotationNextFrame = Quaternion.Identity;

        public override string ToString()
        {
            return this.Name;
        }

        public Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public T GetComponent<T>() where T : Component
        {
            if (components.TryGetValue(typeof(T), out Component component))
            {
                return (T)component;
            }
            else return null;
        }
        public bool AddComponent(Component component)
        {
            if (components.ContainsKey(component.GetType()))
            {
                return false;
            }
            else
            {
                components.Add(component.GetType(), component);
                return true;
            }
        }
    }
}
