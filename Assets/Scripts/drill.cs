using UnityEngine;
using System.Collections;

public class drill : MonoBehaviour {
	public GameObject driver;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void FixedUpdate () {
		driver.transform.Rotate (new Vector3(0,120,0));
	}
}
