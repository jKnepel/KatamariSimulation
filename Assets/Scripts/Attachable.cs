using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Attachable : MonoBehaviour
{
	#region attributes

	private Rigidbody	_rb;
	private Transform	_attachedTo;
	private Material	_material;
	private float		_maxDistance = 0;

	private const float FADE_DURATION = 3;

	[SerializeField] private Material	_attachableMaterial;
	[SerializeField] private Color		_restColor = new(0.8f, 0.8f, 0.8f, 1);
	[SerializeField] private Color		_activeColor = new(1, 0, 0, 1);

	[SerializeField] private float _gravitationalPull = 2000;

	[SerializeField] private bool _isAttached;
    public bool IsAttached 
    {
        get => _isAttached; 
        private set => _isAttached = value; 
    }

	#endregion

	#region lifecycle

	private void OnEnable()
	{
		_rb = GetComponent<Rigidbody>();
		_material = Instantiate(_attachableMaterial);
		GetComponent<Renderer>().material = _material;
		_material.color = _restColor;
	}

	private void FixedUpdate()
	{
		if (!IsAttached)
			return;

		float distance = Vector3.Distance(transform.position, _attachedTo.position);
		_material.color = Color.Lerp(_activeColor, _restColor, Map(distance, 0, _maxDistance, 1, 0));
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

	private static float Map(float value, float from1, float from2, float to1, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}

	#endregion
}
