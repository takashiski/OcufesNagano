using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

public class MMD4MecanimBulletRigidBody : MMD4MecanimBulletPhysicsEntity
{
	// Pending: Replace to MMD4MecanimData

	bool						_isKinematic;
	int							_group = -1;
	int							_mask = -1;
	float						_unityToBulletScale;
	float						_bulletToUnityScale;
	CollisionShape				_shape;
	IMotionState				_motionState;
	RigidBody					_body;
	DiscreteDynamicsWorld		_world;
	
	public struct CreateProperty
	{
		public bool			isKinematic;
		public bool			additionalDamping;
		public int			group;
		public int			mask;
		public int			shapeType;
		public Vector3		shapeSize;
		public Vector3		position;
		public Quaternion	rotation;
		public float		mass;
		public float		linearDamping;
		public float		angularDamping;
		public float		restitution;
		public float		friction;
		public float		unityScale;
	}
	
	public MMD4MecanimBulletRigidBody()
	{
	}

	~MMD4MecanimBulletRigidBody()
	{
		LeaveWorld();
	}

	public bool Create( ref CreateProperty createProperty )
	{
		_isKinematic = createProperty.isKinematic;
		_group = createProperty.group;
		_mask = createProperty.mask;
		
		_bulletToUnityScale = 1.0f;
		_unityToBulletScale = 1.0f;
		if( createProperty.unityScale > Mathf.Epsilon ) {
			_bulletToUnityScale = createProperty.unityScale;
			_unityToBulletScale = 1.0f / _bulletToUnityScale;
		}
		
		Vector3 shapeSize = createProperty.shapeSize * _unityToBulletScale;

		switch( createProperty.shapeType ) {
		case 0:
			_shape = new SphereShape( shapeSize.x );
			break;
		case 1:
			_shape = new BoxShape( new IndexedVector3( shapeSize.x, shapeSize.y, shapeSize.z ) );
			break;
		case 2:
			_shape = new CapsuleShape( shapeSize.x, shapeSize.y );
			break;
		default:
			return false;
		}

		Vector3 position = createProperty.position;
		position *= _unityToBulletScale;

		IndexedMatrix startTransform = MMD4MecanimBulletPhysicsUtil.MakeIndexedMatrix(
			ref position, ref createProperty.rotation );
		
		if( _isKinematic ) {
        	_motionState = new MMD4MecanimBulletPhysicsUtil.KinematicMotionState( ref startTransform );
		} else {
			_motionState = new MMD4MecanimBulletPhysicsUtil.SimpleMotionState( ref startTransform );
		}
		
		float mass = _isKinematic ? 0.0f : createProperty.mass;
        bool isDynamic = mass != 0.0f;
        IndexedVector3 localInertia = IndexedVector3.Zero;
        if( isDynamic ) {
	        _shape.CalculateLocalInertia(mass, out localInertia);
        }
		
        RigidBodyConstructionInfo rbInfo = new RigidBodyConstructionInfo(mass, _motionState, _shape, localInertia);
		rbInfo.m_additionalDamping = createProperty.additionalDamping;
        _body = new RigidBody(rbInfo);

		if( _isKinematic ) {
			_body.SetCollisionFlags( _body.GetCollisionFlags() | BulletXNA.BulletCollision.CollisionFlags.CF_KINEMATIC_OBJECT );
		}
		
		return true;
	}

	public void Destroy()
	{
		LeaveWorld();

		_world = null;
		if( _body != null ) {
			_body.Cleanup();
			_body = null;
		}
		_motionState = null;
		_shape = null;
	}

	public void Update( ref Vector3 position, ref Quaternion rotation )
	{
		if( _isKinematic && _motionState != null ) {
			Vector3 fixed_position = position * _unityToBulletScale;
			MMD4MecanimBulletPhysicsUtil.MakeIndexedMatrix(
					ref ((MMD4MecanimBulletPhysicsUtil.KinematicMotionState)_motionState).m_graphicsWorldTrans,
					ref fixed_position, ref rotation );
		}
	}

	public void LateUpdate( ref Vector3 position, ref Quaternion rotation )
	{
		if( _body != null ) {
			IndexedMatrix transform = _body.GetCenterOfMassTransform();
			position = transform._origin.ToVector3() * _bulletToUnityScale;
			rotation = transform._basis.GetRotation();
		}
	}
	
	public override bool _JoinWorld()
	{
		if( _body == null ) {
			return false;
		}
		if( this.bulletWorld == null ) {
			return false;
		}
		
		_world = this.bulletWorld;
		if( _group < 0 && _mask < 0 ) {
			_world.AddRigidBody( _body );
		} else {
			_world.AddRigidBody( _body, (CollisionFilterGroups)_group, (CollisionFilterGroups)_mask );
		}

		return true;
	}

	public override void _LeaveWorld()
	{
		if( _body != null ) {
            if( _world != null ) {
    			_world.RemoveRigidBody( _body );
            }
		}
		
		_world = null;
	}
}
