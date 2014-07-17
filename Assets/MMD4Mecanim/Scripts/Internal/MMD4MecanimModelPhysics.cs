using UnityEngine;
using System.Collections;

public partial class MMD4MecanimModel
{
	void _InitializeRigidBody()
	{
		if( _modelData == null ) {
			this.rigidBodyList = null;
			return;
		}

		if( _modelData.rigidBodyDataList == null ) {
			this.rigidBodyList = null;
			return;
		}

		if( this.rigidBodyList != null ) {
			for( int i = 0; i < this.rigidBodyList.Length; ++i ) {
				if( this.rigidBodyList[i] == null || this.rigidBodyList[i].rigidBodyData == null ) {
					this.rigidBodyList = null;
					break;
				}
			}
		}

		if( this.rigidBodyList == null || this.rigidBodyList.Length != _modelData.rigidBodyDataList.Length ) {
			this.rigidBodyList = new RigidBody[_modelData.rigidBodyDataList.Length];
			for( int i = 0; i < this.rigidBodyList.Length; ++i ) {
				this.rigidBodyList[i] = new RigidBody();
				this.rigidBodyList[i].rigidBodyData = _modelData.rigidBodyDataList[i];
				this.rigidBodyList[i].enabled = !this.rigidBodyList[i].rigidBodyData.isDisabled;
			}
		}
	}

	void _InitializePhysicsEngine()
	{
		if( this.modelFile == null ) {
			Debug.LogWarning( this.gameObject.name + ":modelFile is nothing." );
			return;
		}
		
		if( this.physicsEngine == PhysicsEngine.BulletPhysics ) {
			MMD4MecanimBulletPhysics instance = MMD4MecanimBulletPhysics.instance;
			if( instance != null ) {
				_bulletPhysicsMMDModel = instance.CreateMMDModel( this );
				_UpdateRigidBody(); // Feedback isDisabled
			}
		}
	}

	public void SetGravity( float gravityScale, float gravityNoise, Vector3 gravityDirection )
	{
		// for local world only.
		if( this.bulletPhysics != null && this.bulletPhysics.worldProperty != null ) {
			this.bulletPhysics.worldProperty.gravityScale = gravityScale;
			this.bulletPhysics.worldProperty.gravityNoise = gravityNoise;
			this.bulletPhysics.worldProperty.gravityDirection = gravityDirection;
		}
	}

	void _UpdateRigidBody()
	{
		if( _bulletPhysicsMMDModel != null && !_bulletPhysicsMMDModel.isExpired ) {
			if( _bulletPhysicsMMDModel.localWorld != null ) {
				if( this.bulletPhysics != null && this.bulletPhysics.worldProperty != null ) {
					_bulletPhysicsMMDModel.localWorld.SetGravity(
						this.bulletPhysics.worldProperty.gravityScale,
						this.bulletPhysics.worldProperty.gravityNoise,
						this.bulletPhysics.worldProperty.gravityDirection );
				}
			}

			if( this.rigidBodyList != null ) {
				for( int i = 0; i < this.rigidBodyList.Length; ++i ) {
					if( this.rigidBodyList[i] != null ) {
						if( this.rigidBodyList[i]._enabledCached == -1 ||
						    this.rigidBodyList[i].enabled != (this.rigidBodyList[i]._enabledCached != 0) ) {
							this.rigidBodyList[i]._enabledCached = (this.rigidBodyList[i].enabled ? 1 : 0);
							_InvalidateProcessingBoneList();

							_bulletPhysicsMMDModel.SetRigidBodyEnabled( i, this.rigidBodyList[i].enabled );
						}
					}
				}
			}
		}
	}
}
