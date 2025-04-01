using System.Collections;
using System.Linq;
using FishNet;
using FishNet.Managing.Server;
using FishNet.Object;
using UnityEditor.PackageManager;
using UnityEngine;

public class PlayerSpawnerTest : NetworkBehaviour
{
    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private GameObject playerPrefab;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (IsServerInitialized)
        {
            StartCoroutine(WaitForClients());
        }
    }

    private IEnumerator WaitForClients()
    {
        ServerManager server = InstanceFinder.ServerManager;

        while (server.Clients.Count < 2)
        {
            yield return null;
        }

        var clients = server.Clients.Values.ToList();

        foreach (var client in clients)
        {
            GameObject playerObj = Instantiate(
                playerPrefab,
                spawnPoint.position,
                Quaternion.identity
            );
            NetworkObject player = playerObj.GetComponent<NetworkObject>();

            ServerManager.Spawn(player, client);
        }

        Debug.Log("SPAWNED");
    }
}
