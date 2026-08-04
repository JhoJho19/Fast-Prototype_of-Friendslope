using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerInteractionHintPresenter))]
public sealed class PlayerAnimalCatchInteractor : MonoBehaviour
{
    private const string CatchActionName = "Catch";
    private const string DogPointName = "DogPoint";
    private const string CatPointName = "CatPoint";
    private const string ParrotPointName = "ParrotPoint";

    [SerializeField] private string catchHint = "Hold left button to catch";
    [SerializeField] private int hintPriority = 10;
    [SerializeField] private Transform dogPoint;
    [SerializeField] private Transform catPoint;
    [SerializeField] private Transform parrotPoint;

    private PlayerInteractor interactor;
    private PlayerInteractionHintPresenter hintPresenter;
    private PlayerInput playerInput;
    private Transform releaseAnchor;
    private InputAction catchAction;
    private CatchableAnimal currentCatchable;
    private CatchableAnimal carriedAnimal;

    private void Awake()
    {
        CacheReferences();
        FindCarryPoints();
    }

    private void Reset()
    {
        FindCarryPoints();
    }

    private void OnValidate()
    {
        FindCarryPoints();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToAction();
    }

    private void Update()
    {
        CacheReferences();

        if (catchAction == null)
        {
            SubscribeToAction();
        }

        if (carriedAnimal != null)
        {
            currentCatchable = null;
            return;
        }

        currentCatchable = ResolveCurrentCatchable();

        if (currentCatchable != null)
        {
            currentCatchable.FleeFrom(GetThreatSourcePosition());
            hintPresenter.SubmitHint(this, catchHint, hintPriority);
        }
    }

    private void OnDisable()
    {
        if (carriedAnimal != null)
        {
            ReleaseCarriedAnimal();
        }

        currentCatchable = null;
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

        if (releaseAnchor == null)
        {
            CharacterController characterController =
                GetComponentInParent<CharacterController>();

            if (characterController != null)
            {
                releaseAnchor = characterController.transform;
            }
            else if (playerInput != null)
            {
                releaseAnchor = playerInput.transform;
            }
            else
            {
                releaseAnchor = transform.parent != null
                    ? transform.parent
                    : transform.root;
            }
        }
    }

    private void FindCarryPoints()
    {
        Transform root = transform.root;

        if (dogPoint == null)
        {
            dogPoint = FindChildByName(root, DogPointName);
        }

        if (catPoint == null)
        {
            catPoint = FindChildByName(root, CatPointName);
        }

        if (parrotPoint == null)
        {
            parrotPoint = FindChildByName(root, ParrotPointName);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null &&
                candidate.name == childName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SubscribeToAction()
    {
        if (playerInput == null ||
            playerInput.actions == null)
        {
            return;
        }

        InputAction nextAction =
            playerInput.actions.FindAction(CatchActionName, false);

        if (nextAction == null ||
            ReferenceEquals(catchAction, nextAction))
        {
            return;
        }

        UnsubscribeFromAction();

        catchAction = nextAction;
        catchAction.started += OnCatchStarted;
        catchAction.canceled += OnCatchCanceled;
    }

    private void UnsubscribeFromAction()
    {
        if (catchAction == null)
        {
            return;
        }

        catchAction.started -= OnCatchStarted;
        catchAction.canceled -= OnCatchCanceled;
        catchAction = null;
    }

    private CatchableAnimal ResolveCurrentCatchable()
    {
        if (interactor == null ||
            !interactor.TryGetTargetComponent(out CatchableAnimal catchable) ||
            !catchable.CanBeCaught)
        {
            return null;
        }

        return catchable;
    }

    private void OnCatchStarted(InputAction.CallbackContext context)
    {
        if (carriedAnimal != null ||
            currentCatchable == null)
        {
            return;
        }

        Transform carryPoint = GetCarryPoint(currentCatchable.Kind);

        if (carryPoint == null ||
            !currentCatchable.BeginCarry(carryPoint))
        {
            return;
        }

        carriedAnimal = currentCatchable;
        currentCatchable = null;
    }

    private void OnCatchCanceled(InputAction.CallbackContext context)
    {
        if (carriedAnimal == null)
        {
            return;
        }

        ReleaseCarriedAnimal();
    }

    private void ReleaseCarriedAnimal()
    {
        if (carriedAnimal == null)
        {
            return;
        }

        Transform anchor =
            releaseAnchor != null
                ? releaseAnchor
                : (interactor != null ? interactor.PlayerRoot : transform.root);

        Vector3 playerForward =
            anchor.forward;

        if (interactor != null &&
            interactor.InteractionCamera != null)
        {
            Vector3 cameraForward =
                interactor.InteractionCamera.transform.forward;

            Vector3 flattenedCameraForward =
                Vector3.ProjectOnPlane(cameraForward, Vector3.up);

            if (flattenedCameraForward.sqrMagnitude > 0.0001f)
            {
                playerForward = flattenedCameraForward.normalized;
            }
        }

        carriedAnimal.Release(anchor.position, playerForward);
        carriedAnimal = null;
    }

    private Vector3 GetThreatSourcePosition()
    {
        if (releaseAnchor != null)
        {
            return releaseAnchor.position;
        }

        if (playerInput != null)
        {
            return playerInput.transform.position;
        }

        return transform.root.position;
    }

    private Transform GetCarryPoint(CatchableAnimalKind animalKind)
    {
        switch (animalKind)
        {
            case CatchableAnimalKind.Cat:
                return catPoint;
            case CatchableAnimalKind.Parrot:
                return parrotPoint;
            default:
                return dogPoint;
        }
    }
}
