using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.ResourceWarp
{
    public class LoadedAsset
    {
        public string Name;
        public byte[] data;

        public DateTimeOffset lastModifiedTime;
        public StorageFolder folder;
        public string fullPath;
        public string relativePath;

        public GraphicsObjectStatus Status;


    }
}
