using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public partial class MMD4MecanimModel
{
	bool				_isUseXDEF;
	int					_xdefSDEFIndex;
	Matrix4x4			_xdefRootTransformInv;
	Quaternion			_xdefRootRotationInv;
	Matrix4x4[]			_xdefBoneTransformCache;
	Matrix4x4[]			_xdefBoneTransformInvCache;
	Quaternion[]		_xdefBoneRotationCache;

	void _InitializeVertex()
	{
		// Memo: Call before _InitializeCloneMesh()
		if( !Application.isPlaying ) {
			return;
		}
		if( !this.xdefEnabled ) {
			return;
		}
		#if UNITY_IPHONE || UNITY_ANDROID
		if( !this.xdefMobileEnabled ) {
			return;
		}
		#endif
		if( this.vertexFile == null ) {
			return;
		}

		_vertexData = MMD4MecanimAuxData.BuildVertexData( this.vertexFile );
		if( _vertexData == null ) {
			Debug.LogError( this.gameObject.name + ":vertexFile is unsupported format." );
			return;
		}

		int meshCount = _vertexData.meshCount;
		int boneCountMax = 0;

		MMD4MecanimAuxData.VertexData.MeshBoneInfo meshBoneInfo = new MMD4MecanimAuxData.VertexData.MeshBoneInfo();
		for( int meshIndex = 0; meshIndex < meshCount; ++meshIndex ) {
			_vertexData.GetMeshBoneInfo( ref meshBoneInfo, meshIndex );
			if( meshBoneInfo.isSDEF ) { // Now supported SDEF only.
				_isUseXDEF = true;
			}
			boneCountMax = Mathf.Max( boneCountMax, meshBoneInfo.count );
		}

		_xdefBoneTransformCache		= new Matrix4x4[boneCountMax];
		_xdefBoneTransformInvCache	= new Matrix4x4[boneCountMax];
		_xdefBoneRotationCache		= new Quaternion[boneCountMax];
		for( int i = 0; i < boneCountMax; ++i ) {
			_xdefBoneTransformCache[i]		= Matrix4x4.identity;
			_xdefBoneTransformInvCache[i]	= Matrix4x4.identity;
			_xdefBoneRotationCache[i]		= Quaternion.identity;
		}
	}

	void _LateUpdateVertex()
	{
		// Memo: Call after _LateUpdateBone()
		if( !_isUseXDEF ) {
			return;
		}
		if( _vertexData == null || _cloneMeshes == null || _vertexData.meshCount != _cloneMeshes.Length ) {
			Debug.LogError("");
			return;
		}

		_xdefRootTransformInv	= this.transform.worldToLocalMatrix;
		_xdefRootRotationInv	= MMD4MecanimCommon.Inverse( this.transform.rotation );

		_xdefSDEFIndex = 0;
		int meshCount = _vertexData.meshCount;
		for( int meshIndex = 0; meshIndex < meshCount; ++meshIndex ) {
			_ProcessVertexMesh( meshIndex );
		}
	}

	// Memo: Call after _InitializeCloneMesh()
	void _PostSetupVertexMesh()
	{
		if( !_isUseXDEF ) {
			return;
		}
		if( !Application.isPlaying ) {
			return;
		}

		if( _vertexData == null || _cloneMeshes == null || _vertexData.meshCount != _cloneMeshes.Length ) {
			Debug.LogError("");
			return;
		}

		int meshCount = _vertexData.meshCount;
		for( int meshIndex = 0; meshIndex < meshCount; ++meshIndex ) {
			_PostSetupVertexMesh( meshIndex );
		}
	}

	void _PostSetupVertexMesh( int meshIndex )
	{
		MMD4MecanimAuxData.VertexData.MeshBoneInfo meshBoneInfo = new MMD4MecanimAuxData.VertexData.MeshBoneInfo();
		_vertexData.GetMeshBoneInfo( ref meshBoneInfo, meshIndex );
		if( !meshBoneInfo.isSDEF ) { // SDEF only.
			return;
		}
		if( _IsEnableMorphBlendShapes( meshIndex ) ) {
			return;
		}

		CloneMesh cloneMesh = _cloneMeshes[ meshIndex ];
		if( cloneMesh == null || cloneMesh.mesh == null ) {
			Debug.LogError("");
			return;
		}
		
		MMD4MecanimAuxData.VertexData.MeshVertexInfo meshVertexInfo = new MMD4MecanimAuxData.VertexData.MeshVertexInfo();
		_vertexData.GetMeshVertexInfo( ref meshVertexInfo, meshIndex );
		if( cloneMesh.mesh.vertexCount != meshVertexInfo.count ) {
			Debug.LogError("");
			return;
		}

		// Overwrite boneWeights to XDEFBone
		BoneWeight[] originalBoneWeights = cloneMesh.boneWeights;
		for( int vertexIndex = 0; vertexIndex < meshVertexInfo.count; ++vertexIndex ) {
			MMD4MecanimAuxData.VertexData.VertexFlags vertexFlags = _vertexData.GetVertexFlags( ref meshVertexInfo, vertexIndex );
			if( (vertexFlags & MMD4MecanimAuxData.VertexData.VertexFlags.SDEF) != MMD4MecanimAuxData.VertexData.VertexFlags.None ) {
				if( (vertexFlags & MMD4MecanimAuxData.VertexData.VertexFlags.SDEFSwapIndex) != MMD4MecanimAuxData.VertexData.VertexFlags.None ) {
					int i = originalBoneWeights[vertexIndex].boneIndex0;
					originalBoneWeights[vertexIndex].boneIndex0 = originalBoneWeights[vertexIndex].boneIndex1;
					originalBoneWeights[vertexIndex].boneIndex1 = i;
					float f = originalBoneWeights[vertexIndex].weight0;
					originalBoneWeights[vertexIndex].weight0 = originalBoneWeights[vertexIndex].weight1;
					originalBoneWeights[vertexIndex].weight1 = f;
				}
			}
		}
	}

	void _ProcessVertexMesh( int meshIndex )
	{
		MMD4MecanimAuxData.VertexData.MeshBoneInfo meshBoneInfo = new MMD4MecanimAuxData.VertexData.MeshBoneInfo();
		_vertexData.GetMeshBoneInfo( ref meshBoneInfo, meshIndex );
		if( !meshBoneInfo.isSDEF ) { // SDEF only.
			return;
		}
		
		if( _xdefBoneTransformCache == null || _xdefBoneTransformCache.Length < meshBoneInfo.count ||
		    _xdefBoneRotationCache == null || _xdefBoneRotationCache.Length < meshBoneInfo.count ) {
			Debug.LogError("");
			return;
		}
		
		CloneMesh cloneMesh = _cloneMeshes[ meshIndex ];
		if( cloneMesh == null ) {
			return;
		}

		if( cloneMesh.bones == null || cloneMesh.bones.Length != meshBoneInfo.count ) {
			Debug.LogError("");
			return;
		}
		
		MMD4MecanimAuxData.VertexData.MeshVertexInfo meshVertexInfo = new MMD4MecanimAuxData.VertexData.MeshVertexInfo();
		_vertexData.GetMeshVertexInfo( ref meshVertexInfo, meshIndex );
		if( cloneMesh.mesh == null || cloneMesh.mesh.vertexCount != meshVertexInfo.count ) {
			Debug.LogError("");
			return;
		}
		
		if( cloneMesh.backupVertices == null || cloneMesh.vertices == null ||
		    cloneMesh.backupNormals == null || cloneMesh.normals == null ||
		    cloneMesh.bindposes == null || cloneMesh.boneWeights == null ) {
			Debug.LogError("");
			return;
		}

		// Prepare to SDEF
		for( int boneIndex = 0; boneIndex < meshBoneInfo.count; ++boneIndex ) {
			MMD4MecanimAuxData.VertexData.BoneFlags boneFlags = _vertexData.GetBoneFlags( ref meshBoneInfo, boneIndex );
			if( (boneFlags & MMD4MecanimAuxData.VertexData.BoneFlags.SDEF) != MMD4MecanimAuxData.VertexData.BoneFlags.None ) {
				_xdefBoneTransformCache[boneIndex] = _xdefRootTransformInv * cloneMesh.bones[boneIndex].localToWorldMatrix * cloneMesh.bindposes[boneIndex];
				_xdefBoneTransformInvCache[boneIndex] = _xdefBoneTransformCache[boneIndex].inverse;
				_xdefBoneRotationCache[boneIndex] = _xdefRootRotationInv * cloneMesh.bones[boneIndex].rotation;
			}
		}
		
		if( !cloneMesh.updatedVertices ) {
			cloneMesh.updatedVertices = true;
			System.Array.Copy( cloneMesh.backupVertices, cloneMesh.vertices, cloneMesh.backupVertices.Length );
		}
		if( !cloneMesh.updatedNormals ) {
			cloneMesh.updatedNormals = true;
			System.Array.Copy( cloneMesh.backupNormals, cloneMesh.normals, cloneMesh.backupNormals.Length );
		}

		Vector3[] vertices = cloneMesh.vertices;
		Vector3[] normals = cloneMesh.normals;
		BoneWeight[] boneWeights = cloneMesh.boneWeights;

		// Processing to SDEF
		MMD4MecanimAuxData.VertexData.SDEFParams sdefParams = new MMD4MecanimAuxData.VertexData.SDEFParams();
		for( int vertexIndex = 0; vertexIndex < meshVertexInfo.count; ++vertexIndex ) {
			MMD4MecanimAuxData.VertexData.VertexFlags vertexFlags = _vertexData.GetVertexFlags( ref meshVertexInfo, vertexIndex );
			if( (vertexFlags & MMD4MecanimAuxData.VertexData.VertexFlags.SDEF) != MMD4MecanimAuxData.VertexData.VertexFlags.None ) {
				Vector3 vertex = vertices[vertexIndex];
				Vector3 normal = normals[vertexIndex];
				
				int boneIndex0 = boneWeights[vertexIndex].boneIndex0;
				int boneIndex1 = boneWeights[vertexIndex].boneIndex1;
				float boneWeight0 = boneWeights[vertexIndex].weight0;
				float boneWeight1 = boneWeights[vertexIndex].weight1;
				
				_vertexData.GetSDEFParams( ref sdefParams, _xdefSDEFIndex );
				++_xdefSDEFIndex;
				
				Vector3 sdefC0 = sdefParams.c;
				Vector3 sdefC1 = sdefParams.c;
				Vector3 sdefR0 = Vector3.zero;
				Vector3 sdefR1 = Vector3.zero;
				Quaternion rotation0 = Quaternion.identity;
				Quaternion rotation1 = Quaternion.identity;
				
				if( boneIndex0 >= 0 ) {
					sdefR0 = _xdefBoneTransformCache[boneIndex0].MultiplyPoint3x4(sdefParams.r0) * boneWeight0;
					sdefC0 = (_xdefBoneTransformCache[boneIndex0].MultiplyPoint3x4(sdefParams.c) - sdefParams.c) * boneWeight0;
					rotation0 = _xdefBoneRotationCache[boneIndex0];
				}
				if( boneIndex1 >= 0 ) {
					sdefR1 = _xdefBoneTransformCache[boneIndex1].MultiplyPoint3x4(sdefParams.r1) * boneWeight1;
					sdefC1 = (_xdefBoneTransformCache[boneIndex1].MultiplyPoint3x4(sdefParams.c) - sdefParams.c) * boneWeight1;
					rotation1 = _xdefBoneRotationCache[boneIndex1];
				}
				
				Vector3 pos = ((sdefR0 + sdefR1) + (sdefC0 + sdefC1 + sdefParams.c)) * 0.5f;
				Quaternion rot = Quaternion.Slerp( rotation0, rotation1, boneWeight1 );
				Matrix4x4 mat = Matrix4x4.TRS( Vector3.zero, rot, Vector3.one );
				Vector3 vertex2 = mat.MultiplyPoint3x4(vertex - sdefParams.c) + pos;
				Vector3 normal2 = mat.MultiplyVector(normal);
				
				vertex = Vector3.zero;
				normal = Vector3.zero;
				if( boneIndex0 >= 0 ) {
					vertex = _xdefBoneTransformInvCache[boneIndex0].MultiplyPoint3x4(vertex2) * boneWeight0;
					normal = _xdefBoneTransformInvCache[boneIndex0].MultiplyVector(normal2) * boneWeight0;
				}
				if( boneIndex1 >= 0 ) {
					vertex += _xdefBoneTransformInvCache[boneIndex1].MultiplyPoint3x4(vertex2) * boneWeight1;
					normal += _xdefBoneTransformInvCache[boneIndex1].MultiplyVector(normal2) * boneWeight1;
				}
				vertices[vertexIndex] = vertex;
				normals[vertexIndex] = normal.normalized;
			}
		}
	}
}
