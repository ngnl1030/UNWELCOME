using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

[RequireComponent(typeof(Animator), typeof(ThirdPersonController))]
public class TrapPlacementAnimation : MonoBehaviour
{
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private GameObject bananaPrefab;
    [SerializeField] private Material previewMaterial;
    [SerializeField] private float placementDistance = 1.2f;
    private bool placementPending;
    private GameObject preview;
    private bool validPlacement;
    private Vector3 placementPosition;
    private Quaternion placementRotation;
    private Animator animator;
    private ThirdPersonController movement;
    private static readonly int PlaceTrap = Animator.StringToHash("PlaceTrap");
    private int selectedTrap;
    public int SelectedTrap => selectedTrap;
    public bool PreviewActive => preview != null;
    public bool IsPlacing => placementPending;
    private GameObject SelectedPrefab => selectedTrap == 1 ? bananaPrefab : boxPrefab;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<ThirdPersonController>();
    }

    private void Update()
    {
        if (placementPending)
            return;

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("PlaceTrap") ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("PlaceTrap")))
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClearPreview();
            return;
        }

        int requested = Keyboard.current == null ? -1 : Keyboard.current.qKey.wasPressedThisFrame ? 0 : Keyboard.current.eKey.wasPressedThisFrame ? 1 : -1;
        if (requested >= 0)
        {
            if (preview != null && selectedTrap == requested)
            {
                ClearPreview();
                return;
            }
            ClearPreview();
            selectedTrap = requested;
            if (SelectedPrefab == null || previewMaterial == null || !movement.Grounded)
                return;

            preview = Instantiate(SelectedPrefab);
            preview.name = "TrapPlacementPreview";
            foreach (var collider in preview.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = previewMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        if (preview == null) return;
        UpdatePreview();
        if (validPlacement && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            placementPending = true;
            var networkPlayer = GetComponent<MakeTrap.Multiplayer.NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.IsSpawned)
                GetComponent<MakeTrap.Multiplayer.OwnerNetworkAnimator>().SetTrigger(PlaceTrap);
            else animator.SetTrigger(PlaceTrap);
        }
    }

    private void UpdatePreview()
    {
        Vector3 position = transform.position + transform.forward * placementDistance;
        validPlacement = Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down,
            out RaycastHit hit, 4f, movement.GroundLayers, QueryTriggerInteraction.Ignore) && movement.Grounded;
        if (validPlacement)
        {
            placementRotation = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            placementPosition = hit.point + hit.normal * 0.3f;
            preview.transform.SetPositionAndRotation(placementPosition, placementRotation);
        }
        preview.SetActive(validPlacement);
    }

    // Called near the end of the installation animation.
    public void PlaceBox()
    {
        if (!placementPending || SelectedPrefab == null) return;
        placementPending = false;

        var networkPlayer = GetComponent<MakeTrap.Multiplayer.NetworkPlayer>();
        if (networkPlayer != null && networkPlayer.IsSpawned)
        {
            if (networkPlayer.IsOwner) networkPlayer.PlaceTrapRpc(placementPosition, placementRotation.eulerAngles.y, selectedTrap);
        }
        else Instantiate(SelectedPrefab, placementPosition, placementRotation);
        ClearPreview();
    }

    private void ClearPreview()
    {
        if (preview != null)
        {
            preview.SetActive(false);
            Destroy(preview);
        }
        preview = null;
        validPlacement = false;
    }

    private void OnDisable()
    {
        placementPending = false;
        ClearPreview();
    }
}
