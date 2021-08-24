using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics1
{
    public class GraphicsContext
    {
        GraphicsDevice graphicsDevice;
		ID3D12GraphicsCommandList4 m_commandList;
        public RootSignature currentRootSignature;

        public void Reload(GraphicsDevice device)
        {
            this.graphicsDevice = device;
        }

        public void SetRootSignature(RootSignature graphicsSignature)
        {
            this.currentRootSignature = graphicsSignature;
        }

        public void RSSetScissorRect(int left, int top, int right, int bottom)
        {
            m_commandList.RSSetScissorRect(new Vortice.RawRect(left, top, right, bottom));
        }
        public void Begin()
        {
            m_commandList = graphicsDevice.GetCommandList();
        }

        public void SetPipelineState(PipelineStateObject pipelineStateObject, PSODesc psoDesc)
        {
            this.pipelineStateObject = pipelineStateObject;
            this.psoDesc = psoDesc;
        }

        public void ClearScreen(Vector4 color)
        {
            //device.rtvHeap.
            //m_commandList.ClearRenderTargetView(device.GetRenderTarget(),new Vortice.Mathematics.Color( color));
        }


        public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            m_commandList.SetPipelineState(pipelineStateObject.GetState(graphicsDevice, psoDesc, currentRootSignature, unnamedInputLayout));
            m_commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        }

        public void Execute()
        {
            m_commandList.Close();
            graphicsDevice.commandQueue.ExecuteCommandList(m_commandList);
            graphicsDevice.ReturnCommandList(m_commandList);
            m_commandList = null;
        }

        public PSODesc psoDesc;
        public UnnamedInputLayout unnamedInputLayout;
        public PipelineStateObject pipelineStateObject;
    }
}
