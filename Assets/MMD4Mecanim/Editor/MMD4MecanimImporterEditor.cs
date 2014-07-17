using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[InitializeOnLoad]
public class MMD4MecanimImporterEditor : Editor
{
	public static bool checkInitialized = true;
	public static bool forceProjectWindowChanged = false;
	public static volatile bool forceCheckChanged = false;

	static void _OnProjectWindowChanged()
	{
		if( Application.isPlaying ) {
			forceProjectWindowChanged = true;
			return; // No changing isPlaying
		}

		#if MMD4MECANIM_DEBUG
		//Debug.Log ("MMD4MecanimDebug: projectWindowChanged");
		#endif
		MMD4MecanimImporter.SetDirtyCachedAllAssets(); // Notify deleted .MMD4Mecanim.asset
		MMD4MecanimImporter.ForceAllCheckAndCreateAssets(); // Create .pmd/.pmx to .MMD4Mecanim.asset
		
		MMD4MecanimImporter[] importerAssets = MMD4MecanimImporter.GetAllAssets();
		if( importerAssets != null ) {
			foreach( MMD4MecanimImporter importerAsset in importerAssets ) {
				forceCheckChanged |= !importerAsset.CheckChanged();
			}
		}
	}

	static void _OnHierarchyWindowChanged()
	{
		if( Application.isPlaying ) {
			return; // No changing isPlaying
		}

		#if MMD4MECANIM_DEBUG
		//Debug.Log ("MMD4MecanimDebug: hierarchyWindowChanged");
		#endif
		MMD4MecanimImporter.ForceAllCheckModelInScene();
	}

	static void _OnUpdate()
	{
		if( MMD4MecanimImporter._overrideEditorStyle ) {
			Object obj = UnityEditor.Selection.activeObject;
			if( obj == null ) {
				MMD4MecanimImporter._UnlockEditorStyle();
			} else {
				if( obj.GetType() != typeof(MMD4MecanimImporter) ) {
					MMD4MecanimImporter._UnlockEditorStyle();
				}
			}
		}

		if( Application.isPlaying ) {
			return; // No changing isPlaying
		}

		if( forceProjectWindowChanged ) {
			#if MMD4MECANIM_DEBUG
			Debug.LogWarning( "MMD4MecanimDebug: MMD4MecanimImporterEditor: update() forceProjectWindowChanged" );
			#endif
			_OnProjectWindowChanged();
		}
		if( checkInitialized ) {
			#if MMD4MECANIM_DEBUG
			Debug.LogWarning( "MMD4MecanimDebug: MMD4MecanimImporterEditor: update() checkInitialized" );
			#endif
			
			checkInitialized = false;
			MMD4MecanimImporter.ForceAllCheckAndCreateAssets(); // Create .pmd/.pmx to .MMD4Mecanim.asset
			
			MMD4MecanimImporter[] importerAssets = MMD4MecanimImporter.GetAllAssets();
			if( importerAssets != null ) {
				foreach( MMD4MecanimImporter importAsset in importerAssets ) {
					importAsset.PrepareDependency();
				}
			}
		}
		if( forceCheckChanged ) {
			#if MMD4MECANIM_DEBUG
			Debug.LogWarning( "MMD4MecanimDebug: MMD4MecanimImporterEditor: update() forceCheckChanged" );
			#endif
			
			forceCheckChanged = false;
			MMD4MecanimImporter[] importerAssets = MMD4MecanimImporter.GetAllAssets();
			if( importerAssets != null ) {
				foreach( MMD4MecanimImporter importAsset in importerAssets ) {
					forceCheckChanged |= !importAsset.ForceCheckChanged();
				}
			}
		}
	}
	
	static bool _isProjectWindowChanged;
	static bool _isHierarchyWindowChanged;

	static MMD4MecanimImporterEditor()
	{
		EditorApplication.projectWindowChanged += () =>
		{
			//_OnProjectWindowChanged();
			_isProjectWindowChanged = true;
		};
		
		EditorApplication.hierarchyWindowChanged += () =>
		{
			//_OnHierarchyWindowChanged();
			_isHierarchyWindowChanged = true;
		};
		
		EditorApplication.update += () =>
		{
			if( _isProjectWindowChanged ) {
				_isProjectWindowChanged = false;
				_OnProjectWindowChanged();
				return;
			}

			if( _isHierarchyWindowChanged ) {
				_isHierarchyWindowChanged = false;
				_OnHierarchyWindowChanged();
				return;
			}

			_OnUpdate();
		};
	}
}
