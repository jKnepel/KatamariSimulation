using jKnepel.SimpleUnityNetworking.Managing;
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

		[SerializeField] private float _forceMult = 100;

		private DefaultInputActions _input;

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
}
