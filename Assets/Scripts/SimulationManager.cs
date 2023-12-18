#undef DEBUG_BANDWIDTH

using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.Katamari
{
    public class SimulationManager : MonoBehaviour
    {
		#region attributes

		[Header("References")]
		[SerializeField] private NetworkManager	_networkManager;
		[SerializeField] private Transform		_objectParent;

		[Header("Values")]
		[SerializeField] private int _tickRate = 64;
		[SerializeField] private int _maxNumberBytes = 1150;

		private NetworkObject[] _networkObjects;
		private readonly List<(ushort, NetworkObject)> _networkObjectsList = new();

		private float _currentTime = 0;
		private const string DATA_NAME = "Simulation";

#if DEBUG_BANDWIDTH
		private int _sendBytes = 0;
		private int _sendBytesTotal = 0;
		private int _secondCount = 1;
		private float _frameTime = 0;
#endif

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_objectParent == null)
				_objectParent = transform;

			if (_networkManager.IsConnected)
				_networkManager.RegisterByteData(DATA_NAME, UpdateSimulation);
			else
				_networkManager.OnConnected += () => _networkManager.RegisterByteData(DATA_NAME, UpdateSimulation);
		}

		private void Start()
		{
			_networkObjects = _objectParent.GetComponentsInChildren<NetworkObject>();
			for (ushort i = 0; i < _networkObjects.Length; i++)
				_networkObjectsList.Add((i, _networkObjects[i]));
		}

		private void OnDisable()
		{
			if (_networkManager.IsConnected)
				_networkManager.UnregisterByteData(DATA_NAME, UpdateSimulation);
		}

		private void FixedUpdate()
		{
			if (!_networkManager.IsConnected) return;

			if (_currentTime > 1f / _tickRate)
			{
				SendSimulation();
				_currentTime = 0;
			}
			_currentTime += Time.deltaTime;
		}

#if DEBUG_BANDWIDTH
		private void Update()
		{
			_frameTime += Time.deltaTime;
			if (_frameTime > 1)
			{
				Debug.Log($"Byte Information:\n"
				 + $"Bytes This Second: {_sendBytes}B\n"
				 + $"Bytes Total: {_sendBytesTotal}B\n"
				 + $"Bytes Per Second: {(float)_sendBytes/_secondCount/1024}kBps\n");

				_frameTime = 0;
				_sendBytes = 0;
				_secondCount++;
			}
		}
#endif

		#endregion

		#region private methods

		private void SendSimulation()
		{
			_networkObjectsList.Sort((a, b) => a.Item2.CompareTo(b.Item2));
			BitWriter writer = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			int numberPosition = writer.Position;
			writer.Skip(writer.Int16);

			ushort numberOfObjects = 0;
			foreach ((ushort, NetworkObject) obj in _networkObjectsList)
			{
				if (obj.Item2.PriorityAccumulator < 0 || !obj.Item2.IsResponsible)
					continue;

				writer.WriteUInt16(obj.Item1);
				NetworkObjectState.WriteNetworkObjectState(writer, obj.Item2.GetState());
				obj.Item2.ResetPriority();
				numberOfObjects++;

				if (writer.ByteLength >= _maxNumberBytes) break;
			}

			writer.Position = numberPosition;
			writer.WriteUInt16(numberOfObjects);

			if (_networkManager.IsHost)
				_networkManager.SendByteDataToAll(DATA_NAME, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
			else
				_networkManager.SendByteDataToServer(DATA_NAME, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);

#if DEBUG_BANDWIDTH
			_sendBytes += writer.ByteLength;
			_sendBytesTotal += writer.ByteLength;
#endif
		}

		private void UpdateSimulation(byte sender, byte[] data)
		{
			BitReader reader = new(data, _networkManager.NetworkConfiguration.SerialiserConfiguration);

			ushort numberOfObjects = reader.ReadUInt16();
			for (ushort i = 0; i < numberOfObjects; i++)
			{
				int index = reader.ReadUInt16();
				NetworkObjectState objData = NetworkObjectState.ReadNetworkObjectState(reader);
				_networkObjects[index].SetState(sender, objData);
			}
		}

		#endregion
	}
}
