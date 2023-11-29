using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.Katamari
{
    public class SimulationManager : MonoBehaviour
    {
		#region attributes

		[Header("References")]
		[SerializeField] private NetworkManager	_networkManager;
		[SerializeField] private NetworkObject	_player;
		[SerializeField] private Transform		_objectParent;
		[SerializeField] private NetworkObject	_objectPrefab;

		[Header("Values")]
		[SerializeField] private int _numberOfObjects = 50;
		[SerializeField] private float _spawnDistance = 2.0f;
		[SerializeField] private int _tickRate = 64;
		[SerializeField] private int _maxNumberBytes = 1150;

		private NetworkObject[] _networkObjects;
		private readonly List<(ushort, NetworkObject)> _networkObjectsList = new();

		private float _currentTime = 0;
		private const string DATA_NAME = "Simulation";

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_objectParent == null)
				_objectParent = transform;

			SetupSimulation();
			_networkManager.OnConnected += () => _networkManager.RegisterByteData(DATA_NAME, UpdateSimulationAccumulator);
			_networkManager.OnDisconnected += () => _networkManager.UnregisterByteData(DATA_NAME, UpdateSimulationAccumulator);
		}

		private void FixedUpdate()
		{
			if (!_networkManager.IsHost)
				return;

			if (_currentTime > 1 / (float)_tickRate)
			{
				SendSimulationAccumulator();
				_currentTime = 0;
			}
			_currentTime += Time.deltaTime;
		}

		#endregion

		#region private methods

		private void SetupSimulation()
		{
			_networkObjects = new NetworkObject[_numberOfObjects];
			int numberOfColumns = (int)Math.Ceiling(Mathf.Sqrt(_numberOfObjects));
			int numberOfRows = (int)Math.Ceiling((float)_numberOfObjects / numberOfColumns);
			int remainder = _numberOfObjects % numberOfRows;
			float startX = -(((float)(numberOfColumns - 1)) / 2 * _spawnDistance);
			float startZ = -(((float)(numberOfRows    - 1)) / 2 * _spawnDistance);

			int index = 0;
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
					obj.gameObject.name = $"Object#{index}";
					_networkObjectsList.Add(((ushort)index, obj));
					_networkObjects[index++] = obj;
				}
			}
		}

		#region version 1

		private void SendSimulation()
		{
			BitWriter writer = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			NetworkObjectData.WriteNetworkObjectData(writer, _player.GetData());
			foreach (NetworkObject obj in _networkObjects)
				NetworkObjectData.WriteNetworkObjectData(writer, obj.GetData());

			_networkManager.SendByteDataToAll(DATA_NAME, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
		}

		private void UpdateSimulation(byte sender, byte[] data)
		{
			BitReader reader = new(data, _networkManager.NetworkConfiguration.SerialiserConfiguration);
			NetworkObjectData playerData = NetworkObjectData.ReadNetworkObjectData(reader);
			_player.SetData(playerData);

			foreach (NetworkObject obj in _networkObjects)
			{
				NetworkObjectData objData = NetworkObjectData.ReadNetworkObjectData(reader);
				obj.SetData(objData);
			}
		}

		#endregion

		#region version 2

		private void SendSimulationAccumulator()
		{
			_networkObjectsList.Sort((a, b) => a.Item2.CompareTo(b.Item2));
			BitWriter writer = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			NetworkObjectData.WriteNetworkObjectData(writer, _player.GetData());
			int numberPosition = writer.Position;
			writer.Skip(writer.Int16);

			ushort numberOfObjects = 0;
			foreach ((ushort, NetworkObject) obj in _networkObjectsList)
			{
				if (obj.Item2.PriorityAccumulator < 0)
					continue;

				writer.WriteUInt16(obj.Item1);
				NetworkObjectData.WriteNetworkObjectData(writer, obj.Item2.GetData());
				obj.Item2.ResetPriority();
				numberOfObjects++;

				if (writer.ByteLength >= _maxNumberBytes) break;
			}

			writer.Position = numberPosition;
			writer.WriteUInt16(numberOfObjects);

			_networkManager.SendByteDataToAll(DATA_NAME, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
		}

		private void UpdateSimulationAccumulator(byte sender, byte[] data)
		{
			BitReader reader = new(data, _networkManager.NetworkConfiguration.SerialiserConfiguration);
			NetworkObjectData playerData = NetworkObjectData.ReadNetworkObjectData(reader);
			_player.SetData(playerData);

			ushort numberOfObjects = reader.ReadUInt16();
			for (ushort i = 0; i < numberOfObjects; i++)
			{
				int index = reader.ReadUInt16();
				NetworkObjectData objData = NetworkObjectData.ReadNetworkObjectData(reader);
				_networkObjects[index].SetData(objData);
			}
		}

		#endregion

		#endregion
	}
}
