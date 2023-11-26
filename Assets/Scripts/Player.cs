using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.Katamari
{
	[RequireComponent(typeof(Rigidbody))]
	public class Player : MonoBehaviour
	{
		#region attributes

		[SerializeField] private NetworkManager _networkManager;
		[SerializeField] private Rigidbody _rb;
		[SerializeField] private Camera _camera;
		[SerializeField] private Vector3 _cameraOffset = new(0, 3, -7);
		[SerializeField] private float _velocityDeadzone = 1.5f;

		[SerializeField] private float _forceMult = 100;

		private DefaultInputActions _input;
		public bool AtRest { get; private set; }
		public Rigidbody Rigidbody => _rb;

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_networkManager == null)
				_networkManager = GameObject.FindWithTag("NetworkManager").GetComponent<NetworkManager>();
			if (_rb == null)
				_rb = GetComponent<Rigidbody>();
			if (_camera == null)
				_camera = Camera.main;

			_input = new();
			_networkManager.OnConnected += () =>
			{
				if (_networkManager.IsHost)
					_input.Enable();
			};
			_networkManager.OnDisconnected += () =>
			{
				if (_networkManager.IsHost)
					_input.Disable();
			};
		}

		private void FixedUpdate()
		{
			if (AtRest && (_rb.velocity.magnitude > _velocityDeadzone || _rb.angularVelocity.magnitude > _velocityDeadzone))
			{
				AtRest = false;
			}
			else if (!AtRest && (_rb.velocity.magnitude <= _velocityDeadzone || _rb.angularVelocity.magnitude <= _velocityDeadzone))
			{
				AtRest = true;
			}

			Vector2 dir = _input.gameplay.directional.ReadValue<Vector2>();
			Vector3 delta = new(dir.x, 0, dir.y);
			_rb.AddForce(_forceMult * Time.fixedDeltaTime * delta);
		}

		private void LateUpdate()
		{
			_camera.transform.position = transform.position + _cameraOffset;
			_camera.transform.LookAt(transform);
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!other.TryGetComponent<Attachable>(out var att))
				return;

			att.Attach(transform);
		}

		private void OnTriggerExit(Collider other)
		{
			if (!other.TryGetComponent<Attachable>(out var att))
				return;

			att.Detach();
		}

		#endregion
	}

	public struct PlayerData
	{
		public Vector3 Position;
		public Quaternion Rotation;
		public bool AtRest;
		public Vector3 LinearVelocity;
		public Vector3 AngularVelocity;

		public PlayerData(Player player)
		{
			Position = player.Rigidbody.position;
			Rotation = player.Rigidbody.rotation;
			AtRest = true;
			LinearVelocity = Vector3.zero;
			AngularVelocity = Vector3.zero;
			if (player.AtRest)
			{
				AtRest = false;
				LinearVelocity = player.Rigidbody.velocity;
				AngularVelocity = player.Rigidbody.angularVelocity;
			}
		}

		public static PlayerData ReadPlayerData(BitReader reader)
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

		public static void WritePlayerData(BitWriter writer, PlayerData data)
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
