#pragma once
#include "UnionStructDefine.h"
namespace Coocoo3DPhysics
{
	using namespace Windows::Foundation::Numerics;
	public ref class Physics3DRigidBody sealed
	{
	public:
	internal:
		byte m_rigidBodyData[MAX_UNION_RIGID_BODY_STRUCTURE_SIZE];
	};
}
