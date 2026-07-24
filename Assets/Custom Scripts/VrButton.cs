
using System.Collections;
using UnityEngine;

public class VrButton : MonoBehaviour
{
    [Header("Button Movement")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private float pressDistance = 0.02f;
    [SerializeField] private float movementSpeed = 0.1f;

    [Header("Scripts to Start")]
    [SerializeField] private MonoBehaviour eyeTrackingScript;
    [SerializeField] private MonoBehaviour speechScript;

    [Header("Object to Show")]
    [SerializeField] private GameObject painting;   

    private Vector3 startingPosition;
    private Vector3 pressedPosition;
    private Coroutine movementCoroutine;

    private void Awake()
    {
        Debug.Log("button awake");
        if (buttonVisual == null)
        {
            buttonVisual = transform;
        }

        startingPosition = buttonVisual.localPosition;
        pressedPosition = startingPosition + Vector3.down * pressDistance;
    }

    public void PressButton()
    {
        Debug.Log("button pressed");
        MoveButton(pressedPosition);

        if (eyeTrackingScript != null)
        {
            eyeTrackingScript.enabled = true;
        }

        if (speechScript != null)
        {
            speechScript.enabled = true;
        }

        if (painting != null)
        {
            painting.SetActive(true);
        }
    }

    public void ReleaseButton()
    {
        MoveButton(startingPosition);
    }

    private void MoveButton(Vector3 targetPosition)
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        movementCoroutine = StartCoroutine(
            MoveButtonCoroutine(targetPosition)
        );
    }

    private IEnumerator MoveButtonCoroutine(Vector3 targetPosition)
    {
        while (Vector3.Distance(
                   buttonVisual.localPosition,
                   targetPosition) > 0.0001f)
        {
            buttonVisual.localPosition = Vector3.MoveTowards(
                buttonVisual.localPosition,
                targetPosition,
                movementSpeed * Time.deltaTime
            );

            yield return null;
        }

        buttonVisual.localPosition = targetPosition;
        movementCoroutine = null;
    }
}