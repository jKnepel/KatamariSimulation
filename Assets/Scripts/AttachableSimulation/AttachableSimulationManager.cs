#undef DEBUG_BANDWIDTH

using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.Katamari.AttachableSimulation
{
	public class AttachableSimulationManager : MonoBehaviour
	{
		#region attributes

		[Header("References")]
		[SerializeField] private NetworkManager _networkManager;
		[SerializeField] private Transform _objectParent;
		[SerializeField] private NetworkObject _objectPrefab;

		[Header("Values")]
		[SerializeField] private int _numberOfObjects = 50;
		[SerializeField] private float _spawnDistance = 2.0f;
		[SerializeField] private int _tickRate = 64;
		[SerializeField] private int _maxNumberBytes = 1150;

		private NetworkObject[] _networkObjects;
		private readonly List<NetworkObject> _networkObjectsList = new();

		private float _currentTime = 0;
		private const string DATA_NAME = "AttachableSimulation";

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

			_networkObjects = new NetworkObject[_numberOfObjects];
			int numberOfColumns = (int)Math.Ceiling(Mathf.Sqrt(_numberOfObjects));
			int numberOfRows = (int)Math.Ceiling((float)_numberOfObjects / numberOfColumns);
			int remainder = _numberOfObjects % numberOfRows;
			float startX = -(((float)(numberOfColumns - 1)) / 2 * _spawnDistance);
			float startZ = -(((float)(numberOfRows - 1)) / 2 * _spawnDistance);

			int index = 1;
			for (int i = 0; i < numberOfColumns; i++)
			{
				for (int j = 0; j < numberOfRows; j++)
				{
					if (remainder > 0 && i == numberOfColumns - 1 && j >= remainder)
						return;

					float x = startX + i * _spawnDistance;
					float z = startZ + j * _spawnDistance;
					Vector3 position = new(x, _objectPrefab.transform.position.y, z);
					NetworkObject obj = Instantiate(_objectPrefab, position, _objectPrefab.transform.rotation, _objectParent);
					obj.Initialise(index);
					_networkObjectsList.Add(obj);
					_networkObjects[index-1] = obj;
					index++;
				}
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
				if (!obj.IsResponsible)
					continue;
				
				writer.WriteUInt16((ushort)obj.ObjectID);
				NetworkObjectState.WriteNetworkObjectState(writer, obj.GetState());
				obj.ResetPriority();
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
				_networkObjects[index-1].SetState(sender, objData);
			}
		}

		#endregion
	}
}
