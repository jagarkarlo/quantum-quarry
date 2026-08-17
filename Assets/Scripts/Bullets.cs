using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullets : MonoBehaviour
{
    [SerializeField] float brzinaMetka = 20f;
    Rigidbody2D myRigidbody;
    PlayerMovement igrac;

    void Start()
    {
        myRigidbody = GetComponent<Rigidbody2D>();
        igrac = FindObjectOfType<PlayerMovement>();

        SetBulletDirection();
    }

    void Update()
    {
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "Enemy")
        {
            Destroy(other.gameObject);
        }
        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        Destroy(gameObject);
    }

    void SetBulletDirection()
    {
        brzinaMetka = igrac.transform.localScale.x * Mathf.Abs(brzinaMetka);

        FlipBulletFacing();

        myRigidbody.velocity = new Vector2(brzinaMetka, 0f);
    }

    void FlipBulletFacing()
    {
        Vector3 theScale = transform.localScale;
        theScale.x = Mathf.Sign(igrac.transform.localScale.x);
        transform.localScale = theScale;
    }
}
