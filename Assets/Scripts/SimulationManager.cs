using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.Katamari
{
    public class SimulationManager : MonoBehaviour
    {
		#region attributes

		[SerializeField] private NetworkManager		_networkManager;
		[SerializeField] private AttachablesManager _attachablesManager;
		[SerializeField] private Player				_player;
		[SerializeField] private int _tickRate = 64;

		private float _currentTime = 0;
		private const string DATA_NAME = "Simulation";

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_networkManager == null)
				_networkManager = GameObject.FindWithTag("NetworkManager").GetComponent<NetworkManager>();

			_networkManager.OnConnected += () => _networkManager.RegisterByteData(DATA_NAME, UpdateSimulation);
			_networkManager.OnDisconnected += () => _networkManager.UnregisterByteData(DATA_NAME, UpdateSimulation);
		}

		private void FixedUpdate()
		{
			if (!_networkManager.IsHost)
				return;

			if (_currentTime > 1 / (float)_tickRate)
			{
				SendSimulation();
				_currentTime = 0;
			}
			_currentTime += Time.deltaTime;
		}

		#endregion

		#region private methods

		private void SendSimulation()
		{
			BitWriter _bitWriter = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			PlayerData.WritePlayerData(_bitWriter, new(_player));
			foreach (Attachable att in _attachablesManager.Attachables)
				AttachableData.WriteAttachableData(_bitWriter, new(att));

			_networkManager.SendByteDataToAll(DATA_NAME, _bitWriter.GetBuffer(), ENetworkChannel.ReliableOrdered);
		}

		private void UpdateSimulation(byte sender, byte[] data)
		{
			BitReader reader = new(data, _networkManager.NetworkConfiguration.SerialiserConfiguration);
			PlayerData playerData = PlayerData.ReadPlayerData(reader);
			_player.Rigidbody.position = playerData.Position;
			_player.Rigidbody.rotation = playerData.Rotation;
			_player.Rigidbody.velocity = playerData.LinearVelocity;
			_player.Rigidbody.angularVelocity = playerData.AngularVelocity;

			foreach (Attachable att in _attachablesManager.Attachables)
			{
				AttachableData attData = AttachableData.ReadAttachableData(reader);
				att.Rigidbody.position = attData.Position;
				att.Rigidbody.rotation = attData.Rotation;
				att.Rigidbody.velocity = attData.LinearVelocity;
				att.Rigidbody.angularVelocity = attData.AngularVelocity;
			}
		}

		#endregion
	}
}
