using StarterAssets;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MakeTrap.Multiplayer
{
    public class NetworkPlayer : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(-1);
        public CinemachineCamera followCameraPrefab;
        public Transform cameraTarget;
        public Behaviour[] ownerBehaviours;
        public Renderer[] hideForFirstPersonOwner;
        public NetworkObject trapPrefab;
        public NetworkObject bananaTrapPrefab;
        float nextTrapTime;
        CinemachineCamera followCamera;
        public bool IsBot => IsSpawned && !NetworkObject.IsPlayerObject;

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void PlaceTrapRpc(Vector3 position, float yaw, int trapType = 0)
        {
            if (trapType < 0 || trapType > 1) return;
            var selectedPrefab = trapType == 1 ? bananaTrapPrefab : trapPrefab;
            var movement = GetComponent<ThirdPersonController>();
            if (!NetworkObject.IsPlayerObject || selectedPrefab == null || movement == null ||
                Time.time < nextTrapTime || !float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                !float.IsFinite(position.z) || !float.IsFinite(yaw) ||
                (position - transform.position).sqrMagnitude > 16f) return;
            if (!Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 2f,
                movement.GroundLayers, QueryTriggerInteraction.Ignore)) return;
            nextTrapTime = Time.time + 1f;
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, yaw, 0);
            var trap = Instantiate(selectedPrefab, hit.point + hit.normal * .3f, rotation);
            trap.Spawn(true);
        }

        public override void OnNetworkSpawn()
        {
            bool localHuman = IsOwner && NetworkObject.IsPlayerObject;
            foreach (var behaviour in ownerBehaviours) if (behaviour != null) behaviour.enabled = localHuman;
            foreach (var camera in GetComponentsInChildren<Camera>(true)) camera.enabled = localHuman;
            foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = localHuman;
            var bot = GetComponent<BotPatrol>();
            if (bot != null) bot.enabled = IsServer && !NetworkObject.IsPlayerObject;
            if (!localHuman) return;
            followCamera = Instantiate(followCameraPrefab);
            followCamera.Follow = cameraTarget;
            foreach (var renderer in hideForFirstPersonOwner) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        public override void OnNetworkDespawn()
        {
            if (followCamera != null) Destroy(followCamera.gameObject);
            if (IsOwner && NetworkObject.IsPlayerObject) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
