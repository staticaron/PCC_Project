using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Vector2 minMaxX;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float movingWithSpeed;
    private Rigidbody2D thisBody;
    private int multiplier = 1;

    private void Awake()
    {
        thisBody = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (transform.position.x < minMaxX.x)
        {
            multiplier = 1;
        }
        else if (transform.position.x > minMaxX.y)
        {
            multiplier = -1;
        }

        thisBody.velocity = new Vector2(moveSpeed * multiplier, 0);
        movingWithSpeed = thisBody.velocity.x;
    }
}
