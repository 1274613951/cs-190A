using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField] private Transform player;
    private Rigidbody rb;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float speed;

    private void Start() {
        rb = player.GetComponent<Rigidbody>();
    }

    private void FixedUpdate() {
        Vector3 playerForward = (rb.velocity + player.transform.forward).normalized;
        transform.position = Vector3.Lerp(transform.position,
            player.position + player.transform.TransformVector(offset)
            + playerForward * (-5f),
            speed * Time.deltaTime);
        transform.LookAt(player);
    }

}
