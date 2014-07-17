#define _PMX_JOINWORLD_TPOSE_ONLY

using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

using PMXModel      = MMD4MecanimBulletPMXModel;
using PMXRigidBody  = MMD4MecanimBulletPMXRigidBody;

public class MMD4MecanimBulletPMXJoint
{
	public PMXModel _model;
	
	public PMXModel model { get { return _model; } }

	// Pending: Replace to MMD4MecanimData

	int 			                _targetRigidBodyIDA;
	int 		                	_targetRigidBodyIDB;
	PMXRigidBody                	_targetRigidBodyA;
	PMXRigidBody	                _targetRigidBodyB;
	Vector3	                    	_position;
	Vector3		                    _rotation;
	Vector3                 		_limitPosFrom;
	Vector3		                    _limitPosTo;
	Vector3		                    _limitRotFrom;
	Vector3		                    _limitRotTo;
	Vector3                 		_springPosition;
	Vector3                     	_springRotation;

    Generic6DofSpringConstraint     _bulletConstraint;
	DiscreteDynamicsWorld           _bulletWorld;

    ~MMD4MecanimBulletPMXJoint()
    {
        Destroy();
    }

	public void Destroy()
    {
        LeaveWorld();

        if( _bulletConstraint != null ) {
            _bulletConstraint.Cleanup();
            _bulletConstraint = null;
        }

        _bulletWorld = null;
        _model = null;
    }

	public bool Import( MMD4MecanimCommon.BinaryReader binaryReader )
    {
        if( !binaryReader.BeginStruct() ) {
	        Debug.LogError( "BeginStruct() failed." );
	        return false;
        }

        binaryReader.ReadStructInt(); // additionalFlags
        binaryReader.ReadStructInt(); // nameJp
        binaryReader.ReadStructInt(); // nameEn
        binaryReader.ReadStructInt(); // jointType
        _targetRigidBodyIDA	= binaryReader.ReadStructInt();
        _targetRigidBodyIDB	= binaryReader.ReadStructInt();

        _position			= binaryReader.ReadStructVector3();
        _rotation			= binaryReader.ReadStructVector3();
        _limitPosFrom		= binaryReader.ReadStructVector3();
        _limitPosTo			= binaryReader.ReadStructVector3();
        _limitRotFrom		= binaryReader.ReadStructVector3();
        _limitRotTo			= binaryReader.ReadStructVector3();
        _springPosition		= binaryReader.ReadStructVector3();
        _springRotation		= binaryReader.ReadStructVector3();

        if( _model != null ) {
	        _position *= _model.modelToBulletScale;
	        _limitPosFrom *= _model.modelToBulletScale;
	        _limitPosTo *= _model.modelToBulletScale;
	        _springPosition *= _model.modelToBulletScale;
        }

        if( !binaryReader.EndStruct() ) {
	        Debug.LogError( "EndStruct() failed." );
	        return false;
        }

        if( _model != null ) {
	        _targetRigidBodyA = _model.GetRigidBody( _targetRigidBodyIDA );
	        _targetRigidBodyB = _model.GetRigidBody( _targetRigidBodyIDB );
        }

        return true;
    }

	private bool _SetupConstraint()
    {
        if( _bulletConstraint != null ) {
            return true;
        }

        if( _targetRigidBodyA == null || _targetRigidBodyB == null ) {
		    if( _targetRigidBodyA == null ) {
			    //Debug.LogWarning( "Warning: Not found RigidBody " + _targetRigidBodyIDA );
		    }
		    if( _targetRigidBodyB == null ) {
			    //Debug.LogWarning( "Warning: Not found RigidBody " + _targetRigidBodyIDB );
		    }
		    return true;
        }

	    RigidBody rigidBodyA = _targetRigidBodyA.bulletRigidBody;
	    RigidBody rigidBodyB = _targetRigidBodyB.bulletRigidBody;
        if( rigidBodyA == null || rigidBodyB == null ) {
	        return false;
        }

		IndexedMatrix tr = IndexedMatrix.Identity;
		Vector3 rhRotation = new Vector3( -_rotation.x, -_rotation.y, _rotation.z );
		tr._basis = MMD4MecanimBulletPhysicsUtil.BasisRotationYXZ( ref rhRotation );
        tr._origin = new IndexedVector3( _position.x, _position.y, -_position.z );

        if( _model != null && _model.rootBone != null ) {
#if _PMX_JOINWORLD_TPOSE_ONLY // Memo: Require to T-Pose only.
            tr._origin += _model.rootBone.worldTransform._origin;
#else
            tr = _model.rootBone.worldTransform * tr;
#endif
        }

        IndexedMatrix trA = rigidBodyA.GetWorldTransform().Inverse() * tr;
        IndexedMatrix trB = rigidBodyB.GetWorldTransform().Inverse() * tr;

        _bulletConstraint = new Generic6DofSpringConstraint(rigidBodyA, rigidBodyB, trA, trB, true );

        _bulletConstraint.SetLinearUpperLimit(new IndexedVector3(_limitPosTo[0], _limitPosTo[1], -_limitPosFrom[2]));
        _bulletConstraint.SetLinearLowerLimit(new IndexedVector3(_limitPosFrom[0], _limitPosFrom[1], -_limitPosTo[2]));

        _bulletConstraint.SetAngularUpperLimit(new IndexedVector3(-_limitRotFrom[0], -_limitRotFrom[1], _limitRotTo[2]));
        _bulletConstraint.SetAngularLowerLimit(new IndexedVector3(-_limitRotTo[0], -_limitRotTo[1], _limitRotFrom[2]));

        for (int i = 0; i < 6; i++) {
            if (i >= 3 || _springPosition[i] != 0.0f) {
                _bulletConstraint.EnableSpring(i, true);
		        if( i >= 3 ) {
	                _bulletConstraint.SetStiffness(i, _springRotation[i - 3]);
		        } else {
	                _bulletConstraint.SetStiffness(i, _springPosition[i]);
		        }
            }
        }

        return true;
    }

	public bool JoinWorld()
    {
        if( _bulletConstraint == null ) {
	        if( !_SetupConstraint() ) {
		        Debug.LogError( "PMXJoint::JoinWorld:SetupConstraint() failed." );
		        return false;
	        }
            if( _bulletConstraint == null ) {
                return true; // No Effects.
            }
        }

        if( _bulletWorld != null || _bulletConstraint == null || _model == null || _model.bulletWorld == null ) {
	        Debug.LogError( "PMXJoint::JoinWorld: null." );
	        return false;
        }

        _bulletWorld = _model.bulletWorld;
        _bulletWorld.AddConstraint( _bulletConstraint );
        return true;
    }

	public void LeaveWorld()
    {
        if( _bulletConstraint != null ) {
            if( _bulletWorld != null ) {
                _bulletWorld.RemoveConstraint( _bulletConstraint );
            }
            _bulletConstraint.Cleanup();
            _bulletConstraint = null;
        }

        _bulletWorld = null;
    }
}
