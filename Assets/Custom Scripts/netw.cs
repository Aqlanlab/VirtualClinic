using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;

    public override void OnNetworkSpawn()
    {
        // IsOwner is valid here (better than Start for NGO objects)
        if (IsOwner)
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = Color.red;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        Vector2 input = Vector2.zero;

        // Keyboard (WASD + arrows)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1;
        }

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 direction = new Vector3(input.x, 0f, input.y);
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }
}
