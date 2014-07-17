using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MorphCategory				= MMD4MecanimData.MorphCategory;
using MorphType					= MMD4MecanimData.MorphType;
using MorphAutoLumninousType	= MMD4MecanimData.MorphAutoLumninousType;
using MorphData					= MMD4MecanimData.MorphData;
using MorphMotionData			= MMD4MecanimData.MorphMotionData;
using BoneData					= MMD4MecanimData.BoneData;
using RigidBodyData				= MMD4MecanimData.RigidBodyData;

using Bone						= MMD4MecanimBone;

[ExecuteInEditMode ()] // for Morph
public partial class MMD4MecanimModel : MonoBehaviour
{
	public enum PhysicsEngine
	{
		None,
		BulletPhysics,
	}

	public class Morph
	{
		public float							weight;
		public float							weight2;

		public float							_animWeight;
		public float							_appendWeight;
		public float							_updateWeight;
		public float							_updatedWeight;

		public MorphData						morphData;
		public MorphAutoLumninousType			morphAutoLuminousType;

		public MorphType morphType {
			get {
				if( morphData != null ) {
					return morphData.morphType;
				}
				return MorphType.Group;
			}
		}

		public MorphCategory morphCategory {
			get {
				if( morphData != null ) {
					return morphData.morphCategory;
				}
				return MorphCategory.Base;
			}
		}

		public string name {
			get {
				if( morphData != null ) {
					return morphData.nameJp;
				}
				return null;
			}
		}
	}
	
	public class MorphAutoLuminous
	{
		public float lightUp;
		public float lightOff;
		public float lightBlink;
		public float lightBS;

		public bool updated;
	}

	[System.Serializable]
	public class RigidBody
	{
		[System.NonSerialized]
		public RigidBodyData					rigidBodyData = null;
		public bool								enabled = true;
		public int								_enabledCached = -1;
	}

	[System.Serializable]
	public class Anim
	{
		public TextAsset						animFile;
		public string							animatorStateName;
		public AudioClip						audioClip;
		
		[NonSerialized]
		public MMD4MecanimData.AnimData			animData;
		[NonSerialized]
		public int								animatorStateNameHash;
		
		public struct MorphMotion
		{
			public Morph						morph;
			public int							lastKeyFrameIndex;
		}
		
		[NonSerialized]
		public MorphMotion[]					morphMotionList;
	}

	[System.Serializable]
	public class BulletPhysics
	{
		public bool joinLocalWorld = true;
		public bool useOriginalScale = true;
		public bool useCustomResetTime = false;
		public float resetMorphTime = 1.8f;
		public float resetWaitTime = 1.2f;
		public MMD4MecanimBulletPhysics.WorldProperty worldProperty;
		public MMD4MecanimBulletPhysics.MMDModelProperty mmdModelProperty;
	}
	
	private class CloneMesh
	{
		public SkinnedMeshRenderer					skinnedMeshRenderer;
		public Mesh									mesh;
		public Vector3[]							vertices;
		public Vector3[]							backupVertices;
		public Vector3[]							normals;				// for XDEF
		public Vector3[]							backupNormals;			// for XDEF
		public Transform[]							bones;					// for XDEF
		public Matrix4x4[]							bindposes;				// for XDEF
		public BoneWeight[]							boneWeights;			// for XDEF
		#if MMD4MECANIM_DEBUG
		public Color32[]							colors32;				// for XDEF(Debug)
		#endif
		public bool									updatedVertices;
		public bool									updatedNormals;
	}

	public class CloneMaterial
	{
		public Material[]							materials;
		public MMD4MecanimData.MorphMaterialData[]	materialData;
		public MMD4MecanimData.MorphMaterialData[]	backupMaterialData;
		public bool[]								updateMaterialData;
	}

	private struct MorphBlendShape
	{
		// Key ... morphID Value ... blendShapeIndex
		public int[]								blendShapeIndices;
	}

	public bool										initializeOnAwake = false;
	public bool										postfixRenderQueue = true;
	public bool										updateWhenOffscreen = true;
	public bool										animEnabled = true;
	public bool										animSyncToAudio = true;
	public TextAsset								modelFile;
	public TextAsset								indexFile;
	public TextAsset								vertexFile;
	public AudioSource								audioSource;

	public bool boneInherenceEnabled				= false;

	public bool pphEnabled							= true;
	public bool pphEnabledNoAnimation				= true;

	public bool										pphShoulderEnabled = true;
	public float									pphShoulderFixRate = 0.7f;

	public bool										ikEnabled = false;
	public bool										xdefEnabled = false;
	public bool										xdefMobileEnabled = false;
	bool											_boneInherenceEnabledCached;
	bool											_ikEnabledCached;
	GameObject										_xdefBone;

	[System.NonSerialized]
	public bool										_isSkinningManually;

	public enum PPHType
	{
		Shoulder,
	}

	public class PPHBone
	{
		public PPHType			pphType;
		public GameObject		target;
		public List<GameObject>	childSkeletons;
		public Quaternion[]		childRotations;

		public PPHBone( PPHType pphType, GameObject target )
		{
			this.pphType = pphType;
			this.target = target;
		}

		public void AddChildSkeleton( GameObject childSkeleton )
		{
			if( this.childSkeletons == null ) {
				this.childSkeletons = new List<GameObject>();
			}
			this.childSkeletons.Add( childSkeleton );
		}

		public void SnapshotChildRotations()
		{
			if( this.childSkeletons != null ) {
				if( this.childRotations == null || this.childRotations.Length != this.childSkeletons.Count ) {
					this.childRotations = new Quaternion[this.childSkeletons.Count];
				}
				for(int i = 0; i < this.childSkeletons.Count; ++i) {
					this.childRotations[i] = this.childSkeletons[i].transform.rotation;
				}
			}
		}

		public void RestoreChildRotations()
		{
			if( this.childSkeletons != null && this.childRotations != null && this.childSkeletons.Count == this.childRotations.Length ) {
				for(int i = 0; i < this.childSkeletons.Count; ++i) {
					this.childSkeletons[i].transform.rotation = this.childRotations[i];
				}
			}
		}
	}

	private List<PPHBone>							_pphBones = new List<PPHBone>();

	public MMD4MecanimData.ModelData modelData {
		get { return _modelData; }
	}
	
	public byte[] modelFileBytes {
		get { return (modelFile != null) ? modelFile.bytes : null; }
	}
	
	[NonSerialized]
	public Bone[]									boneList;
	[NonSerialized]
	public IK[]										ikList;
	[NonSerialized]
	public Morph[]									morphList;
	[NonSerialized]
	public MorphAutoLuminous						morphAutoLuminous;

	public PhysicsEngine							physicsEngine;
	PhysicsEngine									_physicsEngineCached;
	public BulletPhysics							bulletPhysics;
	public RigidBody[]								rigidBodyList;

	public Anim[]									animList;

	private bool									_initialized;
	private Bone									_rootBone;
	private Bone[]									_sortedBoneList;
	private List<Bone>								_processingBoneList;
	private bool									_isDirtyProcessingBoneList = false;
	private MeshRenderer[]							_meshRenderers;
	private SkinnedMeshRenderer[]					_skinnedMeshRenderers;
	private CloneMesh[]								_cloneMeshes;
	private MorphBlendShape[]						_morphBlendShapes;
	private CloneMaterial[]							_cloneMaterials;
	#if MMD4MECANIM_DEBUG
	private bool									_supportDebug;
	#endif
	private bool									_supportDeferred;
	private Light									_deferredLight;

	public MMD4MecanimData.ModelData				_modelData;
	public MMD4MecanimAuxData.IndexData				_indexData;
	public MMD4MecanimAuxData.VertexData			_vertexData;

	public bool isSkinning {
		get {
			return _skinnedMeshRenderers != null && _skinnedMeshRenderers.Length > 0;
		}
	}

	// for Inspector.
	public enum EditorViewPage {
		Model,
		Bone,
		IK,
		Morph,
		Anim,
		Physics,
	}
	
	[HideInInspector]
	public EditorViewPage							editorViewPage;
	[HideInInspector]
	public byte										editorViewMorphBits = 0x0f;
	[HideInInspector]
	public bool										editorViewRigidBodies = false;
	[NonSerialized]
	public Mesh										defaultMesh;
	
	private Animator								_animator;
	private MMD4MecanimBulletPhysics.MMDModel		_bulletPhysicsMMDModel;
	private MMD4MecanimModel.Anim					_currentAnim;
	private MMD4MecanimModel.Anim					_playingAudioAnim;
	private float									_prevDeltaTime;
	private float[]									_animMorphCategoryWeights;

	public System.Action							onUpdating;
	public System.Action							onUpdated;
	public System.Action							onLateUpdating;
	public System.Action							onLateUpdated;

	public Bone GetRootBone()
	{
		return _rootBone;
	}

	public Bone GetBone( int boneID )
	{
		if( boneID >= 0 && boneID < this.boneList.Length ) {
			return this.boneList[boneID];
		}
		return null;
	}

	public Morph GetMorph( string morphName )
	{
		return GetMorph( morphName, false );
	}

	public Morph GetMorph( string morphName, bool isStartsWith )
	{
		if( this.modelData != null ) {
			int morphIndex = this.modelData.GetMorphDataIndex( morphName, isStartsWith );
			if( morphIndex != -1 ) {
				return this.morphList[morphIndex];
			}
		}
		
		return null;
	}

	void Awake()
	{
		if( initializeOnAwake ) {
			Initialize();
		}
	}
	
	void Start()
	{
		Initialize();
	}

	void Update()
	{
		if( !Application.isPlaying ) {
			return;
		}

		if( this.onUpdating != null ) {
			this.onUpdating();
		}

		if( _prevDeltaTime == 0.0f ) { // for _UpdateAnim()
			_prevDeltaTime = Time.deltaTime;
		}
		
		_UpdateAnim();
		_UpdateAnim2();
		_UpdateMorph();

		_prevDeltaTime = Time.deltaTime;

		_UpdateBone();

		if( this.onUpdated != null ) {
			this.onUpdated();
		}
	}

	void LateUpdate()
	{
		_UpdatedDeffered();
		if( !Application.isPlaying ) {
			_UpdateAmbientPreview();
		}

		if( !Application.isPlaying ) {
			return;
		}

		if( this.onLateUpdating != null ) {
			this.onLateUpdating();
		}

		_UpdateRigidBody();
		_LateUpdateBone();
		if( !_isSkinningManually ) {
			_LateUpdateVertex();
			_UploadMeshVertex();
		}
		_UploadMeshMaterial();

		if( this.onLateUpdated != null ) {
			this.onLateUpdated();
		}
	}

	public void ProcessSkinning()
	{
		if( !Application.isPlaying ) {
			return;
		}
		if( !_isSkinningManually ) {
			return;
		}

		_LateUpdateVertex();
		_UploadMeshVertex();
	}

	void OnRenderObject()
	{
		//Debug.Log( Camera.current.projectionMatrix );
		//Matrix4x4 mat = Camera.current.projectionMatrix;
		//Debug.Log ( mat );
		//float rn = (-mat.m32 - mat.m22) / mat.m23;
		//float scale = rn / mat.m11;
		//Debug.Log( "znear:" + (1.0f / rn) + " rn:" + rn + " edge_scale:" + scale );
#if false
		_UpdatedDeffered();
		if( !Application.isPlaying ) {
			_UpdateAmbientPreview();
		}
#endif
	}

	void OnDestroy()
	{
		if( this.ikList != null ) {
			for( int i = 0; i < this.ikList.Length; ++i ) {
				if( this.ikList[i] != null ) {
					this.ikList[i].Destroy();
				}
			}
			this.ikList = null;
		}

		_sortedBoneList = null;

		if( this.boneList != null ) {
			for( int i = 0; i < this.boneList.Length; ++i ) {
				if( this.boneList[i] != null ) {
					this.boneList[i].Destroy();
				}
			}
			this.boneList = null;
		}

		if( _bulletPhysicsMMDModel != null && !_bulletPhysicsMMDModel.isExpired ) {
			MMD4MecanimBulletPhysics instance = MMD4MecanimBulletPhysics.instance;
			if( instance != null ) {
				instance.DestroyMMDModel( _bulletPhysicsMMDModel );
			}
		}
		_bulletPhysicsMMDModel = null;
	}

	public void Initialize()
	{
		if( !Application.isPlaying ) {
			InitializeOnEditor();
			return;
		}

		if( _initialized ) {
			return;
		}
		
		_initialized = true;
		
		_InitializeMesh();
		_InitializeModel();
		_InitializeRigidBody();
		_PrepareBlendShapes();
		_InitializeBlendShapes();
		_InitializeIndex();
		_InitializeVertex();
		_InitializeCloneMesh();
		_PostSetupVertexMesh();
		_InitializeAnimatoion();
		_InitializePhysicsEngine();
		_InitializePPHBones();
		_InitializeDeferredMesh();
		_InitializeGlobalAmbient();
	}

	public AudioSource GetAudioSource()
	{
		if( this.audioSource == null ) {
			this.audioSource = this.gameObject.GetComponent< AudioSource >();
			if( this.audioSource == null ) {
				this.audioSource = this.gameObject.AddComponent< AudioSource >();
			}
		}

		return this.audioSource;
	}

	public void InitializeOnEditor()
	{
		if( _modelData == null || _cloneMaterials == null || _cloneMaterials.Length == 0 ) {
			_initialized = false;
		}
		if( _modelData == null && this.modelFile == null ) {
			return;
		}

		if( _modelData == null ) {
			_modelData = MMD4MecanimData.BuildModelData( this.modelFile );
			if( _modelData == null ) {
				Debug.LogError( this.gameObject.name + ":modelFile is unsupported format." );
				return;
			}
		}
		
		if( _modelData != null ) {
			if( _modelData.boneDataList != null ) {
				if( this.boneList == null || this.boneList.Length != _modelData.boneDataList.Length ) {
					_initialized = false;
				}
			}
		}
		
		if( _initialized ) {
			return;
		}
		
		_initialized = true;

		_InitializeMesh();
		_InitializeModel();
		_InitializeRigidBody();
		_PrepareBlendShapes();
		_InitializeBlendShapes();
		//_InitializeIndex();
		//_InitializeVertex();
		_InitializeCloneMesh();
		//_PostSetupVertexMesh();
		_InitializeAnimatoion();
		//_InitializePhysicsEngine();
		//_InitializePPHBones();
		_InitializeDeferredMaterial(); // for Editor only.
		//_InitializeGlobalAmbient();
	}

	private void _InitializeMesh()
	{
		if( _meshRenderers == null || _meshRenderers.Length == 0 ) {
			_meshRenderers = MMD4MecanimCommon.GetMeshRenderers( this.gameObject );
		}
		if( _skinnedMeshRenderers == null || _skinnedMeshRenderers.Length == 0 ) {
			_skinnedMeshRenderers = MMD4MecanimCommon.GetSkinnedMeshRenderers( this.gameObject );
			if( _skinnedMeshRenderers != null ) {
				foreach( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers ) {
					if( skinnedMeshRenderer.updateWhenOffscreen != this.updateWhenOffscreen ) {
						skinnedMeshRenderer.updateWhenOffscreen = this.updateWhenOffscreen;
					}
				}
			}
		}

		if( _skinnedMeshRenderers != null && _skinnedMeshRenderers.Length > 0 ) {
			if( this.defaultMesh == null ) {
				this.defaultMesh = _skinnedMeshRenderers[0].sharedMesh;
			}
		}

		if( _meshRenderers != null && _meshRenderers.Length > 0 ) {
			MeshFilter meshFilter = gameObject.GetComponent< MeshFilter >();
			if( meshFilter != null ) {
				if( this.defaultMesh == null ) {
					this.defaultMesh = meshFilter.sharedMesh;
				}
			}
		}
	}

	private bool _PrepareBlendShapes()
	{
#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2
		// Not supported BlendShapes.
		return false;
#else
		bool blendShapesAnything = false;
		// Reset blendShapes.
		if( _skinnedMeshRenderers != null ) {
			foreach( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers ) {
				if( skinnedMeshRenderer.sharedMesh != null ) {
					for( int b = 0; b < skinnedMeshRenderer.sharedMesh.blendShapeCount; ++b ) {
						if( Application.isPlaying ) {
							skinnedMeshRenderer.SetBlendShapeWeight( b, 0.0f );
						}
						blendShapesAnything = true;
					}
				}
			}
		}
		return blendShapesAnything;
#endif
	}

	private void _InitializeBlendShapes()
	{
#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2
		// Not supported BlendShapes.
#else
		if( _skinnedMeshRenderers != null && _modelData != null &&
		   _modelData.morphDataList != null && _modelData.morphDataList.Length > 0 ) {
			if( _morphBlendShapes == null || _morphBlendShapes.Length != _skinnedMeshRenderers.Length ) {
				_morphBlendShapes = null;
				bool blendShapeAnything = false;
				foreach( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers ) {
					if( skinnedMeshRenderer.sharedMesh.blendShapeCount > 0 ) {
						blendShapeAnything = true;
						break;
					}
				}
				if( blendShapeAnything ) {
					_morphBlendShapes = new MorphBlendShape[_skinnedMeshRenderers.Length];
					for( int i = 0; i < _skinnedMeshRenderers.Length; ++i ) {
						_morphBlendShapes[i] = new MorphBlendShape();
						_morphBlendShapes[i].blendShapeIndices = new int[_modelData.morphDataList.Length];
						for( int m = 0; m < _modelData.morphDataList.Length; ++m ) {
							_morphBlendShapes[i].blendShapeIndices[m] = -1;
						}
						SkinnedMeshRenderer skinnedMeshRenderer = _skinnedMeshRenderers[i];
						if( skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.blendShapeCount > 0 ) {
							for( int b = 0; b < skinnedMeshRenderer.sharedMesh.blendShapeCount; ++b ) {
								string blendShapeName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName( b );
								int morphID = MMD4MecanimCommon.ToInt( blendShapeName );
								//Debug.Log ( "Mesh:" + i + " morphID:" + morphID + " blendShapeIndex:" + b + " Name:" + blendShapeName );
								if( (uint)morphID < (uint)_modelData.morphDataList.Length ) {
									_morphBlendShapes[i].blendShapeIndices[morphID] = b;
								}
							}
						}
					}
				}
			}
		}
#endif
	}

	private static void _PostfixRenderQueue( Material[] materials, bool postfixRenderQueue )
	{
		if( Application.isPlaying ) { // Don't change renderQueue in Editor Mode.
			if( materials != null ) {
				for( int i = 0; i < materials.Length; ++i ) {
					if( postfixRenderQueue ) {
						materials[i].renderQueue = 2001 + MMD4MecanimCommon.ToInt( materials[i].name );
					} else {
						if( materials[i].renderQueue == 2999 ) {
							materials[i].renderQueue = 2001;
						}
					}
				}
			}
		}
	}

	private static void _SetupCloneMaterial( CloneMaterial cloneMaterial, Material[] materials )
	{
		cloneMaterial.materials = materials;
		if( materials != null ) {
			int materialLength = materials.Length;
			cloneMaterial.materialData = new MMD4MecanimData.MorphMaterialData[materialLength];
			cloneMaterial.backupMaterialData = new MMD4MecanimData.MorphMaterialData[materialLength];
			cloneMaterial.updateMaterialData = new bool[materialLength];
			for( int i = 0; i < materialLength; ++i ) {
				if( materials[i] != null ) {
					MMD4MecanimCommon.BackupMaterial( ref cloneMaterial.backupMaterialData[i], materials[i] );
					cloneMaterial.materialData[i] = cloneMaterial.backupMaterialData[i];
				}
			}
		}
	}

	private bool _IsEnableMorphBlendShapes( int meshIndex )
	{
		if( _morphBlendShapes == null ) {
			return false;
		}
		if( meshIndex < 0 || meshIndex >= _morphBlendShapes.Length ) {
			return false;
		}
		if( _morphBlendShapes[meshIndex].blendShapeIndices == null ) {
			return false;
		}

		foreach( int index in _morphBlendShapes[meshIndex].blendShapeIndices ) {
			if( index != -1 ) {
				return true;
			}
		}

		return false;
	}

	private void _InitializeIndex()
	{
		if( _morphBlendShapes != null ) {
			return; // Skip indexData
		}

		if( _skinnedMeshRenderers == null || _skinnedMeshRenderers.Length == 0 ) {
			return;
		}
		
		if( this.indexFile == null ) {
			Debug.LogWarning( this.gameObject.name + ":indexFile is nothing." );
			return;
		}

		_indexData = MMD4MecanimAuxData.BuildIndexData( this.indexFile );
		if( _indexData == null ) {
			Debug.LogError( this.gameObject.name + ":indexFile is unsupported format." );
			return;
		}
		
		if( !MMD4MecanimAuxData.ValidateIndexData( _indexData, _skinnedMeshRenderers ) ) {
			Debug.LogError( this.gameObject.name + ":indexFile is required recreate." );
			_indexData = null;
			return;
		}
	}
	
	private void _InitializeModel()
	{
		if( this.modelFile == null ) {
			Debug.LogWarning( this.gameObject.name + ":modelFile is nothing." );
			return;
		}
		
		_modelData = MMD4MecanimData.BuildModelData( this.modelFile );
		if( _modelData == null ) {
			Debug.LogError( this.gameObject.name + ":modelFile is unsupported format." );
			return;
		}
		
		if( _modelData.boneDataList != null && _modelData.boneDataDictionary != null ) {
			if( this.boneList == null || this.boneList.Length != _modelData.boneDataList.Length ) {
				this.boneList = new Bone[_modelData.boneDataList.Length];
				_BindBone();

				// Bind(originalParent/target/child/inherenceParent)
				for( int i = 0; i < this.boneList.Length; ++i ) {
					if( this.boneList[i] != null ) {
						this.boneList[i].Bind();
					}
				}
				
				// sortedBoneList
				_sortedBoneList = new Bone[this.boneList.Length];
				for( int i = 0; i < this.boneList.Length; ++i ) {
					if( this.boneList[i] != null ) {
						BoneData boneData = this.boneList[i].boneData;
						if( boneData != null ) {
							int sortedBoneID = boneData.sortedBoneID;
							if( sortedBoneID >= 0 && sortedBoneID < this.boneList.Length ) {
								#if MMD4MECANIM_DEBUG
								if( _sortedBoneList[sortedBoneID] != null ) { // Check overwrite.
									Debug.LogError("");
								}
								#endif
								_sortedBoneList[sortedBoneID] = this.boneList[i];
							} else {
								#if MMD4MECANIM_DEBUG
								Debug.LogError("");
								#endif
							}
						}
					}
				}
			}
		}

		// ikList
		if( _modelData.ikDataList != null ) {
			int ikListLength = _modelData.ikDataList.Length;
			this.ikList = new IK[ikListLength];
			for( int i = 0; i < ikListLength; ++i ) {
				this.ikList[i] = new IK( this, i );
			}
		}

		// morphList
		if( _modelData.morphDataList != null ) {
			this.morphList = new MMD4MecanimModel.Morph[_modelData.morphDataList.Length];
			for( int i = 0; i < _modelData.morphDataList.Length; ++i ) {
				this.morphList[i] = new Morph();
				this.morphList[i].morphData = _modelData.morphDataList[i];

				// for AutoLuminous
				string morphName = this.morphList[i].name;
				if( !string.IsNullOrEmpty(morphName) ) {
					switch( morphName ) {
					case "LightUp":
						this.morphList[i].morphAutoLuminousType = MorphAutoLumninousType.LightUp;
						break;
					case "LightOff":
						this.morphList[i].morphAutoLuminousType = MorphAutoLumninousType.LightOff;
						break;
					case "LightBlink":
						this.morphList[i].morphAutoLuminousType = MorphAutoLumninousType.LightBlink;
						break;
					case "LightBS":
						this.morphList[i].morphAutoLuminousType = MorphAutoLumninousType.LightBS;
						break;
					}
				}
			}
		}

		// morphAutoLuminous
		this.morphAutoLuminous = new MorphAutoLuminous();
	}
}
