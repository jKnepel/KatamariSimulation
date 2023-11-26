using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.Katamari
{
	[RequireComponent(typeof(Rigidbody))]
	public class NetworkObject : MonoBehaviour
	{
		[SerializeField] private Rigidbody _rb;
		[SerializeField] private float _velocityDeadzone = 1.5f;

		private bool _atRest = true;

		private void Awake()
		{
			if (_rb == null)
				_rb = GetComponent<Rigidbody>();
		}

		private void FixedUpdate()
		{
			if (_atRest && (_rb.velocity.magnitude > _velocityDeadzone || _rb.angularVelocity.magnitude > _velocityDeadzone))
			{
				_atRest = false;
			}
			else if (!_atRest && (_rb.velocity.magnitude <= _velocityDeadzone || _rb.angularVelocity.magnitude <= _velocityDeadzone))
			{
				_atRest = true;
			}
		}

		public NetworkObjectData GetData()
		{
			return new()
			{
				Position = _rb.position,
				Rotation = _rb.rotation,
				AtRest = _atRest,
				LinearVelocity = _rb.velocity,
				AngularVelocity = _rb.angularVelocity
			};
		}

		public void SetData(NetworkObjectData data)
		{
			_rb.position = data.Position;
			_rb.rotation = data.Rotation;
			_rb.velocity = data.LinearVelocity;
			_rb.angularVelocity = data.AngularVelocity;
		}
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
