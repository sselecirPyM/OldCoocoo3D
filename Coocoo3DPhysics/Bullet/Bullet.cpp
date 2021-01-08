#include "pch.h"
#include "../MathUtility.h"
#include "Bullet.h"
#include "BulletScene.h"
#include "BulletRigidBody.h"
#include "BulletJoint.h"
using namespace Coocoo3DPhysics;
using namespace Coocoo3DNative;
const float c_worldScale = 1.0f;
void Bullet::Init()
{
	m_collisionConfiguration = new btDefaultCollisionConfiguration();
	m_dispatcher = new btCollisionDispatcher(m_collisionConfiguration);
	m_broadphase = new btDbvtBroadphase();
	m_solver = new btSequentialImpulseConstraintSolver();
}

void Bullet::InitScene(void* _scene)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	new(scene) BulletScene();
	scene->m_dynamicsWorld = new btDiscreteDynamicsWorld(m_dispatcher, m_broadphase, m_solver, m_collisionConfiguration);
	scene->m_dynamicsWorld->setGravity(btVector3(0, -9.81f, 0));
	btCollisionShape* groundShape = new btStaticPlaneShape(btVector3(0, 1, 0), 0);
	btDefaultMotionState* myMotionState = new btDefaultMotionState();
	btRigidBody::btRigidBodyConstructionInfo btInfo(0, myMotionState, groundShape, btVector3(0, 0, 0));
	btInfo.m_friction = 0.25f;
	btRigidBody* body = new btRigidBody(btInfo);
	body->setCollisionFlags(body->getCollisionFlags() || btCollisionObject::CF_KINEMATIC_OBJECT);
	body->setActivationState(DISABLE_DEACTIVATION);

	//add the body to the dynamics world
	scene->m_dynamicsWorld->addRigidBody(body);
}

void Bullet::SceneAddRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation, float3 scale, float mass, float restitution, float friction, float movDampling, float rotDampling, UINT8 shape, UINT8 type, UINT8 collisionGroup, UINT collisionMask)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);

	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	new(rigidBody)BulletRigidBody();
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

	float hx = scale.x / c_worldScale;
	float hy = scale.y / c_worldScale;
	float hz = scale.z / c_worldScale;
	btCollisionShape* collisionShape = nullptr;

	if (shape == 0)
	{
		collisionShape = new btSphereShape(hx);
	}
	else if (shape == 1)
	{
		collisionShape = new btBoxShape(btVector3(hx, hy, hz));
	}
	else if (shape == 2)
	{
		collisionShape = new btCapsuleShape(hx, hy);
	}

	btVector3 localInertia(0, 0, 0);
	if (type == 0)
	{
		//collisionShape->calculateLocalInertia(mass, localInertia);
		mass = 0;
	}
	else
	{
		collisionShape->calculateLocalInertia(mass, localInertia);
	}
	btTransform transform = Util::GetbtTransform2(position, rotation, c_worldScale);
	btDefaultMotionState* myMotionState = new btDefaultMotionState(transform);
	btRigidBody::btRigidBodyConstructionInfo rigidBdoyInfo(mass, myMotionState, collisionShape, localInertia);
	rigidBdoyInfo.m_friction = friction;
	rigidBdoyInfo.m_linearDamping = movDampling;
	rigidBdoyInfo.m_angularDamping = rotDampling;
	rigidBdoyInfo.m_restitution = restitution;
	btRigidBody* body = new btRigidBody(rigidBdoyInfo);
	body->setActivationState(DISABLE_DEACTIVATION);
	body->setSleepingThresholds(0, 0);
	if (type == 0)
		body->setCollisionFlags(body->getFlags() | btCollisionObject::CF_KINEMATIC_OBJECT);
	rigidBody->m_rigidBody = body;

	scene->m_dynamicsWorld->addRigidBody(body, 1 << collisionGroup, collisionMask);
}

void Bullet::SceneAddJoint(void* _scene, void* _joint, float3 position, quaternion rotation, void* _rigidBody1, void* _rigidBody2, float3 PositionMinimum, float3 PositionMaximum, float3 RotationMinimum, float3 RotationMaximum, float3 PositionSpring, float3 RotationSpring)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	auto r1 = reinterpret_cast<BulletRigidBody*>(_rigidBody1);
	auto r2 = reinterpret_cast<BulletRigidBody*>(_rigidBody2);

	btTransform t0 = Util::GetbtTransform2(position, rotation, c_worldScale);
	btTransform t1 = Util::GetbtTransform2(r1->m_position, r1->m_rotation, c_worldScale).inverseTimes(t0);
	btTransform t2 = Util::GetbtTransform2(r2->m_position, r2->m_rotation, c_worldScale).inverseTimes(t0);
	btGeneric6DofSpringConstraint* bt_constraint = new btGeneric6DofSpringConstraint(*r1->m_rigidBody, *r2->m_rigidBody, t1, t2, true);
	bt_constraint->setLinearLowerLimit(Util::GetbtVector3(PositionMinimum) / c_worldScale);
	bt_constraint->setLinearUpperLimit(Util::GetbtVector3(PositionMaximum) / c_worldScale);
	bt_constraint->setAngularLowerLimit(Util::GetbtVector3(RotationMinimum));
	bt_constraint->setAngularUpperLimit(Util::GetbtVector3(RotationMaximum));

	if (PositionSpring.x != 0.0f)
	{
		bt_constraint->enableSpring(0, true);
		bt_constraint->setStiffness(0, PositionSpring.x);
	}
	else
		bt_constraint->enableSpring(0, false);
	if (PositionSpring.y != 0.0f)
	{
		bt_constraint->enableSpring(1, true);
		bt_constraint->setStiffness(1, PositionSpring.y);
	}
	else
		bt_constraint->enableSpring(1, false);
	if (PositionSpring.z != 0.0f)
	{
		bt_constraint->enableSpring(2, true);
		bt_constraint->setStiffness(2, PositionSpring.z);
	}
	else
		bt_constraint->enableSpring(2, false);
	if (RotationSpring.x != 0.0f)
	{
		bt_constraint->enableSpring(3, true);
		bt_constraint->setStiffness(3, RotationSpring.x);
	}
	else
		bt_constraint->enableSpring(3, false);
	if (RotationSpring.y != 0.0f)
	{
		bt_constraint->enableSpring(4, true);
		bt_constraint->setStiffness(4, RotationSpring.y);
	}
	else
		bt_constraint->enableSpring(4, false);
	if (RotationSpring.z != 0.0f)
	{
		bt_constraint->enableSpring(5, true);
		bt_constraint->setStiffness(5, RotationSpring.z);
	}
	else
		bt_constraint->enableSpring(5, false);
	scene->m_dynamicsWorld->addConstraint(bt_constraint, false);
	auto joint = reinterpret_cast<BulletJoint*>(_joint);
	joint->m_joint = bt_constraint;
}

void Bullet::SceneResetRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	auto body = rigidBody->m_rigidBody;
	auto worldTransform = Util::GetbtTransform2(position, rotation, c_worldScale);
	body->getMotionState()->setWorldTransform(worldTransform);
	body->setCenterOfMassTransform(worldTransform);
	body->setInterpolationWorldTransform(worldTransform);
	body->setAngularVelocity(btVector3(0, 0, 0));
	body->setLinearVelocity(btVector3(0, 0, 0));
	body->setInterpolationAngularVelocity(btVector3(0, 0, 0));
	body->setInterpolationLinearVelocity(btVector3(0, 0, 0));
	body->clearForces();
}

void Bullet::SceneSimulate(void* _scene, double time)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	scene->m_dynamicsWorld->stepSimulation(static_cast<btScalar>(time, 60));
}

void Bullet::SceneFetchResults(void* _scene)
{
}

void Bullet::SceneMoveRigidBody(void* _scene, void* _rigidBody, float3 position, quaternion rotation)
{
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	auto scene = reinterpret_cast<BulletScene*>(_scene);

	auto body = rigidBody->m_rigidBody;
	body->getMotionState()->setWorldTransform(Util::GetbtTransform2(position, rotation, c_worldScale));
}

void Bullet::SceneMoveRigidBody(void* _scene, void* _rigidBody, float4x4 matrix)
{
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	auto scene = reinterpret_cast<BulletScene*>(_scene);

	auto body = rigidBody->m_rigidBody;
	body->getMotionState()->setWorldTransform(Util::GetbtTransform(matrix));
}

float3 Bullet::SceneGetRigidBodyPosition(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	auto scene = reinterpret_cast<BulletScene*>(_scene);

	auto body = rigidBody->m_rigidBody;

	btTransform trans;
	if (body && body->getMotionState())
	{
		body->getMotionState()->getWorldTransform(trans);
		auto pos = trans.getOrigin();
		float3 _pos;
		_pos.x = pos.x() * c_worldScale;
		_pos.y = pos.y() * c_worldScale;
		_pos.z = pos.z() * c_worldScale;
		return _pos;
	}
	else
	{
		//trans = obj->getWorldTransform();
	}
	return float3();
}

quaternion Bullet::SceneGetRigidBodyRotation(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	auto scene = reinterpret_cast<BulletScene*>(_scene);

	auto body = rigidBody->m_rigidBody;

	btTransform trans;
	if (body && body->getMotionState())
	{
		body->getMotionState()->getWorldTransform(trans);
		auto quat = trans.getRotation();
		quaternion _quat;
		_quat.x = quat.x();
		_quat.y = quat.y();
		_quat.z = quat.z();
		_quat.w = quat.w();
		return _quat;
	}
	else
	{
		//trans = obj->getWorldTransform();
	}
	return quaternion();
}

float4x4 Bullet::SceneGetRigidBodyTransform(void* _scene, void* _rigidBody)
{
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);
	auto scene = reinterpret_cast<BulletScene*>(_scene);

	auto body = rigidBody->m_rigidBody;

	btTransform trans;
	if (body && body->getMotionState())
	{
		body->getMotionState()->getWorldTransform(trans);
		float4x4 transform;
		trans.getOpenGLMatrix((btScalar*)&transform);
		return transform;
	}
	else
	{
		//trans = obj->getWorldTransform();
	}
	return float4x4();
}

void Bullet::SceneSetGravitation(void* _scene, float3 gravitation)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	scene->m_dynamicsWorld->setGravity(Util::GetbtVector3(gravitation) / c_worldScale);
}

void Bullet::SceneRemoveRigidBody(void* _scene, void* _rigidBody)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	auto rigidBody = reinterpret_cast<BulletRigidBody*>(_rigidBody);

	auto body = rigidBody->m_rigidBody;
	if (body && body->getMotionState())
	{
		delete body->getMotionState();
	}

	scene->m_dynamicsWorld->removeRigidBody(body);
	delete body;
	rigidBody->m_rigidBody = nullptr;
}

void Bullet::SceneRemoveJoint(void* _scene, void* _joint)
{
	auto scene = reinterpret_cast<BulletScene*>(_scene);
	auto joint = reinterpret_cast<BulletJoint*>(_joint);
	scene->m_dynamicsWorld->removeConstraint(joint->m_joint);
	delete(joint->m_joint);
	joint->m_joint = nullptr;
}
