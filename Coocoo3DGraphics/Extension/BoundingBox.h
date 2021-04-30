#pragma once

namespace Coocoo3DGraphics
{
	public value struct BoundingBox sealed
	{
		Windows::Foundation::Numerics::float3 position;
		Windows::Foundation::Numerics::float3 extension;
	};
}