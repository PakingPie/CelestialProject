using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerShipMovement : MonoBehaviour
{
    new Rigidbody rigidbody;

    const float FORCEMULT = 100.0f;

    public float thrust;
    public float yaw;
    public float pitch;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }
    
    // FixedUpdate is called once per physics frame
    void FixedUpdate()
    {
        float inPitch = Keyboard.current.sKey.isPressed ? -1.0f :
                        Keyboard.current.wKey.isPressed ? 1.0f : 0.0f;
        float inYaw = Keyboard.current.aKey.isPressed ? -1.0f :
                      Keyboard.current.dKey.isPressed ? 1.0f : 0.0f;

        rigidbody.AddRelativeForce(0.0f, 0.0f, thrust * FORCEMULT * Time.deltaTime);

        rigidbody.AddRelativeTorque(inPitch * pitch * FORCEMULT * Time.deltaTime,
                                    inYaw * yaw * FORCEMULT * Time.deltaTime,
                                    -inYaw * yaw * FORCEMULT * 0.5f * Time.deltaTime);
    }
}