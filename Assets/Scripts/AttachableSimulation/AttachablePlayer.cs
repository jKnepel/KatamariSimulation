using jKnepel.SimpleUnityNetworking.Managing;
using UnityEngine;

namespace jKnepel.Katamari.AttachableSimulation
{
	[RequireComponent(typeof(Rigidbody))]
	public class AttachablePlayer : MonoBehaviour
	{
		#region attributes

		[SerializeField] private NetworkManager _networkManager;
		[SerializeField] private Rigidbody _rb;

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

			_input = new();
			_networkManager.OnConnected += _input.Enable;
			_networkManager.OnDisconnected += _input.Disable;
		}

		private void FixedUpdate()
		{
			Vector2 dir = _input.gameplay.directional.ReadValue<Vector2>();
			Vector3 delta = new(dir.x, 0, dir.y);
			_rb.AddForce(_forceMult * Time.fixedDeltaTime * delta);
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
