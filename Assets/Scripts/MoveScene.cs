using UnityEngine;
using System.Collections;

public class MoveScene : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
	void OnGUI()
	{
		if(GUI.Button(new Rect(0,0,100,100),"Push me"))
		{
			FadeManager.Instance.LoadLevel("VRDemo_Tuscany",5);

		}
	}


}
