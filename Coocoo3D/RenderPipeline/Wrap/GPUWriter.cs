using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System.IO;
using System.Numerics;

namespace Coocoo3D.RenderPipeline.Wrap
{
    public class GPUWriter
    {
        MemoryStream memoryStream = new MemoryStream();
        public BinaryWriterPlus binaryWriter;
        CBuffer cBuffer;

        public GPUWriter()
        {
            binaryWriter = new BinaryWriterPlus(memoryStream);
        }

        public int BufferBegin()
        {
            int allign = ((int)memoryStream.Position + 255) & ~255;
            binaryWriter.Seek(allign, SeekOrigin.Begin);
            return allign;
        }

        public CBuffer GetBuffer(GraphicsDevice device, GraphicsContext context, bool isCBuffer)
        {
            if (cBuffer == null)
                cBuffer = new CBuffer();
            if (cBuffer.size < memoryStream.Position)
            {
                device.InitializeCBuffer(cBuffer, (int)memoryStream.Position);
            }
            context.UpdateResource(cBuffer, new Span<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Position));
            binaryWriter.Seek(0, SeekOrigin.Begin);
            return cBuffer;
        }

        void GetSpacing(int sizeX)
        {
            int currentOffset = (int)memoryStream.Position;
            int c = (currentOffset & 15);
            if (c != 0 && c + sizeX > 16)
            {
                int d = 16 - c;
                for (int i = 0; i < d; i++)
                    binaryWriter.Write((byte)0);
            }
        }

        public void Write(int a) => binaryWriter.Write(a);
        public void Write(float a) => binaryWriter.Write(a);

        public void Write(Vector2 a)
        {
            GetSpacing(8);
            binaryWriter.Write(a);
        }

        public void Write(Vector3 a)
        {
            GetSpacing(12);
            binaryWriter.Write(a);
        }

        public void Write(Vector4 a)
        {
            GetSpacing(16);
            binaryWriter.Write(a);
        }

        public void Write(Matrix4x4 a)
        {
            GetSpacing(16);
            binaryWriter.Write(Matrix4x4.Transpose(a));
        }

        public void SetBufferImmediately(GraphicsContext context, bool isCBuffer,int slot)
        {
            context.SetCBVRSlot(new Span<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Position), slot);
            binaryWriter.Seek(0, SeekOrigin.Begin);
        }

        public void SetBufferComputeImmediately(GraphicsContext context, bool isCBuffer,int slot)
        {
            context.SetComputeCBVRSlot(new Span<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Position), slot);
            binaryWriter.Seek(0, SeekOrigin.Begin);
        }
    }
}
