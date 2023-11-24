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

		[SerializeField] private Rigidbody _rb;
		[SerializeField] private Material _attachableMaterial;
		[SerializeField] private Color _restColor = new(0.8f, 0.8f, 0.8f);
		[SerializeField] private Color _activeColor = new(1, 0, 0);
		[SerializeField] private float _velocityDeadzone = 1.5f;
		[SerializeField] private float _gravitationalPull = 3000;
		[SerializeField] private float _fadeDuration = 3;

		public bool IsAttached { get; private set; }
		public bool AtRest { get; private set; }
		public Rigidbody Rigidbody => _rb;

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
			if (AtRest && (_rb.velocity.magnitude > _velocityDeadzone || _rb.angularVelocity.magnitude > _velocityDeadzone))
			{
				_material.color = _activeColor;
				AtRest = false;
			}
			else if (!AtRest && (_rb.velocity.magnitude <= _velocityDeadzone || _rb.angularVelocity.magnitude <= _velocityDeadzone))
			{
				IEnumerator FadeToRestColor()
				{
					float time = 0;
					Color startColor = _material.color;

					while (time < _fadeDuration && !IsAttached)
					{
						_material.color = Color.Lerp(startColor, _restColor, time / _fadeDuration);
						time += Time.deltaTime;
						yield return null;
					}
				}
				StartCoroutine(FadeToRestColor());
				AtRest = true;
			}

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
		}

		#endregion

		#region private methods

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

		public AttachableData(Attachable att)
		{
			Position = att.Rigidbody.position;
			Rotation = att.Rigidbody.rotation;
			AtRest = true;
			LinearVelocity = Vector3.zero;
			AngularVelocity = Vector3.zero;
			if (att.AtRest)
			{
				AtRest = false;
				LinearVelocity = att.Rigidbody.velocity;
				AngularVelocity = att.Rigidbody.angularVelocity;
			}
		}

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
