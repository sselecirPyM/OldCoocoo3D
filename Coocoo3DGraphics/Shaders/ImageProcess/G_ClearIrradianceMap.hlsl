#include "../RandomNumberGenerator.hlsli"
cbuffer cb0 : register(b0)
{
	uint2 imageSize;
}
RWTexture2DArray<float4> IrradianceMap : register(u0);
TextureCube Image : register(t0);
SamplerState s0 : register(s0);
[numthreads(8, 8, 1)]
void main(uint3 dtid : SV_DispatchThreadID)
{
	IrradianceMap[dtid] = float4(0, 0, 0, 1);
}