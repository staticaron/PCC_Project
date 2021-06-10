using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform target;
    private float speed;
    [SerializeField] private float normalSpeed;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float maxOffset;

    private void LateUpdate()
    {
        if (Mathf.Sqrt(Vector2.SqrMagnitude((Vector2)transform.position - (Vector2)target.position)) < maxOffset)
        {
            speed = normalSpeed;
        }
        else
        {
            speed = maxSpeed;
        }

        var movementVector = Vector2.Lerp(transform.position, target.position, Time.deltaTime * speed);
        transform.position = new Vector3(movementVector.x, movementVector.y, -10);
    }
}
