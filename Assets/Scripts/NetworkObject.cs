using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.Katamari
{
	[RequireComponent(typeof(Rigidbody))]
	public class NetworkObject : MonoBehaviour
	{
		#region attributes

		[SerializeField] private Rigidbody _rb;
		[SerializeField] private float _velocityDeadzone = 1.5f;

		public float PriorityAccumulator => _priorityAccumulator;
		private float _priorityAccumulator = 0;
		public float Priority => _priority;
		private float _priority = 0.1f;

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_rb == null)
				_rb = GetComponent<Rigidbody>();
		}

		private void FixedUpdate()
		{
			_priorityAccumulator += _priority;
		}

		#endregion

		#region public methods

		public NetworkObjectData GetData()
		{
			return new()
			{
				Position = _rb.position,
				Rotation = _rb.rotation,
				AtRest = _rb.velocity.magnitude < _velocityDeadzone 
					  || _rb.angularVelocity.magnitude < _velocityDeadzone,
				LinearVelocity = _rb.velocity,
				AngularVelocity = _rb.angularVelocity
			};
		}

		public void SetData(NetworkObjectData data)
		{
			_rb.position = data.Position;
			_rb.rotation = data.Rotation;
			if (data.AtRest)
				return;
			_rb.velocity = data.LinearVelocity;
			_rb.angularVelocity = data.AngularVelocity;
		}

		public void ResetPriority() => _priorityAccumulator = 0;

		public int CompareTo(NetworkObject other)
		{
			if (other == null) return 1;
			if (PriorityAccumulator == other.PriorityAccumulator) return 0;
			if (PriorityAccumulator < other.PriorityAccumulator) return 1;
			return -1;
		}

		#endregion
	}

	public struct NetworkObjectData
	{
		public Vector3 Position;
		public Quaternion Rotation;
		public bool AtRest;
		public Vector3 LinearVelocity;
		public Vector3 AngularVelocity;

		public static NetworkObjectData ReadNetworkObjectData(BitReader reader)
		{
			Vector3 position = reader.ReadVector3();
			Quaternion rotation = reader.ReadQuaternion();
			bool atRest = reader.ReadBoolean();
			Vector3 linearVelocity = !atRest ? reader.ReadVector3() : Vector3.zero;
			Vector3 angularVelocity = !atRest ? reader.ReadVector3() : Vector3.zero;

			return new()
			{
				Position = position,
				Rotation = rotation,
				AtRest = atRest,
				LinearVelocity = linearVelocity,
				AngularVelocity = angularVelocity
			};
		}

		public static void WriteNetworkObjectData(BitWriter writer, NetworkObjectData data)
		{
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
}
