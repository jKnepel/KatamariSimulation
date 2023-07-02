using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Attachable : MonoBehaviour
{
	#region attributes

	private Rigidbody _rb;
	private Transform _attachedTo;

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
	}

	private void FixedUpdate()
	{
		if (!_isAttached)
			return;

		float distance = Vector3.Distance(transform.position, _attachedTo.position);
		float strength = Map(distance, 5, 0, 0, _gravitationalPull);
		_rb.AddForce(strength * Time.fixedDeltaTime * (_attachedTo.position - transform.position));
	}

	#endregion

	#region public methods

	public void Attach(Transform transform)
	{
		if (_isAttached)
			return;

		_isAttached = true;
		_attachedTo = transform;
	}

	public void Detach()
	{
		if (!_isAttached)
			return;

		_isAttached = false;
		_attachedTo = null;
	}

	#endregion

	#region private methods

	private static float Map(float value, float from1, float from2, float to1, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}

	#endregion
}
