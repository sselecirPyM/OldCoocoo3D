#pragma once
namespace Coocoo3DGraphics
{
	static public ref class CooUtility sealed
	{
	public:
		static int Write(const Platform::Array<byte>^ array, int startIndex, float value);
		static int Write(const Platform::Array<byte>^ array, int startIndex, int value);
		static int Write(const Platform::Array<byte>^ array, int startIndex, unsigned int value);
		static int Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float2 value);
		static int Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float3 value);
		static int Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float4 value);
		static int Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float4x4 value);
	};
}
