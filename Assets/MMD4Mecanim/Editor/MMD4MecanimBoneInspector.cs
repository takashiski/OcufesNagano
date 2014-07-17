using UnityEditor;
using UnityEngine;
using System.Collections;

[CustomEditor(typeof(MMD4MecanimBone))]
public class MMD4MecanimBoneInspector : Editor
{
	Vector3 _eulerAngles;

	void _RefreshEulerAngles( MMD4MecanimBone bone )
	{
		Vector3 eulerAngles = MMD4MecanimCommon.NormalizeAsDegree( bone.userEulerAngles );
		for( int i = 0; i < 3; ++i ) {
			if( Mathf.Abs(eulerAngles[i] - MMD4MecanimCommon.NormalizeAsDegree(_eulerAngles[i])) > 0.001f ) {
				_eulerAngles[i] = Mathf.Ceil( eulerAngles[i] * 1000.0f ) / 1000.0f;
				_eulerAngles[i] = Mathf.Clamp( _eulerAngles[i], -180.0f, 180.0f );
			}
		}
	}

	public override void OnInspectorGUI()
	{
		MMD4MecanimBone bone = this.target as MMD4MecanimBone;
		_RefreshEulerAngles( bone );

		GUILayout.Label( "Information" );
		if( bone.boneData != null ) {
			EditorGUILayout.TextField( "Name", "" + bone.boneID + " : " + bone.boneData.nameJp );
			#if MMD4MECANIM_DEBUG
			EditorGUILayout.ObjectField( "ParentBone", ( bone.parentBone != null ) ? bone.parentBone.gameObject : null, typeof(GameObject), false );
			EditorGUILayout.ObjectField( "OriginalParentBone", ( bone.originalParentBone != null ) ? bone.originalParentBone.gameObject : null, typeof(GameObject), false );
			EditorGUILayout.Toggle( "ModifiedHierarchy", bone.isModifiedHierarchy );
			EditorGUILayout.Toggle( "isIKDepended", bone.isIKDepended );
			EditorGUILayout.ObjectField( "InherenceParentBone", ( bone.inherenceParentBone != null ) ? bone.inherenceParentBone.gameObject : null, typeof(GameObject), false );
			EditorGUILayout.Vector3Field( "InherenceRotation", bone.inherenceRotation.eulerAngles );
			EditorGUILayout.FloatField( "InherenceWeight", bone.inherenceWeight );
			#endif
		}
		EditorGUILayout.Separator();
		GUILayout.Label( "UserRotation" );
		Vector3 eulerAngles2 = Vector3.zero;
		for( int i = 0; i < 3; ++i ) {
			EditorGUILayout.BeginHorizontal();
			switch( i ) {
			case 0: GUILayout.Label("X"); break;
			case 1: GUILayout.Label("Y"); break;
			case 2: GUILayout.Label("Z"); break;
			}
			eulerAngles2[i] = EditorGUILayout.Slider( _eulerAngles[i], -180.0f, 180.0f );
			EditorGUILayout.EndHorizontal();
		}

		bone.ikEnabled = EditorGUILayout.Toggle("IKEnabled", bone.ikEnabled);
		bone.ikWeight = EditorGUILayout.Slider( "IKWeight", bone.ikWeight, 0.0f, 1.0f );
		bone.ikGoal = (GameObject)EditorGUILayout.ObjectField("IKGoal", (Object)bone.ikGoal, typeof(GameObject), true);

		if( Mathf.Abs(_eulerAngles.x - eulerAngles2.x) > Mathf.Epsilon ||
		    Mathf.Abs(_eulerAngles.y - eulerAngles2.y) > Mathf.Epsilon ||
		    Mathf.Abs(_eulerAngles.z - eulerAngles2.z) > Mathf.Epsilon ) {
			_eulerAngles = eulerAngles2;
			bone.userEulerAngles = eulerAngles2;
		}
	}
}
