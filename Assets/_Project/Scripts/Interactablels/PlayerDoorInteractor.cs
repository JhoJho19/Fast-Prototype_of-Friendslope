using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerInteractionHintPresenter))]
public sealed class PlayerDoorInteractor : MonoBehaviour
{
    private const string InteractActionName = "Interact";

    [SerializeField] private string interactionHint = "Press \"E\" to interact";
    [SerializeField] private int hintPriority;

    private PlayerInteractor interactor;
    private PlayerInteractionHintPresenter hintPresenter;
    private PlayerInput playerInput;
    private InputAction interactAction;
    private IInteractable currentInteractable;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToAction();
    }

    private void Update()
    {
        CacheReferences();

        if (interactAction == null)
        {
            SubscribeToAction();
        }

        currentInteractable = ResolveCurrentInteractable();

        if (currentInteractable != null)
        {
            hintPresenter.SubmitHint(this, interactionHint, hintPriority);
        }
    }

    private void OnDisable()
    {
        currentInteractable = null;
        UnsubscribeFromAction();
    }

    private void CacheReferences()
    {
        if (interactor == null)
        {
            interactor = GetComponent<PlayerInteractor>();
        }

        if (hintPresenter == null)
        {
            hintPresenter = GetComponent<PlayerInteractionHintPresenter>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponentInParent<PlayerInput>();
        }
    }

    private void SubscribeToAction()
    {
        if (playerInput == null ||
            playerInput.actions == null)
        {
            return;
        }

        InputAction nextAction =
            playerInput.actions.FindAction(InteractActionName, false);

        if (nextAction == null ||
            ReferenceEquals(interactAction, nextAction))
        {
            return;
        }

        UnsubscribeFromAction();

        interactAction = nextAction;
        interactAction.performed += OnInteractPerformed;
    }

    private void UnsubscribeFromAction()
    {
        if (interactAction == null)
        {
            return;
        }

        interactAction.performed -= OnInteractPerformed;
        interactAction = null;
    }

    private IInteractable ResolveCurrentInteractable()
    {
        if (interactor == null ||
            !interactor.TryGetTargetComponent(out IInteractable interactable))
        {
            return null;
        }

        return interactable;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        currentInteractable?.Interact();
    }
}
