using Unity.Netcode;
using UnityEngine;

namespace MakeTrap.Multiplayer
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        [Tooltip("KidSpawn01, KidSpawn02, ThiefSpawn01, ThiefSpawn02 순서")]
        public Transform[] spawnPoints = new Transform[4];
        public void SpawnPlayers(NetworkSessionManager session)
        {
            if (!session.Net.IsServer || LobbyManager.Instance == null) return;
            foreach (var player in LobbyManager.Instance.Players)
            {
                if (player.Slot < 0 || player.Slot >= spawnPoints.Length || spawnPoints[player.Slot] == null) continue;
                if (!player.IsBot && (!session.Net.ConnectedClients.TryGetValue(player.ClientId, out var client) || client.PlayerObject != null)) continue;
                bool alreadySpawned = false;
                foreach (var existing in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                    if (existing.IsSpawned && existing.Slot.Value == player.Slot) { alreadySpawned = true; break; }
                if (alreadySpawned) continue;
                var point = spawnPoints[player.Slot];
                var prefab = player.Team == PlayerTeam.Kids ? session.kidsPrefab : session.thievesPrefab;
                var variants = player.Team == PlayerTeam.Kids ? session.kidVariants : session.thiefVariants;
                if (variants != null && player.CharacterIndex >= 0 && player.CharacterIndex < variants.Length && variants[player.CharacterIndex] != null)
                    prefab = variants[player.CharacterIndex];
                var instance = Instantiate(prefab, point.position, point.rotation);
                if (player.IsBot) instance.Spawn(true);
                else instance.SpawnAsPlayerObject(player.ClientId, true);
                instance.GetComponent<NetworkPlayer>().Slot.Value = player.Slot;
            }
        }
    }
}
