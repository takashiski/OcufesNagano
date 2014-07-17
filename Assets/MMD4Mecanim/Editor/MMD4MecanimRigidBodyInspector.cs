using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[CustomEditor(typeof(MMD4MecanimRigidBody))]
public class MMD4MecanimRigidBodyInspector : Editor
{
	public override void OnInspectorGUI()
	{
		MMD4MecanimRigidBody rigidBody = this.target as MMD4MecanimRigidBody;

		MMD4MecanimBulletPhysics.RigidBodyProperty bulletPhysicsRigidBodyProperty = rigidBody.bulletPhysicsRigidBodyProperty;
		if( bulletPhysicsRigidBodyProperty == null ) {
			bulletPhysicsRigidBodyProperty = new MMD4MecanimBulletPhysics.RigidBodyProperty();
			rigidBody.bulletPhysicsRigidBodyProperty = bulletPhysicsRigidBodyProperty;
		}
		
		GUILayout.Label( "Bullet Physics Rigid Body", EditorStyles.boldLabel );
		
		bulletPhysicsRigidBodyProperty.isKinematic = EditorGUILayout.Toggle( "isKinematic", bulletPhysicsRigidBodyProperty.isKinematic );
		bulletPhysicsRigidBodyProperty.isAdditionalDamping = EditorGUILayout.Toggle( "isAdditionalDamping", bulletPhysicsRigidBodyProperty.isAdditionalDamping );
		
		if( bulletPhysicsRigidBodyProperty.isKinematic ) {
			GUI.enabled = false;
		}
		
		bulletPhysicsRigidBodyProperty.mass = EditorGUILayout.FloatField( "Mass", bulletPhysicsRigidBodyProperty.mass );
				
		if( bulletPhysicsRigidBodyProperty.isKinematic ) {
			GUI.enabled = true;
		}

		bulletPhysicsRigidBodyProperty.linearDamping = EditorGUILayout.FloatField( "LinearDamping", bulletPhysicsRigidBodyProperty.linearDamping );
		bulletPhysicsRigidBodyProperty.angularDamping = EditorGUILayout.FloatField( "AngularDamping", bulletPhysicsRigidBodyProperty.angularDamping );
		bulletPhysicsRigidBodyProperty.restitution = EditorGUILayout.FloatField( "Restitution", bulletPhysicsRigidBodyProperty.restitution );
		bulletPhysicsRigidBodyProperty.friction = EditorGUILayout.FloatField( "Friction", bulletPhysicsRigidBodyProperty.friction );
	}
}
