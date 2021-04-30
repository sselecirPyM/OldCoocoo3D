#include "pch.h"
#include "CooUtility.h"
#include <DirectXCollision.h>
using namespace Coocoo3DGraphics;
using namespace Windows::Foundation::Numerics;

int CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, float value)
{
	*(float*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

int CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, int value)
{
	*(int*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

int Coocoo3DGraphics::CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, unsigned int value)
{
	*(unsigned int*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

int CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float2 value)
{
	*(float2*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

int CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float3 value)
{
	*(float3*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

int CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float4 value)
{
	*(float4*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

int CooUtility::Write(const Platform::Array<byte>^ array, int startIndex, Windows::Foundation::Numerics::float4x4 value)
{
	*(float4x4*)(array->begin() + startIndex) = value;
	return sizeof(value);
}

bool Intersect()
{
	return false;
}
