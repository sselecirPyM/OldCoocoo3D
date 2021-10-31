using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public enum GraphicSignatureDesc
    {
        CBV,
        SRV,
        UAV,
        CBVTable,
        SRVTable,
        UAVTable,
    }
    public class RootSignature : IDisposable
    {
        public Dictionary<int, int> cbv = new Dictionary<int, int>();
        public Dictionary<int, int> srv = new Dictionary<int, int>();
        public Dictionary<int, int> uav = new Dictionary<int, int>();
        public ID3D12RootSignature rootSignature;
        public string Name;

        public void ReloadSkinning(GraphicsDevice graphicsDevice)
        {
            FeatureDataRootSignature featherData;
            featherData.HighestVersion = RootSignatureVersion.Version11;
            if (graphicsDevice.device.CheckFeatureSupport(Feature.RootSignature, ref featherData))
            {
                featherData.HighestVersion = RootSignatureVersion.Version10;
            }

            RootParameter1[] rootParameters = new RootParameter1[1];
            rootParameters[0] = new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(0, 0), ShaderVisibility.All);


            RootSignatureDescription1 rootSignatureDescription = new RootSignatureDescription1();
            rootSignatureDescription.Flags = RootSignatureFlags.AllowInputAssemblerInputLayout | RootSignatureFlags.AllowStreamOutput;
            rootSignatureDescription.Parameters = rootParameters;


            ThrowIfFailed(graphicsDevice.device.CreateRootSignature(0, rootSignatureDescription, out rootSignature));
        }

        public void Sign1(GraphicsDevice graphicsDevice, IReadOnlyList<GraphicSignatureDesc> Descs, RootSignatureFlags flags)
        {
            cbv.Clear();
            srv.Clear();
            uav.Clear();

            //static samplers
            StaticSamplerDescription[] samplerDescription = new StaticSamplerDescription[4];
            samplerDescription[0] = new StaticSamplerDescription(ShaderVisibility.All, 0, 0)
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                BorderColor = StaticBorderColor.OpaqueBlack,
                ComparisonFunction = ComparisonFunction.Never,
                Filter = Filter.MinMagMipLinear,
                MipLODBias = 0,
                MaxAnisotropy = 0,
                MinLOD = 0,
                MaxLOD = float.MaxValue,
                ShaderVisibility = ShaderVisibility.All,
                RegisterSpace = 0,
                ShaderRegister = 0,
            };
            samplerDescription[1] = samplerDescription[0];
            samplerDescription[2] = samplerDescription[0];
            samplerDescription[3] = samplerDescription[0];

            samplerDescription[1].ShaderRegister = 1;
            samplerDescription[2].ShaderRegister = 2;
            samplerDescription[3].ShaderRegister = 3;
            samplerDescription[1].MaxAnisotropy = 16;
            samplerDescription[1].Filter = Filter.Anisotropic;
            samplerDescription[2].ComparisonFunction = ComparisonFunction.Less;
            samplerDescription[2].Filter = Filter.ComparisonMinMagMipLinear;
            samplerDescription[3].Filter = Filter.MinMagMipPoint;

            RootParameter1[] rootParameters = new RootParameter1[Descs.Count];

            int cbvCount = 0;
            int srvCount = 0;
            int uavCount = 0;
            cbv.Clear();
            srv.Clear();
            uav.Clear();

            for (int i = 0; i < Descs.Count; i++)
            {
                GraphicSignatureDesc t = Descs[i];
                switch (t)
                {
                    case GraphicSignatureDesc.CBV:
                        rootParameters[i] = new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(cbvCount, 0), ShaderVisibility.All);
                        cbv[cbvCount] = i;
                        cbvCount++;
                        break;
                    case GraphicSignatureDesc.SRV:
                        rootParameters[i] = new RootParameter1(RootParameterType.ShaderResourceView, new RootDescriptor1(srvCount, 0), ShaderVisibility.All);
                        srv[srvCount] = i;
                        srvCount++;
                        break;
                    case GraphicSignatureDesc.UAV:
                        rootParameters[i] = new RootParameter1(RootParameterType.UnorderedAccessView, new RootDescriptor1(uavCount, 0), ShaderVisibility.All);
                        uav[uavCount] = i;
                        uavCount++;
                        break;
                    case GraphicSignatureDesc.CBVTable:
                        rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ConstantBufferView, 1, cbvCount)), ShaderVisibility.All);
                        cbv[cbvCount] = i;
                        cbvCount++;
                        break;
                    case GraphicSignatureDesc.SRVTable:
                        rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ShaderResourceView, 1, srvCount)), ShaderVisibility.All);
                        srv[srvCount] = i;
                        srvCount++;
                        break;
                    case GraphicSignatureDesc.UAVTable:
                        rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.UnorderedAccessView, 1, uavCount)), ShaderVisibility.All);
                        uav[uavCount] = i;
                        uavCount++;
                        break;
                }
            }

            RootSignatureDescription1 rootSignatureDescription = new RootSignatureDescription1();
            rootSignatureDescription.StaticSamplers = samplerDescription;
            rootSignatureDescription.Flags = flags;
            rootSignatureDescription.Parameters = rootParameters;

            rootSignature = graphicsDevice.device.CreateRootSignature<ID3D12RootSignature>(0, rootSignatureDescription);

        }

        public void Reload(GraphicsDevice graphicsDevice, GraphicSignatureDesc[] Descs)
        {
            Sign1(graphicsDevice, Descs, RootSignatureFlags.AllowInputAssemblerInputLayout | RootSignatureFlags.AllowStreamOutput);
        }

        public void ReloadCompute(GraphicsDevice graphicsDevice, IReadOnlyList<GraphicSignatureDesc> Descs)
        {
            Sign1(graphicsDevice, Descs, RootSignatureFlags.None);
        }

        public void Dispose()
        {
            rootSignature?.Dispose();
            rootSignature = null;
        }
    }
}
