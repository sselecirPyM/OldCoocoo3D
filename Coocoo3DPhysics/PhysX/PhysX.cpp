#include "pch.h"
#include "PhysX.h"

#ifdef USE_PHYSX
#include "../MathUtility.h"
#include "PhysXScene.h"
#include "PhysXRigidBody.h"
#include "PhysXJoint.h"
#include <new>
using namespace Coocoo3DPhysics;
using namespace physx;
using namespace Windows::Foundation::Numerics;
using namespace Coocoo3DNative;

void PhysX::Init()
{
#define PVD_HOST "127.0.0.1"

	m_foundation = PxCreateFoundation(PX_PHYSICS_VERSION, gAllocator, gErrorCallback);

	m_pvd = PxCreatePvd(*m_foundation);
	PxPvdTransport* transport = PxDefaultPvdSocketTransportCreate(PVD_HOST, 5425, 10);
	m_pvd->connect(*transport, PxPvdInstrumentationFlag::eALL);
	m_physics = PxCreatePhysics(PX_PHYSICS_VERSION, *m_foundation, PxTolerancesScale(), true, m_pvd);
	PxInitExtensions(*m_physics, m_pvd);

	m_dispatcher = PxDefaultCpuDispatcherCreate(4);
}

void PhysX::InitScene(void* _scene)
{
	auto scene = reinterpret_cast<PhysXScene*>(_scene);
	new(scene) PhysXScene();

	auto physics = m_physics;
	PxSceneDesc sceneDesc(physics->getTolerancesScale());
	sceneDesc.gravity = PxVec3(0.0f, -9.81f, 0.0f);
	sceneDesc.cpuDispatcher = m_dispatcher;
	sceneDesc.filterShader = PxDefaultSimulationFilterShader;
	//sceneDesc.filterShader = FilterShader1;
	scene->m_scene = physics->createScene(sceneDesc);
	scene->m_scene->setVisualizationParameter(PxVisualizationParameter::eJOINT_LOCAL_FRAMES, 1.0f);
	scene->m_scene->setVisualizationParameter(PxVisualizationParameter::eJOINT_LIMITS, 1.0f);

	PxPvdSceneClient* pvdClient = scene->m_scene->getScenePvdClient();
	if (pvdClient)
	{
		pvdClient->setScenePvdFlag(PxPvdSceneFlag::eTRANSMIT_CONSTRAINTS, true);
		pvdClient->setScenePvdFlag(PxPvdSceneFlag::eTRANSMIT_CONTACTS, true);
		pvdClient->setScenePvdFlag(PxPvdSceneFlag::eTRANSMIT_SCENEQUERIES, true);
	}
	scene->m_material = physics->createMaterial(0.5f, 0.5f, 0.6f);
	PxRigidStatic* groundPlane = PxCreatePlane(*physics, PxPlane(0, 1, 0, 0), *scene->m_material);
	scene->m_scene->addActor(*groundPlane);
}

void PhysX::SceneAddRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation, float3 scale, float mass, float restitution, float friction, float movDampling, float rotDampling, UINT8 shape, UINT8 type, UINT8 collisionGroup, UINT collisionMask)
{
	auto scene = reinterpret_cast<PhysXScene*>(_scene);

	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	new(rigidBody)PhysXRigidBody();
	rigidBody->m_position = position;
	rigidBody->m_rotation = rotation;
	rigidBody->m_mass = mass;
	rigidBody->m_shape = shape;
	rigidBody->m_collisionGroup = collisionGroup;
	rigidBody->m_collisionMask = collisionMask;
	rigidBody->m_scale = scale;
	rigidBody->m_movDampling = movDampling;
	rigidBody->m_rotDampling = rotDampling;
	rigidBody->m_restitution = restitution;
	rigidBody->m_friction = friction;
	rigidBody->m_type = type;

	rigidBody->m_material = m_physics->createMaterial(rigidBody->m_friction, rigidBody->m_friction, rigidBody->m_restitution);
	PxTransform transform = Util::GetPxTransform(position, rotation);
	PxTransform ofs = (PxTransform)PxIdentity;
	float hx = scale.x;
	float hy = scale.y;
	float hz = scale.z;
	PxGeometry* geometry = nullptr;

	if (shape == 0)
	{
		geometry = &PxSphereGeometry(hx);
	}
	else if (shape == 1)
	{
		geometry = &PxBoxGeometry(hx, hy, hz);
	}
	else if (shape == 2)
	{
		geometry = &PxCapsuleGeometry(hx, hy / 2);
		ofs.q = PxQuat::PxQuat(-PxPi / 2, PxVec3(0, 0, 1));
	}
	PxShape* shape1 = shape1 = m_physics->createShape(*geometry, *rigidBody->m_material);
	shape1->setLocalPose(ofs);
	PxFilterData filterData(1 << collisionGroup, collisionMask, 0, 0);
	shape1->setSimulationFilterData(filterData);
	shape1->setContactOffset(0.2f);
	shape1->setRestOffset(0.1f);
	if (type == 0)
	{
		//rigidBody->m_rigidBody = PxCreateKinematic(*m_physics3D->m_physics, transform, *geometry, *rigidBody->m_material, mass, ofs);
		rigidBody->m_rigidBody = PxCreateKinematic(*m_physics, transform, *shape1, mass);
	}
	else
	{
		//rigidBody->m_rigidBody = PxCreateDynamic(*m_physics3D->m_physics, transform, *geometry, *rigidBody->m_material, mass, ofs);
		rigidBody->m_rigidBody = PxCreateDynamic(*m_physics, transform, *shape1, mass);
	}
	rigidBody->m_rigidBody->setAngularDamping(rotDampling);
	rigidBody->m_rigidBody->setLinearDamping(movDampling);
	//rigidBody->m_rigidBody->setDominanceGroup(collisionGroup);
	rigidBody->m_rigidBody->setMass(mass);
	shape1->release();
	scene->m_scene->addActor(*rigidBody->m_rigidBody);
}

void PhysX::SceneAddJoint(void* _scene, void* _joint, float3 position, quaternion rotation, void* _rigidBody1, void* _rigidBody2, float3 PositionMinimum, float3 PositionMaximum, float3 RotationMinimum, float3 RotationMaximum, float3 PositionSpring, float3 RotationSpring)
{
	auto r1 = reinterpret_cast<PhysXRigidBody*>(_rigidBody1);
	auto r2 = reinterpret_cast<PhysXRigidBody*>(_rigidBody2);
	auto pos = Util::GetPxVec3(position);
	auto pos1 = Util::GetPxVec3(r1->m_position);
	auto pos2 = Util::GetPxVec3(r2->m_position);
	auto rot = Util::GetPxQuat(rotation);
	auto rot1 = Util::GetPxQuat(r1->m_rotation);
	auto rot2 = Util::GetPxQuat(r2->m_rotation);
	PxTransform t1(pos - pos1, (rot1 * rot.getConjugate()).getNormalized());
	PxTransform t2(pos - pos2, (rot2 * rot.getConjugate()).getNormalized());
	auto j = PxD6JointCreate(*m_physics, r1->m_rigidBody, t1, r2->m_rigidBody, t2);
	j->setMotion(PxD6Axis::eSWING1, PxD6Motion::eFREE);
	j->setMotion(PxD6Axis::eSWING2, PxD6Motion::eFREE);
	j->setMotion(PxD6Axis::eTWIST, PxD6Motion::eFREE);
	j->setMotion(PxD6Axis::eX, PxD6Motion::eLIMITED);
	j->setMotion(PxD6Axis::eY, PxD6Motion::eLIMITED);
	j->setMotion(PxD6Axis::eZ, PxD6Motion::eLIMITED);
	j->setConstraintFlag(PxConstraintFlag::eVISUALIZATION,true);
	PxJointLinearLimitPair limitPair1(PositionMinimum.x, PositionMaximum.x, PxSpring(PositionSpring.x, 0.0f));
	PxJointLinearLimitPair limitPair2(PositionMinimum.y, PositionMaximum.y, PxSpring(PositionSpring.y, 0.0f));
	PxJointLinearLimitPair limitPair3(PositionMinimum.z, PositionMaximum.z, PxSpring(PositionSpring.z, 0.0f));
	PxJointLinearLimitPair limitPair4(RotationMinimum.x, RotationMaximum.x, PxSpring(RotationSpring.x, 0.0f));
	PxJointLinearLimitPair limitPair5(RotationMinimum.y, RotationMaximum.y, PxSpring(RotationSpring.y, 0.0f));
	PxJointLinearLimitPair limitPair6(RotationMinimum.z, RotationMaximum.z, PxSpring(RotationSpring.z, 0.0f));
	j->setLinearLimit(PxD6Axis::eX, limitPair1);
	j->setLinearLimit(PxD6Axis::eY, limitPair2);
	j->setLinearLimit(PxD6Axis::eZ, limitPair3);
	//j->setLinearLimit(PxD6Axis::eSWING1, limitPair4);
	//j->setLinearLimit(PxD6Axis::eSWING2, limitPair5);
	//j->setLinearLimit(PxD6Axis::eTWIST, limitPair6);
	//j->setDrive(PxD6Drive::eSLERP, PxD6JointDrive(0, 1000, FLT_MAX, true));
	auto joint = reinterpret_cast<PhysXJoint*>(_joint);
	new(joint) PhysXJoint();
	joint->m_joint = j;
}

void PhysX::SceneResetRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation)
{
	auto scene = reinterpret_cast<PhysXScene*>(_scene);
	auto r1 = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	r1->m_rigidBody->setGlobalPose(Util::GetPxTransform(position,rotation));
	r1->m_rigidBody->setLinearVelocity(PxVec3());
	r1->m_rigidBody->setAngularVelocity(PxVec3());
}

void PhysX::SceneSimulate(void* _scene, double time)
{
	auto scene = reinterpret_cast<PhysXScene*>(_scene);
	scene->m_scene->simulate(static_cast<PxReal>(time));
}

void PhysX::SceneFetchResults(void* _scene)
{
	auto scene = reinterpret_cast<PhysXScene*>(_scene);
	scene->m_scene->fetchResults(true);
}

void PhysX::SceneMoveRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation)
{
	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	rigidBody->m_rigidBody->setKinematicTarget(Util::GetPxTransform(position, rotation));
}

void PhysX::SceneMoveRigidBody(void* _scene, void* _rigidBody, float4x4 matrix)
{
	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	rigidBody->m_rigidBody->setKinematicTarget(Util::GetPxTransform(matrix));
}

float3 PhysX::SceneGetRigidBodyPosition(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	PxTransform transformM = rigidBody->m_rigidBody->getGlobalPose();
	auto vec1 = float3();
	vec1.x = transformM.p.x;
	vec1.y = transformM.p.y;
	vec1.z = transformM.p.z;
	return vec1;
}

quaternion PhysX::SceneGetRigidBodyRotation(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	PxTransform transformM = rigidBody->m_rigidBody->getGlobalPose();
	auto rot1 = quaternion();
	rot1.x = transformM.q.x;
	rot1.y = transformM.q.y;
	rot1.z = transformM.q.z;
	rot1.w = transformM.q.w;
	return rot1;
}

float4x4 PhysX::SceneGetRigidBodyTransform(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	PxTransform transformM = rigidBody->m_rigidBody->getGlobalPose();
	PxMat44 matrix1(transformM);
	float4x4 matrix2;
	memcpy(&matrix2, &matrix1, sizeof(matrix2));
	return matrix2;
}

void PhysX::SceneSetGravitation(void* _scene, float3 gravitation)
{
	auto scene = reinterpret_cast<PhysXScene*>(_scene);
	scene->m_scene->setGravity(Util::GetPxVec3(gravitation));
}

void PhysX::SceneRemoveRigidBody(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<PhysXRigidBody*>(_rigidBody);
	rigidBody->m_rigidBody->release();
	rigidBody->m_material->release();
	rigidBody->m_rigidBody = nullptr;
	rigidBody->m_material = nullptr;
}

void PhysX::SceneRemoveJoint(void* _scene, void* _joint)
{
	auto joint = reinterpret_cast<PhysXJoint*>(_joint);
	joint->m_joint->release();
}

PhysX::~PhysX()
{
	m_physics->release();
	m_foundation->release();
}

PxFilterFlags PhysX::FilterShader1(PxFilterObjectAttributes attributes0, PxFilterData filterData0, PxFilterObjectAttributes attributes1, PxFilterData filterData1, PxPairFlags& pairFlags, const void* constantBlock, PxU32 constantBlockSize)
{
	if ((filterData0.word0 & filterData1.word1) && (filterData0.word1 & filterData1.word0))
	{
		pairFlags = PxPairFlag::eCONTACT_DEFAULT;
		return PxFilterFlags(PxFilterFlag::eDEFAULT);
	}
	return PxFilterFlags(PxFilterFlag::eSUPPRESS);
}
#endif