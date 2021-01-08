#pragma once
#include "GraphicsContext.h"
namespace Coocoo3DGraphics
{
	static public ref class CooGExtension sealed
	{
	public:
		static void SetSRVTexture2(GraphicsContext^ context, Texture2D^ tex1, Texture2D^ tex2, int startSlot, Texture2D^ loading, Texture2D^ error);
		static void SetCBVBuffer3(GraphicsContext^ context, ConstantBuffer^ buffer1, ConstantBuffer^ buffer2, ConstantBuffer^ buffer3, int startSlot);
	};
}