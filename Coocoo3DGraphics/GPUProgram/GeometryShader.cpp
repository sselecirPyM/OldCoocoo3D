#include "pch.h"
#include "GeometryShader.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

void GeometryShader::Initialize(const Platform::Array<byte>^ data)
{
	D3DCreateBlob(data->Length, &byteCode);
	memcpy(byteCode->GetBufferPointer(), data->begin(), data->Length);
}
