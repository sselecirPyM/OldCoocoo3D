using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public ref struct SpanWriter
    {
        public Span<byte> dest;
        public int currentPosition;

        public SpanWriter(Span<byte> dest)
        {
            this.dest = dest;
            currentPosition = 0;
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            data.CopyTo(dest.Slice(currentPosition));
            currentPosition += data.Length;
        }
    }
}
