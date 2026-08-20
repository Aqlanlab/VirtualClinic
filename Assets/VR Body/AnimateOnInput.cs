using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class AnimationInput
{
    public string animationPropertyName;
    public string actionName;

    [HideInInspector]
    public InputAction action;
}

public class AnimateOnInput : MonoBehaviour
{
    public InputActionAsset inputActions;
    public List<AnimationInput> animationInputs;
    public Animator animator;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("Input Actions asset is not assigned!", this);
            enabled = false;
            return;
        }

        foreach (var item in animationInputs)
        {
            item.action = inputActions.FindAction(item.actionName);

            if (item.action == null)
            {
                Debug.LogError(
                    "Could not find action: " + item.actionName,
                    this
                );
            }
        }
    }

    private void Update()
    {
        if (animator == null)
            return;

        foreach (var item in animationInputs)
        {
            if (item.action == null)
                continue;

            float actionValue = item.action.ReadValue<float>();

            animator.SetFloat(
                item.animationPropertyName,
                actionValue
            );
        }
    }
}