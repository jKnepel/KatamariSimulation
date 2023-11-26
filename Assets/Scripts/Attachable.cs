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
		[SerializeField] private float _gravitationalPull = 3000;
		[SerializeField] private float _fadeDuration = 3;

		public bool IsAttached { get; private set; }

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_rb == null)
				_rb = GetComponent<Rigidbody>();
			_material = Instantiate(_attachableMaterial);
			_material.color = _restColor;
			GetComponent<Renderer>().material = _material;
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

			IsAttached = true;
			_attachedTo = transform;
			_maxDistance = transform.GetComponents<Collider>().First(x => x.isTrigger).bounds.size.x;
			_material.color = _activeColor;
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

				while (time < _fadeDuration && !IsAttached)
				{
					_material.color = Color.Lerp(startColor, _restColor, time / _fadeDuration);
					time += Time.deltaTime;
					yield return null;
				}
			}
			StartCoroutine(FadeToRestColor());
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
