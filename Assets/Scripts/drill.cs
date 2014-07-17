using UnityEngine;
using System.Collections;

public class drill : MonoBehaviour {
	public Vector3 rotateDir = new Vector3(0,1,0);
	public float moveAngle = 3600;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void FixedUpdate () {
		transform.Rotate (rotateDir*moveAngle*Time.fixedDeltaTime);
	}
}
