#define DEBUG_AUTHORITY

using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using System;
using UnityEngine;

namespace jKnepel.Katamari
{
	[RequireComponent(typeof(Rigidbody))]
	public class NetworkObject : MonoBehaviour
	{
		#region attributes

		[SerializeField] private NetworkManager _networkManager;
		[SerializeField] private Rigidbody _rb;
		[SerializeField] private int _forceRestAfterFrames = 16;
		[SerializeField] private float _restThreshold = 0.5f;

#if DEBUG_AUTHORITY
		[SerializeField] private MeshRenderer _meshRenderer;
		[SerializeField] private Material _material;
		private Material _materialInstance;
#endif

		private byte ClientID => _networkManager.ClientInformation?.ID ?? 0;
		public bool IsAuthor => _networkManager.IsConnected && _authorityID == ClientID;
		public bool IsOwner => _networkManager.IsConnected && _ownershipID == ClientID;
		public bool IsResponsible => IsAuthor || (_networkManager.IsHost && _authorityID == 0);

		private byte _ownershipID = 0;
		private byte _authorityID = 0;
		private ushort _ownershipSequence = 0;
		private ushort _authoritySequence = 0;

		private Action<bool> _onOwnershipTaken;
		private Action<bool> _onAuthorityTaken;

		public float PriorityAccumulator => _priorityAccumulator;
		private float _priorityAccumulator = 0;
		public float Priority => _priority;
		private float _priority = 0.1f;

		private int _frameNumber = 0;
		private bool _stillAtRest = false;

		private string _objectName;

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_networkManager == null)
				_networkManager = GameObject.FindWithTag("NetworkManager").GetComponent<NetworkManager>();
			if (_rb == null)
				_rb = GetComponent<Rigidbody>();
#if DEBUG_AUTHORITY
			if (_meshRenderer == null)
				_meshRenderer = GetComponent<MeshRenderer>();
			_meshRenderer.material = _materialInstance = Instantiate(_material);
#endif

			_objectName = gameObject.name;
			if (_networkManager.IsConnected)
				RegisterNetworkObject();
			else
				_networkManager.OnConnected += RegisterNetworkObject;
			_networkManager.OnClientDisconnected += ReleaseHostageAuthorities;
		}

		private void OnDisable()
		{
			if (_networkManager.IsConnected)
			{
				if (_networkManager.IsHost)
					_networkManager.UnregisterByteData(_objectName, UpdateOwnershipHost);
				else
					_networkManager.UnregisterByteData(_objectName, UpdateOwnership);
			}
		}

		private void FixedUpdate()
		{
			if (!IsResponsible) return;

			_priority = _rb.velocity.magnitude + _rb.angularVelocity.magnitude;
			_priorityAccumulator += _priority;
			
			_frameNumber++;
			float kineticEnergy = Mathf.Pow(_rb.velocity.magnitude, 2) * 0.5f + Mathf.Pow(_rb.angularVelocity.magnitude, 2) * 0.5f;
			_stillAtRest = kineticEnergy < _restThreshold;

			if (!_stillAtRest)
			{
				_frameNumber = 0;
				return;
			}

			if (_frameNumber >= _forceRestAfterFrames)
			{
				_rb.velocity = _rb.angularVelocity = Vector3.zero;
				_frameNumber = 0;
				ReleaseAuthority();
			}
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (collision == null || !IsAuthor) return;
			if (!collision.gameObject.TryGetComponent<NetworkObject>(out var obj)) return;
			obj.TakeAuthority();
		}

		#endregion

		#region public methods

		public NetworkObjectState GetState()
		{
			return new()
			{
				OwnershipSequence = _ownershipSequence,
				AuthoritySequence = _authoritySequence,
				Position = _rb.position,
				Rotation = _rb.rotation,
				AtRest = _stillAtRest,
				LinearVelocity = _rb.velocity,
				AngularVelocity = _rb.angularVelocity
			};
		}

		public void SetState(byte sender, NetworkObjectState data)
		{
			if (_networkManager.IsHost && _authorityID != sender 
				|| !_networkManager.IsHost && _authorityID == ClientID) return;

			_rb.position = data.Position;
			_rb.rotation = data.Rotation;
			_rb.velocity = data.LinearVelocity;
			_rb.angularVelocity = data.AngularVelocity;
		}

		public void ResetPriority() => _priorityAccumulator = 0;

		public void TakeOwnership() => TakeOwnership(null);
		public void TakeOwnership(Action<bool> onOwnershipTaken = null)
		{
			if (_ownershipID > 0)
			{
				onOwnershipTaken?.Invoke(false);
				return;
			}

			_onOwnershipTaken = onOwnershipTaken;
			_ownershipSequence++;
			if (_authorityID != ClientID) _authoritySequence++;
			BitWriter bitWriter = new();
			NetworkObjectPacket packet = new(NetworkObjectPacket.EPacketTypes.TakeOwnership, ClientID, _ownershipSequence, _authoritySequence);
			if (_networkManager.IsHost)
			{
				SetTakeOwnership(ClientID, _ownershipSequence);
				if (_authorityID != ClientID) SetTakeAuthority(ClientID, _authoritySequence);
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToAll(_objectName, bitWriter.GetBuffer());
			}
			else
			{
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToServer(_objectName, bitWriter.GetBuffer());
			}
		}

		public void ReleaseOwnership()
		{
			if (!IsOwner) return;

			BitWriter bitWriter = new();
			_ownershipSequence++;
			NetworkObjectPacket packet = new(NetworkObjectPacket.EPacketTypes.ReleaseOwnership, ClientID, _ownershipSequence, _authoritySequence);
			if (_networkManager.IsHost)
			{
				SetReleaseOwnership(packet.OwnershipSequence);
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToAll(_objectName, bitWriter.GetBuffer());
			}
			else
			{
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToServer(_objectName, bitWriter.GetBuffer());
			}
		}

		public void TakeAuthority() => TakeAuthority(null);
		public void TakeAuthority(Action<bool> onAuthorityTaken = null)
		{
			if (IsAuthor || _ownershipID != 0)
			{
				onAuthorityTaken?.Invoke(false);
				return;
			}

			_onAuthorityTaken = onAuthorityTaken;
			_authoritySequence++;
			BitWriter bitWriter = new();
			NetworkObjectPacket packet = new(NetworkObjectPacket.EPacketTypes.TakeAuthority, ClientID, _ownershipSequence, _authoritySequence);
			if (_networkManager.IsHost)
			{
				SetTakeAuthority(ClientID, _authoritySequence);
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToAll(_objectName, bitWriter.GetBuffer());
			}
			else
			{
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToServer(_objectName, bitWriter.GetBuffer());
			}
		}

		public void ReleaseAuthority()
		{
			if (!IsAuthor || IsOwner) return;

			_authoritySequence++;
			BitWriter bitWriter = new();
			NetworkObjectPacket packet = new(NetworkObjectPacket.EPacketTypes.ReleaseAuthority, ClientID, _ownershipSequence, _authoritySequence);
			if (_networkManager.IsHost)
			{
				SetReleaseAuthority(packet.AuthoritySequence);
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToAll(_objectName, bitWriter.GetBuffer());
			}
			else
			{
				NetworkObjectPacket.WriteNetworkObjectPacket(bitWriter, packet);
				_networkManager.SendByteDataToServer(_objectName, bitWriter.GetBuffer());
			}
		}

		public int CompareTo(NetworkObject other)
		{
			if (other == null) return 1;
			if (PriorityAccumulator == other.PriorityAccumulator) return 0;
			if (PriorityAccumulator < other.PriorityAccumulator) return 1;
			return -1;
		}

		#endregion

		#region private methods

		private void RegisterNetworkObject()
		{
			if (_networkManager.IsHost)
				_networkManager.RegisterByteData(_objectName, UpdateOwnershipHost);
			else
				_networkManager.RegisterByteData(_objectName, UpdateOwnership);
		}

		private void ReleaseHostageAuthorities(byte clientID)
		{
			if (_ownershipID == clientID)
			{
				_ownershipID++;
				SetReleaseOwnership(_ownershipID);
			}

			if (_authorityID == clientID)
			{
				_authoritySequence++;
				SetReleaseAuthority(_authoritySequence);
			}
		}

		private void UpdateOwnershipHost(byte sender, byte[] data)
		{
			BitReader reader = new(data);
			NetworkObjectPacket packet = NetworkObjectPacket.ReadNetworkObjectPacket(reader);
			switch (packet.PacketType)
			{
				case NetworkObjectPacket.EPacketTypes.TakeOwnership:
					if (_ownershipID != 0 || !IsOwnershipNewer(packet.OwnershipSequence))
					{
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.TakeOwnership, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteData(sender, _objectName, writer.GetBuffer());
					}
					else
					{
						SetTakeOwnership(sender, packet.OwnershipSequence);
						if (_authorityID != sender) SetTakeAuthority(sender, packet.AuthoritySequence);
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.TakeOwnership, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteDataToAll(_objectName, writer.GetBuffer());
					}
					break;
				case NetworkObjectPacket.EPacketTypes.ReleaseOwnership:
					if (_ownershipID != sender || !IsOwnershipNewer(packet.OwnershipSequence))
					{
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.ReleaseOwnership, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteData(sender, _objectName, writer.GetBuffer());
					}
					else
					{
						SetReleaseOwnership(packet.OwnershipSequence);
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.ReleaseOwnership, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteDataToAll(_objectName, writer.GetBuffer());
					}
					break;
				case NetworkObjectPacket.EPacketTypes.TakeAuthority:
					if (_ownershipID != 0 || _authorityID == sender || !IsAuthorityNewer(packet.AuthoritySequence))
					{
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.TakeAuthority, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteData(sender, _objectName, writer.GetBuffer());
					}
					else
					{
						SetTakeAuthority(sender, packet.AuthoritySequence);
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.TakeAuthority, sender, _authoritySequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteDataToAll(_objectName, writer.GetBuffer());
					}
					break;
				case NetworkObjectPacket.EPacketTypes.ReleaseAuthority:
					if (_authorityID != sender || !IsAuthorityNewer(packet.AuthoritySequence))
					{
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.ReleaseAuthority, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteData(sender, _objectName, writer.GetBuffer());
					}
					else
					{
						SetReleaseAuthority(packet.AuthoritySequence);
						BitWriter writer = new();
						NetworkObjectPacket answer = new(NetworkObjectPacket.EPacketTypes.ReleaseAuthority, sender, _ownershipSequence, _authoritySequence);
						NetworkObjectPacket.WriteNetworkObjectPacket(writer, answer);
						_networkManager.SendByteDataToAll(_objectName, writer.GetBuffer());
					}
					break;
			}
		}

		private void UpdateOwnership(byte sender, byte[] data)
		{
			BitReader reader = new(data);
			NetworkObjectPacket packet = NetworkObjectPacket.ReadNetworkObjectPacket(reader);
			switch (packet.PacketType)
			{
				// TODO : clients should also check sequence
				case NetworkObjectPacket.EPacketTypes.TakeOwnership:
					SetTakeOwnership(packet.ClientID, packet.OwnershipSequence);
					SetTakeAuthority(packet.ClientID, packet.AuthoritySequence);
					break;
				case NetworkObjectPacket.EPacketTypes.ReleaseOwnership:
					SetReleaseOwnership(packet.AuthoritySequence);
					break;
				case NetworkObjectPacket.EPacketTypes.TakeAuthority:
					SetTakeAuthority(packet.ClientID, packet.AuthoritySequence);
					break;
				case NetworkObjectPacket.EPacketTypes.ReleaseAuthority:
					SetReleaseAuthority(packet.AuthoritySequence);
					break;
			}
		}

		private void SetTakeOwnership(byte clientID, ushort ownershipSequence)
		{
			_ownershipID = clientID;
			_ownershipSequence = ownershipSequence;
			
			if (_onOwnershipTaken != null)
			{
				_onOwnershipTaken.Invoke(clientID == ClientID);
				_onOwnershipTaken = null;
			}
		}

		private void SetReleaseOwnership(ushort ownershipSequence)
		{
			_ownershipID = 0;
			_ownershipSequence = ownershipSequence;
		}

		private void SetTakeAuthority(byte clientID, ushort authoritySequence)
		{
			_authorityID = clientID;
			_authoritySequence = authoritySequence;

			if (_onAuthorityTaken != null)
			{
				_onAuthorityTaken.Invoke(clientID == ClientID);
				_onAuthorityTaken = null;
			}

#if DEBUG_AUTHORITY
			if (clientID == ClientID)
				_materialInstance.color = _networkManager.ClientInformation.Color;
			else
				_materialInstance.color = _networkManager.ConnectedClients[clientID].Color;
#endif
		}

		private void SetReleaseAuthority(ushort authoritySequence)
		{
			_authorityID = 0;
			_authoritySequence = authoritySequence;

#if DEBUG_AUTHORITY
			_materialInstance.color = Color.white;
#endif
		}

		private const ushort HALF_USHORT = ushort.MaxValue / 2;
		private bool IsOwnershipNewer(ushort ownershipSequence)
		{
			return ((ownershipSequence > _ownershipSequence) && (ownershipSequence - _ownershipSequence <= HALF_USHORT))
				|| ((ownershipSequence < _ownershipSequence) && (_ownershipSequence - ownershipSequence > HALF_USHORT));
		}

		private bool IsAuthorityNewer(ushort authoritySequence)
		{
			return ((authoritySequence > _authoritySequence) && (authoritySequence - _authoritySequence <= HALF_USHORT))
				|| ((authoritySequence < _authoritySequence) && (_authoritySequence - authoritySequence > HALF_USHORT));
		}

		#endregion
	}

	public struct NetworkObjectState : IStructData
	{
		public ushort OwnershipSequence;
		public ushort AuthoritySequence;
		public Vector3 Position;
		public Quaternion Rotation;
		public bool AtRest;
		public Vector3 LinearVelocity;
		public Vector3 AngularVelocity;

		public static NetworkObjectState ReadNetworkObjectState(BitReader reader)
		{
			ushort authoritySequence = reader.ReadUInt16();
			ushort ownershipSequence = reader.ReadUInt16();
			Vector3 position = reader.ReadVector3();
			Quaternion rotation = reader.ReadQuaternion();
			bool atRest = reader.ReadBoolean();
			Vector3 linearVelocity = !atRest ? reader.ReadVector3() : Vector3.zero;
			Vector3 angularVelocity = !atRest ? reader.ReadVector3() : Vector3.zero;

			return new()
			{
				OwnershipSequence = ownershipSequence,
				AuthoritySequence = authoritySequence,
				Position = position,
				Rotation = rotation,
				AtRest = atRest,
				LinearVelocity = linearVelocity,
				AngularVelocity = angularVelocity
			};
		}

		public static void WriteNetworkObjectState(BitWriter writer, NetworkObjectState data)
		{
			writer.WriteUInt16(data.OwnershipSequence);
			writer.WriteUInt16(data.AuthoritySequence);
			writer.WriteVector3(data.Position);
			writer.WriteQuaternion(data.Rotation);
			writer.WriteBoolean(data.AtRest);
			if (!data.AtRest)
			{
				writer.WriteVector3(data.LinearVelocity);
				writer.WriteVector3(data.AngularVelocity);
			}
		}
	}

	public struct NetworkObjectPacket : IStructData
	{
		public enum EPacketTypes : byte
		{
			TakeOwnership = 0,
			ReleaseOwnership = 1,
			TakeAuthority = 2,
			ReleaseAuthority = 3
		}

		public EPacketTypes PacketType;
		public byte ClientID;
		public ushort OwnershipSequence;
		public ushort AuthoritySequence;

		public NetworkObjectPacket(EPacketTypes packetType, byte clientID, ushort ownershipSequence, ushort authoritySequence)
		{
			PacketType = packetType;
			ClientID = clientID;
			OwnershipSequence = ownershipSequence;
			AuthoritySequence = authoritySequence;
		}

		public static NetworkObjectPacket ReadNetworkObjectPacket(BitReader reader)
		{
			return new()
			{
				PacketType = (EPacketTypes)reader.ReadByte(),
				ClientID = reader.ReadByte(),
				OwnershipSequence = reader.ReadUInt16(),
				AuthoritySequence = reader.ReadUInt16()
			};
		}

		public static void WriteNetworkObjectPacket(BitWriter writer, NetworkObjectPacket packet)
		{
			writer.WriteByte((byte)packet.PacketType); 
			writer.WriteByte(packet.ClientID);
			writer.WriteUInt16(packet.OwnershipSequence);
			writer.WriteUInt16(packet.AuthoritySequence);
		}
	}
}
