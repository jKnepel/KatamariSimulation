using System.Linq;
using UnityEngine;

namespace jKnepel.Katamari.AttachableSimulation
{
	[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
	public class Attachable : MonoBehaviour
	{
		#region attributes

		private Transform _attachedTo;
		private float _maxDistance = 0;

		[SerializeField] private NetworkObject _networkObject;
		[SerializeField] private Rigidbody _rb;
		[SerializeField] private float _gravitationalPull = 3000;

		public bool IsAttached { get; private set; }

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_networkObject == null)
				_networkObject = GetComponent<NetworkObject>();
			if (_rb == null)
				_rb = GetComponent<Rigidbody>();
		}

		private void FixedUpdate()
		{
			if (!IsAttached)
				return;

			float distance = Vector3.Distance(transform.position, _attachedTo.position);
			float strength = Map(distance, _maxDistance, 0, 0, _gravitationalPull);
			_rb.AddForce(strength * Time.fixedDeltaTime * (_attachedTo.position - transform.position));
		}

		#endregion

		#region public methods

		public void Attach(Transform transform)
		{
			if (IsAttached)
				return;

			_networkObject.TakeOwnership((success) => Attach(success, transform));
		}

		private void Attach(bool success, Transform transform)
		{
			if (!success || IsAttached)
				return;

			IsAttached = true;
			_attachedTo = transform;
			_maxDistance = transform.GetComponents<Collider>().First(x => x.isTrigger).bounds.size.x;
		}

		public void Detach()
		{
			if (!IsAttached)
				return;

			_networkObject.ReleaseOwnership();
			IsAttached = false;
			_attachedTo = null;
			_maxDistance = 0;
		}

		#endregion

		#region private methods

		private static float Map(float value, float from1, float from2, float to1, float to2)
		{
			return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
		}

		#endregion
	}
}
