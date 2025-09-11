using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Searching : MonoBehaviour
{
    private float positionValue = 0;
    private float radius = 90f;
    private Vector3 origin;
    // Start is called before the first frame update
    void Start()
    {
        origin = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        float xPos = Mathf.Cos(positionValue) * radius;
        float yPos = Mathf.Sin(positionValue) * radius;

        transform.position = new Vector3(origin.x + xPos, origin.y + yPos, 0);

        positionValue += 0.01f;
    }
}
