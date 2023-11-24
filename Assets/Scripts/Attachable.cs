using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace jKnepel.Katamari
{
	[RequireComponent(typeof(Rigidbody), typeof(Collider))]
	public class Attachable : MonoBehaviour
	{
		#region attributes

		private Transform _attachedTo;
		private Material _material;
		private float _maxDistance = 0;

		private const float FADE_DURATION = 3;

		[SerializeField] private Rigidbody _rb;
		[SerializeField] private Material _attachableMaterial;
		[SerializeField] private Color _restColor = new(0.8f, 0.8f, 0.8f);
		[SerializeField] private Color _activeColor = new(1, 0, 0);
		[SerializeField] private float _velocityDeadzone = 1.5f;

		[SerializeField] private float _gravitationalPull = 3000;

		[SerializeField] private bool _isAttached;
		public bool IsAttached
		{
			get => _isAttached;
			private set => _isAttached = value;
		}

		private NetworkManager _networkManager;
		private string _name;

		#endregion

		#region lifecycle

		private void Awake()
		{
			_material = Instantiate(_attachableMaterial);
			_material.color = _restColor;
			GetComponent<Renderer>().material = _material;
		}

		private void FixedUpdate()
		{
			if (_networkManager.IsConnected && _networkManager.IsHost)
				SendAttachable();

			if (!IsAttached)
				return;

			float distance = Vector3.Distance(transform.position, _attachedTo.position);
			float strength = Map(distance, _maxDistance, 0, 0, _gravitationalPull);
			_material.color = Color.Lerp(_activeColor, _restColor, Map(distance, 0, _maxDistance, 1, 0));
			_rb.AddForce(strength * Time.fixedDeltaTime * (_attachedTo.position - transform.position));
		}

		#endregion

		#region public methods

		public void Initiate(int index)
		{
			_name = $"Attachable#{index}";
			if (_networkManager == null)
				_networkManager = GameObject.FindWithTag("NetworkManager").GetComponent<NetworkManager>();
			_networkManager.OnConnected += () => _networkManager.RegisterByteData(_name, UpdateAttachable);
			_networkManager.OnDisconnected += () => _networkManager.UnregisterByteData(_name, UpdateAttachable);
		}

		public void Attach(Transform transform)
		{
			if (IsAttached)
				return;

			IsAttached = true;
			_attachedTo = transform;
			_maxDistance = transform.GetComponents<Collider>().First(x => x.isTrigger).bounds.size.x;
		}

		public void Detach()
		{
			if (!IsAttached)
				return;

			IsAttached = false;
			_attachedTo = null;
			_maxDistance = 0;
			IEnumerator FadeToRestColor()
			{
				float time = 0;
				Color startColor = _material.color;

				while (time < FADE_DURATION && !IsAttached)
				{
					_material.color = Color.Lerp(startColor, _restColor, time / FADE_DURATION);
					time += Time.deltaTime;
					yield return null;
				}
			}
			StartCoroutine(FadeToRestColor());
		}

		#endregion

		#region private methods

		private void SendAttachable()
		{
			Vector3 position = _rb.position;
			Quaternion rotation = _rb.rotation;
			bool atRest = true;
			Vector3 linearVelocity = Vector3.zero;
			Vector3 angularVelocity = Vector3.zero;
			if (_rb.velocity.magnitude > _velocityDeadzone || _rb.angularVelocity.magnitude > _velocityDeadzone)
			{
				atRest = false;
				linearVelocity = _rb.velocity;
				angularVelocity = _rb.angularVelocity;
			}

			AttachableData data = new()
			{
				Position = position,
				Rotation = rotation,
				AtRest = atRest,
				LinearVelocity = linearVelocity,
				AngularVelocity = angularVelocity
			};

			BitWriter _bitWriter = new(_networkManager.NetworkConfiguration.SerialiserConfiguration);
			_bitWriter.Write(data);
			
			_networkManager.SendByteDataToAll(_name, _bitWriter.GetBuffer(), ENetworkChannel.UnreliableOrdered);
		}

		private void UpdateAttachable(byte sender, byte[] data)
		{
			BitReader reader = new(data, _networkManager.NetworkConfiguration.SerialiserConfiguration);
			AttachableData attachableData = reader.Read<AttachableData>();
			_rb.position = attachableData.Position;
			_rb.rotation = attachableData.Rotation;
			_rb.velocity = attachableData.LinearVelocity;
			_rb.angularVelocity = attachableData.AngularVelocity;
		}

		private static float Map(float value, float from1, float from2, float to1, float to2)
		{
			return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
		}

		#endregion
	}

	public struct AttachableData
	{
		public Vector3 Position;
		public Quaternion Rotation;
		public bool AtRest;
		public Vector3 LinearVelocity;
		public Vector3 AngularVelocity;

		public static AttachableData ReadAttachableData(BitReader reader)
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

		public static void WriteAttachableData(BitWriter writer, AttachableData data)
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
