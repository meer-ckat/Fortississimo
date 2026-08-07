using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
   
    public float xThreshold = 0.9f;
    public float yThreshold= 0.9f;
    public float speed;
    public float damp;
    float width => Screen.width;
    float height => Screen.height;
    Vector2 center => new(width * 0.5f, height * 0.5f);
    Vector2 velocity;

    void Update() //지옥에서 온 코드
    {
        Vector2 pos = Mouse.current.position.ReadValue() - center;
        Vector2 input = Vector2.zero;
        if(Mathf.Abs(pos.y) > center.y * yThreshold)
        input.y = Mathf.Sign(pos.y);
        if(Mathf.Abs(pos.x) > center.x * xThreshold)
        input.x = Mathf.Sign(pos.x);
        float t = 1f - Mathf.Exp(-damp * Time.deltaTime);
        velocity = Vector2.Lerp(velocity, input * speed, t);
        transform.Translate(velocity * Time.deltaTime);
    }
}
