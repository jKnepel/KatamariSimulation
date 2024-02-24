#undef DEBUG_BANDWIDTH

using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.Katamari.XRSimulation
{
    public class XRSimulationManager : MonoBehaviour
    {
		#region attributes

		[Header("References")]
		[SerializeField] private NetworkManager	_networkManager;
		[SerializeField] private Transform		_objectParent;

		[Header("Values")]
		[SerializeField] private int _tickRate = 64;
		[SerializeField] private int _maxNumberBytes = 1150;

		private NetworkObject[] _networkObjects;
		private readonly List<NetworkObject> _networkObjectsList = new();

		private float _currentTime = 0;
		private const string DATA_NAME = "XRSimulation";

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
			for (ushort i = 1; i <= _networkObjects.Length; i++)
			{
				_networkObjects[i-1].Initialise(i);
				_networkObjectsList.Add(_networkObjects[i-1]);
			}
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
			if (!_networkManager.IsConnected) return;

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
			_networkObjectsList.Sort((a, b) => a.CompareTo(b));
			BitWriter writer = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			int numberPosition = writer.Position;
			writer.Skip(writer.Int16);

			ushort numberOfObjects = 0;
			foreach (NetworkObject obj in _networkObjectsList)
			{
				if (obj.PriorityAccumulator < 0 || !obj.IsResponsible)
					continue;
				
				writer.WriteUInt16((ushort)obj.ObjectID);
				NetworkObjectState.WriteNetworkObjectState(writer, obj.GetState());
				obj.ResetPriority();
				numberOfObjects++;

				if (writer.ByteLength >= _maxNumberBytes) break;
			}

			writer.Position = numberPosition;
			writer.WriteUInt16(numberOfObjects);

			_networkManager.SendByteDataToAll(DATA_NAME, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);

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
				_networkObjects[index-1].SetState(sender, objData);
			}
		}

		#endregion
	}
}
