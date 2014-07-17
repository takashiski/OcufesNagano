using UnityEngine;
using System.Collections;

using MorphType					= MMD4MecanimData.MorphType;

public partial class MMD4MecanimModel
{
	bool	_updatedGlobalAmbient;
	Color 	_globalAmbient;

	//--------------------------------------------------------------------------------------------------------------------

	void _UpdateAmbientPreview()
	{
		Color globalAmbient = RenderSettings.ambientLight;

		if( _cloneMaterials != null ) {
			for( int i = 0; i < _cloneMaterials.Length; ++i ) {
				Material[] materials = _cloneMaterials[i].materials;
				if( materials != null ) {
					for( int m = 0; m < materials.Length; ++m ) {
						Material material = _cloneMaterials[i].materials[m];
						if( material != null && material.shader != null && material.shader.name != null && material.shader.name.StartsWith("MMD4Mecanim") ) {
							Color diffuse = material.GetColor("_Color");
							Color ambient = material.GetColor("_Ambient");

							Color tempAmbient = MMD4MecanimCommon.MMDLit_GetTempAmbient(globalAmbient, ambient);
							Color tempAmbientL = MMD4MecanimCommon.MMDLit_GetTempAmbientL(ambient);
							Color tempDiffuse = MMD4MecanimCommon.MMDLit_GetTempDiffuse(globalAmbient, ambient, diffuse);
							tempDiffuse.a = diffuse.a;

							MMD4MecanimCommon.WeakSetMaterialColor( material, "_TempAmbient", tempAmbient );
							MMD4MecanimCommon.WeakSetMaterialColor( material, "_TempAmbientL", tempAmbientL );
							MMD4MecanimCommon.WeakSetMaterialColor( material, "_TempDiffuse", tempDiffuse );
						}
					}
				}
			}
		}
	}

	//--------------------------------------------------------------------------------------------------------------------

	void _InitializeGlobalAmbient()
	{
		_updatedGlobalAmbient = true;
		_globalAmbient = RenderSettings.ambientLight;
	}

	void _UpdateGlobalAmbient()
	{
		_updatedGlobalAmbient |= (_globalAmbient != RenderSettings.ambientLight);
		if( _updatedGlobalAmbient ) {
			_globalAmbient = RenderSettings.ambientLight;
			foreach( CloneMaterial cloneMaterial in _cloneMaterials ) {
				if( cloneMaterial.updateMaterialData != null && cloneMaterial.materialData != null && cloneMaterial.materials != null ) {
					for( int i = 0; i < cloneMaterial.updateMaterialData.Length; ++i ) {
						cloneMaterial.updateMaterialData[i] = true;
					}
				}
			}
		}
	}

	void _CleanupGlobalAmbient()
	{
		_updatedGlobalAmbient = false;
	}

	void _UpdateAutoLuminous()
	{
		if( this.morphAutoLuminous != null && this.morphAutoLuminous.updated && _cloneMaterials != null ) { // Check for updated AutoLuminous.
			foreach( CloneMaterial cloneMaterial in _cloneMaterials ) {
				if( cloneMaterial.updateMaterialData != null && cloneMaterial.materialData != null && cloneMaterial.materials != null ) {
					for( int i = 0; i < cloneMaterial.updateMaterialData.Length; ++i ) {
						if( !cloneMaterial.updateMaterialData[i] ) {
							float shininess = cloneMaterial.materialData[i].shininess;
							if( shininess > 100.0f ) {
								cloneMaterial.updateMaterialData[i] = true;
							}
						}
					}
				}
			}
		}
	}
	
	void _CleanupAutoLuminous()
	{
		if( this.morphAutoLuminous != null ) { // Cleanup for updated AutoLuminous.
			this.morphAutoLuminous.updated = false;
		}
	}

	void _UploadMeshVertex()
	{
		if( !Application.isPlaying ) {
			return; // Don't initialize cloneMesh for Editor Mode.
		}
		
		if( _morphBlendShapes != null && !_isUseXDEF ) {
			return;
		}
		
		if( _cloneMeshes != null ) {
			foreach( CloneMesh cloneMesh in _cloneMeshes ) {
				if( cloneMesh != null && cloneMesh.mesh != null ) {
					if( cloneMesh.updatedVertices ) {
						cloneMesh.updatedVertices = false;
						cloneMesh.mesh.vertices = cloneMesh.vertices;
					}
					if( cloneMesh.updatedNormals ) {
						cloneMesh.updatedNormals = false;
						cloneMesh.mesh.normals = cloneMesh.normals;
					}
				}
			}
		}
	}
	
	void _UploadMeshMaterial()
	{
		if( !Application.isPlaying ) {
			return; // Don't initialize cloneMesh for Editor Mode.
		}

		_UpdateGlobalAmbient();
		_UpdateAutoLuminous();
		
		if( _cloneMaterials != null ) {
			foreach( CloneMaterial cloneMaterial in _cloneMaterials ) {
				if( cloneMaterial.updateMaterialData != null && cloneMaterial.materialData != null && cloneMaterial.materials != null ) {
					for( int i = 0; i < cloneMaterial.updateMaterialData.Length; ++i ) {
						if( cloneMaterial.updateMaterialData[i] ) {
							cloneMaterial.updateMaterialData[i] = false;
							MMD4MecanimCommon.FeedbackMaterial( ref cloneMaterial.materialData[i], cloneMaterial.materials[i], this.morphAutoLuminous );
						}
					}
				}
			}
		}

		_CleanupGlobalAmbient();
		_CleanupAutoLuminous();
	}

	void _InitializeCloneMesh()
	{
		bool initializeCloneMesh = false;
		if( Application.isPlaying ) { // Don't initialize cloneMesh in Editor Mode.
			if( _skinnedMeshRenderers != null ) {
				if( _cloneMeshes == null || _cloneMeshes.Length != _skinnedMeshRenderers.Length ) {
					if( _morphBlendShapes == null || _isUseXDEF ) {
						initializeCloneMesh = true;
						_cloneMeshes = null;
					}
				}
			} else {
				_cloneMeshes = null;
			}
		}
		
		bool[] validateMesh = null;
		bool[] validateMeshXDEF = null;
		if( initializeCloneMesh ) {
			// Check for cloneMesh (U_CHAR_0 - U_CHAR_X)
			// Pending: Optimize for index check.
			if( _indexData != null && _indexData.indexValues != null && _morphBlendShapes == null ) {
				int meshCount = _indexData.meshCount;
				if( meshCount > 1 && _skinnedMeshRenderers != null && meshCount == _skinnedMeshRenderers.Length ) {
					if( _modelData != null && _modelData.morphDataList != null ) {
						int[] indexValues = _indexData.indexValues;
						validateMesh = new bool[meshCount];
						for( int m = 0; m < _modelData.morphDataList.Length; ++m ) {
							if( _modelData.morphDataList[m].morphType == MorphType.Vertex ) {
								int[] indices = _modelData.morphDataList[m].indices;
								if( indices != null ) {
									for( int i = 0; i < indices.Length; ++i ) {
										int ofst0 = indexValues[2 + indices[i] + 0];
										int ofst1 = indexValues[2 + indices[i] + 1];
										for( int n = ofst0; n < ofst1; ++n ) {
											uint realIndex = (uint)indexValues[n];
											uint meshIndex = (realIndex >> 24);
											validateMesh[meshIndex] = true;
										}
									}
								}
							}
						}
					}
				}
			}
			
			if( _isUseXDEF && _vertexData != null ) {
				int meshCount = _vertexData.meshCount;
				if( meshCount > 0 && _skinnedMeshRenderers != null && meshCount == _skinnedMeshRenderers.Length ) {
					validateMeshXDEF = new bool[meshCount];
					MMD4MecanimAuxData.VertexData.MeshBoneInfo meshBoneInfo = new MMD4MecanimAuxData.VertexData.MeshBoneInfo();
					for( int meshIndex = 0; meshIndex < meshCount; ++meshIndex ) {
						if( !_IsEnableMorphBlendShapes( meshIndex ) ) {
							_vertexData.GetMeshBoneInfo( ref meshBoneInfo, meshIndex );
							validateMeshXDEF[meshIndex] = meshBoneInfo.isSDEF; // Now supported SDEF only.
						}
					}
				}
			}
		}
		
		int cloneMaterialIndex = 0;
		int cloneMaterialLength = 0;
		if( _meshRenderers != null ) {
			cloneMaterialLength += _meshRenderers.Length;
		}
		if( _skinnedMeshRenderers != null ) {
			cloneMaterialLength += _skinnedMeshRenderers.Length;
		}
		if( cloneMaterialLength > 0 ) {
			_cloneMaterials = new CloneMaterial[cloneMaterialLength];
		}
		
		if( _meshRenderers != null ) { // Setup Materials for No Animation Mesh.
			for( int meshIndex = 0; meshIndex < _meshRenderers.Length; ++meshIndex ) {
				MeshRenderer meshRenderer = _meshRenderers[meshIndex];
				
				Material[] materials = null;
				if( Application.isPlaying ) {
					materials = meshRenderer.materials;
				}
				if( materials == null ) { // for Editor Mode.
					materials = meshRenderer.sharedMaterials;
				}
				
				_PostfixRenderQueue( materials, this.postfixRenderQueue );
				
				_cloneMaterials[cloneMaterialIndex] = new CloneMaterial();
				_SetupCloneMaterial( _cloneMaterials[cloneMaterialIndex], materials );
				++cloneMaterialIndex;
			}
		}

		if( validateMesh != null || validateMeshXDEF != null ) {
			bool cloneAnything = false;
			for( int meshIndex = 0; meshIndex < _skinnedMeshRenderers.Length && !cloneAnything; ++meshIndex ) {
				bool cloneIndex = (validateMesh != null && validateMesh[meshIndex]);
				bool cloneXDEF = (validateMeshXDEF != null && validateMeshXDEF[meshIndex]);
				cloneAnything = cloneIndex || cloneXDEF;
			}
			if( !cloneAnything ) {
				initializeCloneMesh = false;
			}
		} else {
			initializeCloneMesh = false;
		}

		if( _skinnedMeshRenderers != null ) {
			if( initializeCloneMesh ) {
				_cloneMeshes = new CloneMesh[_skinnedMeshRenderers.Length];
			}
			
			for( int meshIndex = 0; meshIndex < _skinnedMeshRenderers.Length; ++meshIndex ) {
				SkinnedMeshRenderer skinnedMeshRenderer = _skinnedMeshRenderers[meshIndex];
				
				if( initializeCloneMesh ) {
					bool cloneIndex = (validateMesh != null && validateMesh[meshIndex]);
					bool cloneXDEF = (validateMeshXDEF != null && validateMeshXDEF[meshIndex]);
					if( cloneIndex || cloneXDEF ) {
						_cloneMeshes[meshIndex] = new CloneMesh();
						MMD4MecanimCommon.CloneMeshWork cloneMeshWork = MMD4MecanimCommon.CloneMesh( skinnedMeshRenderer.sharedMesh );
						if( cloneMeshWork != null && cloneMeshWork.mesh != null ) {
							_cloneMeshes[meshIndex].skinnedMeshRenderer = skinnedMeshRenderer;
							_cloneMeshes[meshIndex].mesh = cloneMeshWork.mesh;
							_cloneMeshes[meshIndex].backupVertices = cloneMeshWork.vertices;
							_cloneMeshes[meshIndex].vertices = cloneMeshWork.vertices.Clone() as Vector3[];
							if( cloneXDEF ) {
								_cloneMeshes[meshIndex].backupNormals = cloneMeshWork.normals;
								_cloneMeshes[meshIndex].normals = cloneMeshWork.normals.Clone() as Vector3[];
								_cloneMeshes[meshIndex].bones = skinnedMeshRenderer.bones;
								_cloneMeshes[meshIndex].bindposes = cloneMeshWork.bindposes;
								_cloneMeshes[meshIndex].boneWeights = cloneMeshWork.boneWeights;
								#if MMD4MECANIM_DEBUG
								_cloneMeshes[meshIndex].colors32 = cloneMeshWork.colors32;
								#endif
							}
							skinnedMeshRenderer.sharedMesh = _cloneMeshes[meshIndex].mesh;
						} else {
							Debug.LogError("CloneMesh() Failed. : " + this.gameObject.name );
						}
					}
				}
				
				Material[] materials = null;
				if( Application.isPlaying ) {
					materials = skinnedMeshRenderer.materials;
				}
				if( materials == null ) { // for Editor Mode.
					materials = skinnedMeshRenderer.sharedMaterials;
				}
				
				_PostfixRenderQueue( materials, this.postfixRenderQueue );
				
				_cloneMaterials[cloneMaterialIndex] = new CloneMaterial();
				_SetupCloneMaterial( _cloneMaterials[cloneMaterialIndex], materials );
				++cloneMaterialIndex;
			}
		}
		
		// Check for Deferred Rendering
		if( _cloneMaterials != null ) {
			#if MMD4MECANIM_DEBUG
			for( int i = 0; i < _cloneMaterials.Length; ++i ) {
				Material[] materials = _cloneMaterials[i].materials;
				if( materials != null ) {
					for( int m = 0; m < materials.Length; ++m ) {
						if( MMD4MecanimCommon.IsDebugShader( materials[m] ) ) {
							_supportDebug = true;
							break;
						}
					}
					if( _supportDebug ) {
						break;
					}
				}
			}
			#endif
			for( int i = 0; i < _cloneMaterials.Length; ++i ) {
				Material[] materials = _cloneMaterials[i].materials;
				if( materials != null ) {
					for( int m = 0; m < materials.Length; ++m ) {
						if( MMD4MecanimCommon.IsDeferredShader( materials[m] ) ) {
							_supportDeferred = true;
							break;
						}
					}
					if( _supportDeferred ) {
						break;
					}
				}
			}
		}
	}
}
