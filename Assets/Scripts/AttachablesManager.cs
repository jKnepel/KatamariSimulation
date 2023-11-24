using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using System;
using UnityEngine;

namespace jKnepel.Katamari
{
	public class AttachablesManager : MonoBehaviour
	{
		#region attributes

		[SerializeField] private NetworkManager _networkManager;
		[SerializeField] private Transform	_attachableParent;
		[SerializeField] private Attachable _attachablePrefab;
		[SerializeField] private int		_numberOfAttachables = 50;
		[SerializeField] private float		_spawnOffset = 2.0f;
		[SerializeField] private int		_tickRate = 64;

		[SerializeField] private Attachable[] _attachables;

		private float _currentTime = 0;

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_networkManager == null)
				_networkManager = GameObject.FindWithTag("NetworkManager").GetComponent<NetworkManager>();
			if (_attachableParent == null)
				_attachableParent = transform;

			_networkManager.OnConnected += () => _networkManager.RegisterByteData("Attachables", UpdateAttachables);
			_networkManager.OnDisconnected += () => _networkManager.UnregisterByteData("Attachables", UpdateAttachables);

			_attachables = new Attachable[_numberOfAttachables];
			int numberOfColumns = (int)Math.Ceiling(Mathf.Sqrt(_numberOfAttachables));
			int numberOfRows = (int)Math.Ceiling((float)_numberOfAttachables / numberOfColumns);
			int remainder = _numberOfAttachables % numberOfRows;
			float startX = -(((float)(numberOfColumns - 1)) / 2 * _spawnOffset);
			float startZ = -(((float)(numberOfRows - 1)) / 2 * _spawnOffset);

			int index = 0;
			for (int i = 0; i < numberOfColumns; i++)
			{
				for (int j = 0; j < numberOfRows; j++)
				{
					if (i == numberOfColumns - 1 && j >= remainder)
						return;

					float x = startX + i * _spawnOffset;
					float z = startZ + j * _spawnOffset;
					Vector3 position = new(x, _attachablePrefab.transform.position.y, z);
					Attachable att = Instantiate(_attachablePrefab, position, _attachablePrefab.transform.rotation, _attachableParent);
					att.gameObject.name = $"Attachable#{index}";
					_attachables[index++] = att;
				}
			}

		}

		private void FixedUpdate()
		{
			if (!_networkManager.IsHost)
				return;

			if (_currentTime > 1 / (float)_tickRate)
			{
				SendAttachables();
				_currentTime = 0;
			}
			_currentTime += Time.deltaTime;
		}

		#endregion

		#region private methods

		private void SendAttachables()
		{
			BitWriter _bitWriter = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			foreach (Attachable att in _attachables)
				AttachableData.WriteAttachableData(_bitWriter, new(att));

			_networkManager.SendByteDataToAll("Attachables", _bitWriter.GetBuffer(), ENetworkChannel.ReliableOrdered);
		}

		private void UpdateAttachables(byte sender, byte[] data)
		{
			BitReader reader = new(data, _networkManager.NetworkConfiguration.SerialiserConfiguration);
			foreach (Attachable att in _attachables)
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
