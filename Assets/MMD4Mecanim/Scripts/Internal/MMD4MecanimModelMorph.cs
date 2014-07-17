using UnityEngine;
using System.Collections;

using MorphCategory				= MMD4MecanimData.MorphCategory;
using MorphType					= MMD4MecanimData.MorphType;
using MorphAutoLumninousType	= MMD4MecanimData.MorphAutoLumninousType;
using MorphData					= MMD4MecanimData.MorphData;
using MorphMotionData			= MMD4MecanimData.MorphMotionData;

public partial class MMD4MecanimModel
{
	public void ForceUpdateMorph()
	{
		_UpdateMorph();
	}
	
	void _UpdateMorph()
	{
		if( this.morphList != null && _animMorphCategoryWeights != null) {
			// Check overrideWeights.
			for( int i = 0; i < _animMorphCategoryWeights.Length; ++i ) {
				_animMorphCategoryWeights[i] = 1.0f;
			}
			for( int i = 0; i < this.morphList.Length; ++i ) {
				Morph morph = this.morphList[i];
				switch( morph.morphCategory ) {
				case MorphCategory.EyeBrow:
				case MorphCategory.Eye:
				case MorphCategory.Lip:
					if( morph.weight2 != 0.0f ) {
						if( morph.weight2 == 1.0f ) {
							_animMorphCategoryWeights[(int)morph.morphCategory] = 0.0f;
						} else {
							_animMorphCategoryWeights[(int)morph.morphCategory] = Mathf.Min( _animMorphCategoryWeights[(int)morph.morphCategory], 1.0f - morph.weight2 );
						}
					}
					break;
				default:
					break;
				}
			}
			
			// Check update.
			bool updatedAnything = false;
			for( int i = 0; i < this.morphList.Length; ++i ) {
				this.morphList[i]._updateWeight = _GetMorphUpdateWeight( this.morphList[i], _animMorphCategoryWeights );
				updatedAnything |= ( this.morphList[i]._updateWeight != this.morphList[i]._updatedWeight );
			}
			
			if( updatedAnything ) {
				for( int i = 0; i < this.morphList.Length; ++i ) {
					this.morphList[i]._appendWeight = 0;
				}
				
				if( _modelData != null && _modelData.morphDataList != null ) {
					bool groupMorphAnything = false;
					for( int i = 0; i < _modelData.morphDataList.Length; ++i ) {
						if( _modelData.morphDataList[i].morphType == MorphType.Group ) {
							groupMorphAnything = true;
							_ApplyMorph( i );
						}
					}
					for( int i = 0; i < this.morphList.Length; ++i ) {
						if( _modelData.morphDataList[i].morphType != MorphType.Group ) {
							if( groupMorphAnything ) {
								this.morphList[i]._updateWeight = _GetMorphUpdateWeight( this.morphList[i], _animMorphCategoryWeights );
							}
							_ApplyMorph( i );
						}
					}
				}
			}
		}
	}

	void _ApplyMorph( int morphIndex )
	{
		if( (_morphBlendShapes == null && _cloneMeshes == null) || this.morphList == null || (uint)morphIndex >= (uint)this.morphList.Length ) {
			return;
		}
		
		Morph morph = this.morphList[morphIndex];
		if( morph == null ) {
			return;
		}
		
		float weight = morph._updateWeight;
		morph._updatedWeight = weight;
		
		if( _modelData == null || _modelData.morphDataList == null || (uint)morphIndex >= (uint)_modelData.morphDataList.Length ) {
			return;
		}
		
		MorphData morphData = _modelData.morphDataList[morphIndex];

		// for AutoLuminous
		if( this.morphAutoLuminous != null ) {
			if( morph.morphAutoLuminousType != MorphAutoLumninousType.None ) {
				switch( morph.morphAutoLuminousType ) {
				case MorphAutoLumninousType.LightUp:
					if( this.morphAutoLuminous.lightUp != weight ) {
						this.morphAutoLuminous.lightUp = weight;
						this.morphAutoLuminous.updated = true;
					}
					break;
				case MorphAutoLumninousType.LightOff:
					if( this.morphAutoLuminous.lightOff != weight ) {
						this.morphAutoLuminous.lightOff = weight;
						this.morphAutoLuminous.updated = true;
					}
					break;
				case MorphAutoLumninousType.LightBlink:
					if( this.morphAutoLuminous.lightBlink != weight ) {
						this.morphAutoLuminous.lightBlink = weight;
						this.morphAutoLuminous.updated = true;
					}
					break;
				case MorphAutoLumninousType.LightBS:
					if( this.morphAutoLuminous.lightBS != weight ) {
						this.morphAutoLuminous.lightBS = weight;
						this.morphAutoLuminous.updated = true;
					}
					break;
				}
			}
		}

		if( morphData.morphType == MorphType.Group ) {
			if( morphData.indices == null ) {
				return;
			}
			for( int i = 0; i < morphData.indices.Length; ++i ) {
				this.morphList[morphData.indices[i]]._appendWeight += weight;
			}
		} else if( morphData.morphType == MorphType.Vertex ) {
			#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2
			// Not supported BlendShapes.
			#else
			if( _morphBlendShapes != null ) {
				weight *= 100.0f;
				if( _skinnedMeshRenderers != null && _skinnedMeshRenderers.Length == _morphBlendShapes.Length ) {
					for( int i = 0; i < _morphBlendShapes.Length; ++i ) {
						if( _morphBlendShapes[i].blendShapeIndices != null && morphIndex < _morphBlendShapes[i].blendShapeIndices.Length ) {
							int blendShapeIndex = _morphBlendShapes[i].blendShapeIndices[morphIndex];
							if( blendShapeIndex != -1 && _skinnedMeshRenderers[i] != null ) {
								_skinnedMeshRenderers[i].SetBlendShapeWeight( blendShapeIndex, weight );
							}
						}
					}
				}
				
				return;
			}
			#endif
			if( morphData.indices == null ) {
				return;
			}
			if( _indexData == null || _indexData.indexValues == null ) {
				return;
			}
			if( _modelData.vertexCount != _indexData.vertexCount ) {
				return;
			}
			if( morphData.positions == null ) {
				return;
			}
			
			int[] indexValues = _indexData.indexValues;
			
			if( Mathf.Abs(weight - 1.0f) <= Mathf.Epsilon ) {
				for( int i = 0; i < morphData.indices.Length; ++i ) {
					int ofst0 = indexValues[2 + morphData.indices[i] + 0];
					int ofst1 = indexValues[2 + morphData.indices[i] + 1];
					for( int n = ofst0; n < ofst1; ++n ) {
						uint realIndex = (uint)indexValues[n];
						uint meshIndex = (realIndex >> 24);
						realIndex &= 0x00ffffff;
						
						CloneMesh cloneMesh = _cloneMeshes[meshIndex];
						if( cloneMesh != null ) {
							if( !cloneMesh.updatedVertices ) {
								cloneMesh.updatedVertices = true;
								System.Array.Copy( cloneMesh.backupVertices, cloneMesh.vertices, cloneMesh.backupVertices.Length );
							}
							Vector3 v = cloneMesh.vertices[realIndex];
							v += morphData.positions[i];
							cloneMesh.vertices[realIndex] = v;
						}
					}
				}
			} else {
				for( int i = 0; i < morphData.indices.Length; ++i ) {
					int ofst0 = indexValues[2 + morphData.indices[i] + 0];
					int ofst1 = indexValues[2 + morphData.indices[i] + 1];
					for( int n = ofst0; n < ofst1; ++n ) {
						uint meshVertexIndex = (uint)indexValues[n];
						uint meshIndex = (meshVertexIndex >> 24);
						meshVertexIndex &= 0x00ffffff;
						
						CloneMesh cloneMesh = _cloneMeshes[meshIndex];
						if( cloneMesh != null ) {
							if( !cloneMesh.updatedVertices ) {
								cloneMesh.updatedVertices = true;
								System.Array.Copy( cloneMesh.backupVertices, cloneMesh.vertices, cloneMesh.backupVertices.Length );
							}
							Vector3 v = cloneMesh.vertices[meshVertexIndex];
							v += morphData.positions[i] * weight;
							cloneMesh.vertices[meshVertexIndex] = v;
						}
					}
				}
			}
		} else if( morphData.morphType == MorphType.Material ) {
			if( morphData.materialData == null ) {
				return;
			}
			for( int i = 0; i < morphData.materialData.Length; ++i ) {
				_ApplyMaterialData( ref morphData.materialData[i], weight );
			}
		}
	}
	
	void _ApplyMaterialData( ref MMD4MecanimData.MorphMaterialData morphMaterialData, float weight )
	{
		if( _cloneMaterials != null ) {
			foreach( CloneMaterial cloneMaterial in _cloneMaterials ) {
				if( cloneMaterial.backupMaterialData != null && cloneMaterial.updateMaterialData != null && cloneMaterial.materialData != null && cloneMaterial.materials != null ) {
					for( int i = 0; i < cloneMaterial.updateMaterialData.Length; ++i ) {
						if( cloneMaterial.backupMaterialData[i].materialID == morphMaterialData.materialID ) {
							if( !cloneMaterial.updateMaterialData[i] ) {
								cloneMaterial.updateMaterialData[i] = true;
								cloneMaterial.materialData[i] = cloneMaterial.backupMaterialData[i];
							}
							
							MMD4MecanimCommon.OperationMaterial( ref cloneMaterial.materialData[i], ref morphMaterialData, weight );
						}
					}
				}
			}
		}
	}

	static float _FastScl( float weight, float weight2 )
	{
		if( weight2 == 0.0f ) return 0.0f;
		if( weight2 == 1.0f ) return weight;
		return weight * weight2;
	}
	
	static float _GetMorphUpdateWeight( Morph morph, float[] animMorphCategoryWeights )
	{
		float categoryWeight = animMorphCategoryWeights[(int)morph.morphCategory];
		float animWeight2 = 1.0f - morph.weight2;
		return Mathf.Min( 1.0f, Mathf.Max(
			morph.weight,
			_FastScl( categoryWeight, _FastScl( morph._animWeight + morph._appendWeight, animWeight2 ) ) ) );
	}
}
