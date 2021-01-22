#pragma once
#include "GraphicsContext.h"
namespace Coocoo3DGraphics
{
	static public ref class CooGExtension sealed
	{
	public:
		static void SetSRVTexture2(GraphicsContext^ context, Texture2D^ tex1, Texture2D^ tex2, int startSlot, Texture2D^ loading, Texture2D^ error);
		static void SetCBVBuffer3(GraphicsContext^ context, CBuffer^ buffer1, CBuffer^ buffer2, CBuffer^ buffer3, int startSlot);
	};
}