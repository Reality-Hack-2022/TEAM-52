using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomRotation : MonoBehaviour
{
    public float speed = 30f;


    // Start is called before the first frame update
    void Start()
    {
        transform.rotation = Random.rotation;
        speed *= Random.Range(.5f, 1.5f);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(speed * Time.deltaTime * Vector3.up, Space.Self);
    }
}