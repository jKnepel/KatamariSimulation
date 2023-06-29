using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class Player : MonoBehaviour
{
	#region attributes

	[SerializeField] private Camera _camera;
	[SerializeField] private Vector3 _cameraOffset = new(0, 3, -7);

	[SerializeField] private InputActionProperty _horizontalInput;
	[SerializeField] private InputActionProperty _verticalInput;

	[SerializeField] private float _forceMult = 100;

	private Rigidbody _rb;
	private Vector2 _movementDirection = new();

	#endregion

	#region lifecycle

	private void OnEnable()
	{
		_rb = GetComponent<Rigidbody>();
		if (_camera == null)
			_camera = Camera.main;
	}

	private void Update()
	{
		if (_horizontalInput.action != null)
			_movementDirection.x = _horizontalInput.action.ReadValue<float>();
		if (_verticalInput.action != null)
			_movementDirection.y = _verticalInput.action.ReadValue<float>();
	}

	private void FixedUpdate()
	{
		Vector3 delta = new(_movementDirection.x, 0, _movementDirection.y);
		_rb.AddForce(_forceMult * Time.fixedDeltaTime * delta);
	}

	private void LateUpdate()
	{
		_camera.transform.position = transform.position + _cameraOffset;
		_camera.transform.LookAt(transform);
	}

	private void OnTriggerEnter(Collider other)
	{
		Attachable att = other.GetComponent<Attachable>();
		if (att == null)
			return;

		att.Attach(transform);
	}

	private void OnTriggerExit(Collider other)
	{
		Attachable att = other.GetComponent<Attachable>();
		if (att == null)
			return;

		att.Detach();
	}

	#endregion
}
