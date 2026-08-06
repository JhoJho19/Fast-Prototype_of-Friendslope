using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerInteractionHintPresenter))]
public sealed class PlayerAnimalCatchInteractor : MonoBehaviour
{
    private const string CatchActionName = "Catch";
    private const string DogPointName = "DogPoint";
    private const string CatPointName = "CatPoint";
    private const string ParrotPointName = "ParrotPoint";
    private const string DogVisualName = "DogVisual";
    private const string CatVisualName = "CatVisual";
    private const string ParotVisualName = "ParotVisual";

    [SerializeField] private string catchHint = "Hold left button to catch";
    [SerializeField] private int hintPriority = 10;
    [SerializeField] private Transform dogPoint;
    [SerializeField] private Transform catPoint;
    [SerializeField] private Transform parrotPoint;
    [SerializeField] private GameObject dogVisual;
    [SerializeField] private GameObject catVisual;
    [SerializeField] private GameObject parotVisual;
    [SerializeField] private GameObject textMission;
    [SerializeField] private TextMeshProUGUI missionLabel;

    private PlayerInteractor interactor;
    private PlayerInteractionHintPresenter hintPresenter;
    private PlayerInput playerInput;
    private Transform releaseAnchor;
    private InputAction catchAction;
    private CatchableAnimal currentCatchable;
    private CatchableAnimal carriedAnimal;
    private Coroutine missionCoroutine;
    private Coroutine winMessageCoroutine;
    private bool networkCatchRequested;

    private void Awake()
    {
        CacheReferences();
        FindCarryPoints();
        FindCarryVisuals();
        HideCarryVisuals();
    }
    
    private void Start()
    {
        missionCoroutine = StartCoroutine(ShowInitialMission());
    }

    private void Reset()
    {
        FindCarryPoints();
        FindCarryVisuals();
    }

    private void OnValidate()
    {
        FindCarryPoints();
        FindCarryVisuals();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToAction();
        HideCarryVisuals();
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
            if (!NetworkClient.active || NetworkServer.active)
            {
                currentCatchable.FleeFrom(GetThreatSourcePosition());
            }

            hintPresenter.SubmitHint(this, catchHint, hintPriority);
        }
    }

    private void OnDisable()
    {
        if (networkCatchRequested)
        {
            GetComponentInParent<CoopNetworkPlayer>()
                ?.RequestReleaseAnimal();
            networkCatchRequested = false;
        }

        if (carriedAnimal != null)
        {
            ReleaseCarriedAnimal();
        }

        HideCarryVisuals();
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

    private void FindCarryVisuals()
    {
        Transform root = transform.root;

        if (dogVisual == null)
        {
            dogVisual = FindChildByName(root, DogVisualName)?.gameObject;
        }

        if (catVisual == null)
        {
            catVisual = FindChildByName(root, CatVisualName)?.gameObject;
        }

        if (parotVisual == null)
        {
            parotVisual = FindChildByName(root, ParotVisualName)?.gameObject;
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
            networkCatchRequested ||
            currentCatchable == null)
        {
            return;
        }

        CoopNetworkPlayer networkPlayer =
            GetComponentInParent<CoopNetworkPlayer>();

        if (networkPlayer != null &&
            NetworkClient.active)
        {
            networkCatchRequested = true;
            networkPlayer.RequestCatch(currentCatchable);
            currentCatchable = null;
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
        ShowCarryVisual(carriedAnimal.Kind);
        if (missionCoroutine != null)
        {
            StopCoroutine(missionCoroutine);
            missionCoroutine = null;
        }

        if (missionLabel != null)
        {
            missionLabel.text = "Bring it to the UFO";
        }

        if (textMission != null)
        {
            textMission.SetActive(true);
        }
    }

    private void OnCatchCanceled(InputAction.CallbackContext context)
    {
        if (networkCatchRequested)
        {
            GetComponentInParent<CoopNetworkPlayer>()
                ?.RequestReleaseAnimal();
            networkCatchRequested = false;
            return;
        }

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

        HideCarryVisuals();

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
        if (textMission != null)
        {
            textMission.SetActive(false);
        }
    }

    public void ApplyNetworkCarry(CatchableAnimal animal)
    {
        if (animal == null)
        {
            return;
        }

        carriedAnimal = animal;
        networkCatchRequested = true;
        currentCatchable = null;
        ShowCarryVisual(animal.Kind);

        if (missionCoroutine != null)
        {
            StopCoroutine(missionCoroutine);
            missionCoroutine = null;
        }

        if (missionLabel != null)
        {
            missionLabel.text = "Bring it to the UFO";
        }

        if (textMission != null)
        {
            textMission.SetActive(true);
        }
    }

    public void ApplyNetworkRelease()
    {
        networkCatchRequested = false;
        carriedAnimal = null;
        currentCatchable = null;
        HideCarryVisuals();

        if (textMission != null)
        {
            textMission.SetActive(false);
        }
    }

    public void ShowWinMessage(float duration)
    {
        if (missionCoroutine != null)
        {
            StopCoroutine(missionCoroutine);
            missionCoroutine = null;
        }

        if (winMessageCoroutine != null)
        {
            StopCoroutine(winMessageCoroutine);
        }

        if (missionLabel != null)
        {
            missionLabel.text = "YOU WIN";
        }

        if (textMission != null)
        {
            textMission.SetActive(true);
        }

        winMessageCoroutine = StartCoroutine(
            HideWinMessageAfterDelay(duration));
    }

    private IEnumerator HideWinMessageAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));

        if (textMission != null)
        {
            textMission.SetActive(false);
        }

        winMessageCoroutine = null;
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

    private void ShowCarryVisual(CatchableAnimalKind animalKind)
    {
        HideCarryVisuals();

        GameObject visual = GetCarryVisual(animalKind);

        if (visual != null &&
            !visual.activeSelf)
        {
            visual.SetActive(true);
        }
    }

    private void HideCarryVisuals()
    {
        SetVisualActive(dogVisual, false);
        SetVisualActive(catVisual, false);
        SetVisualActive(parotVisual, false);
    }

    private GameObject GetCarryVisual(CatchableAnimalKind animalKind)
    {
        switch (animalKind)
        {
            case CatchableAnimalKind.Cat:
                return catVisual;
            case CatchableAnimalKind.Parrot:
                return parotVisual;
            default:
                return dogVisual;
        }
    }

    private static void SetVisualActive(GameObject visual, bool active)
    {
        if (visual != null &&
            visual.activeSelf != active)
        {
            visual.SetActive(active);
        }
    }

    private IEnumerator ShowInitialMission()
    {
        if (textMission == null || missionLabel == null)
        {
            yield break;
        }

        missionLabel.text = "Catch an animal and bring them to this UFO";
        textMission.SetActive(true);
        yield return new WaitForSeconds(3f);
        textMission.SetActive(false);
        missionCoroutine = null;
    }
}
