using ithappy.Animals_FREE;
using UnityEngine;

[RequireComponent(typeof(CreatureMover))]
public class TrainingAnimalWalker : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private bool walkWhenEnabled = true;

    private CreatureMover mover;
    private MovePlayerInput playerInput;

    private void Awake()
    {
        mover = GetComponent<CreatureMover>();
        playerInput = GetComponent<MovePlayerInput>();

        if (playerInput != null)
        {
            playerInput.enabled = false;
        }
    }

    private void Update()
    {
        if (!walkWhenEnabled || mover == null)
        {
            return;
        }

        Vector3 lookTarget = target != null
            ? target.position
            : transform.position + transform.forward * 2f;

        Vector2 forwardOnly = Vector2.up;
        bool walkOnly = false;
        bool noJump = false;

        mover.SetInput(in forwardOnly, in lookTarget, in walkOnly, in noJump);
    }
}
