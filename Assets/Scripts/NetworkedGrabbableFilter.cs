using jKnepel.Katamari;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

public class NetworkedGrabbableFilter : MonoBehaviour, IXRSelectFilter
{
	[SerializeField] private NetworkObject _networkObject;

	private void Awake()
	{
		if (_networkObject == null)
			_networkObject = GetComponent<NetworkObject>();
	}

	public bool canProcess => isActiveAndEnabled;

	public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
	{
		return Process().Result;
	}

	private async Task<bool> Process()
	{
		bool ownershipTaken = false;
		bool hasReturned = false;
		_networkObject.TakeOwnership((success) =>
		{
			ownershipTaken = success;
			hasReturned = true;
		});
		while (!hasReturned)
		{
			await Task.Delay(100);
		}
		Debug.Log(ownershipTaken);
		return ownershipTaken;
	}
}
