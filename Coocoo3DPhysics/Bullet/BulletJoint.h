#pragma once
#include "btBulletDynamicsCommon.h"
namespace Coocoo3DNative
{
	using namespace Windows::Foundation::Numerics;
	class BulletJoint
	{
	public:
		btTypedConstraint* m_joint = nullptr;
	};
}