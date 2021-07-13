using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics1
{
    public class GraphicsContext
    {
        GraphicsDevice device;
		GraphicsSignature m_currentSign;
		ID3D12GraphicsCommandList4 m_commandList;

        public void Reload(GraphicsDevice device)
        {
            this.device = device;
        }
        public void Execute()
        {

        }
    }
}
